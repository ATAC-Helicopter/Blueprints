using System.Text.Json;
using Blueprints.Collaboration.Models;
using Blueprints.Security.Models;
using Blueprints.Storage.Models;

namespace Blueprints.Collaboration.Services;

public sealed class FileSystemWorkspaceSyncService
{
    private readonly WorkspaceExchangeSnapshotBuilder _snapshotBuilder;
    private readonly WorkspaceSyncAnalyzer _analyzer;
    private readonly FileSystemSyncManifestStore _manifestStore;
    private readonly FileSystemSyncStateStore _syncStateStore;
    private readonly WorkspaceExchangeValidator _exchangeValidator;
    private readonly FileSystemAuditLogService _auditLogService;

    public FileSystemWorkspaceSyncService(
        WorkspaceExchangeSnapshotBuilder snapshotBuilder,
        WorkspaceSyncAnalyzer analyzer,
        FileSystemSyncManifestStore manifestStore,
        FileSystemSyncStateStore syncStateStore,
        WorkspaceExchangeValidator exchangeValidator,
        FileSystemAuditLogService auditLogService)
    {
        _snapshotBuilder = snapshotBuilder;
        _analyzer = analyzer;
        _manifestStore = manifestStore;
        _syncStateStore = syncStateStore;
        _exchangeValidator = exchangeValidator;
        _auditLogService = auditLogService;
    }

    public WorkspaceSyncResult Push(
        WorkspacePaths workspacePaths,
        Guid projectId,
        SignatureKeyMaterial signingKey,
        SignaturePublicKey publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return Push(
            workspacePaths,
            projectId,
            signingKey,
            new Dictionary<string, SignaturePublicKey>(StringComparer.Ordinal)
            {
                [publicKey.KeyId] = publicKey,
            });
    }

    public WorkspaceSyncResult Push(
        WorkspacePaths workspacePaths,
        Guid projectId,
        SignatureKeyMaterial signingKey,
        IReadOnlyDictionary<string, SignaturePublicKey> publicKeys)
    {
        ArgumentNullException.ThrowIfNull(workspacePaths);
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(publicKeys);

        Directory.CreateDirectory(workspacePaths.SharedProjectRoot);

        var state = _syncStateStore.Load(workspacePaths.LocalWorkspaceRoot);
        var analysis = _analyzer.Analyze(workspacePaths, state.TrackedEntries);
        if (analysis.HasConflicts)
        {
            return new WorkspaceSyncResult(
                false,
                "push",
                0,
                state.LastPushedManifestVersion,
                string.Empty,
                analysis.PotentialConflictDocumentPaths,
                "Push blocked because local and shared changes overlap.");
        }

        if (!analysis.HasOutgoingChanges)
        {
            return new WorkspaceSyncResult(
                true,
                "push",
                0,
                state.LastPushedManifestVersion,
                string.Empty,
                [],
                "No outgoing changes detected.");
        }

        var deletedRequiredDocuments = analysis.OutgoingDocumentPaths
            .Where(path =>
                IsRequiredDocument(path) &&
                !File.Exists(ResolveWorkspacePath(workspacePaths.LocalWorkspaceRoot, path)))
            .ToArray();
        if (deletedRequiredDocuments.Length > 0)
        {
            return new WorkspaceSyncResult(
                false,
                "push",
                0,
                state.LastPushedManifestVersion,
                string.Empty,
                deletedRequiredDocuments,
                "Push blocked because required project documents cannot be deleted.");
        }

        var currentManifestVersion = TryReadManifestVersion(workspacePaths.SharedProjectRoot, publicKeys);
        var lastKnownManifestVersion = Math.Max(
            state.LastPulledManifestVersion,
            state.LastPushedManifestVersion);
        if (currentManifestVersion < lastKnownManifestVersion)
        {
            return new WorkspaceSyncResult(
                false,
                "push",
                0,
                state.LastPushedManifestVersion,
                string.Empty,
                [],
                $"Push blocked because the shared manifest rolled back from known version {lastKnownManifestVersion} to {currentManifestVersion}.");
        }

        var batchId = CreateBatchId();
        var stageRoot = Path.Combine(workspacePaths.LocalWorkspaceRoot, "sync", "staging", batchId);
        var packRoot = Path.Combine(workspacePaths.SharedProjectRoot, "packs", batchId);

        foreach (var documentPath in analysis.OutgoingDocumentPaths)
        {
            SnapshotOrMarkDeletion(
                workspacePaths.LocalWorkspaceRoot,
                stageRoot,
                documentPath);
            MirrorDocumentPair(
                workspacePaths.LocalWorkspaceRoot,
                workspacePaths.SharedProjectRoot,
                documentPath);
            SnapshotOrMarkDeletion(
                workspacePaths.LocalWorkspaceRoot,
                packRoot,
                documentPath);
        }

        var nextManifestVersion = currentManifestVersion + 1;
        var manifestWrite = _manifestStore.Write(
            workspacePaths.SharedProjectRoot,
            projectId,
            nextManifestVersion,
            batchId,
            signingKey);

        var manifestEntries = _snapshotBuilder.Build(workspacePaths.SharedProjectRoot);
        _syncStateStore.Save(
            workspacePaths.LocalWorkspaceRoot,
            state with
            {
                LastPushedManifestVersion = nextManifestVersion,
                LastPulledManifestVersion = Math.Max(state.LastPulledManifestVersion, currentManifestVersion),
                LastSuccessfulTrustValidationUtc = DateTimeOffset.UtcNow,
                KnownRemoteBatchIds = AppendUnique(state.KnownRemoteBatchIds, batchId),
                UnresolvedConflicts = [],
                TrackedEntries = manifestEntries
                    .Select(static entry => new SyncTrackedEntry(entry.DocumentPath, entry.DocumentHash, entry.SignatureHash))
                    .ToArray(),
            });

        return new WorkspaceSyncResult(
            true,
            "push",
            analysis.OutgoingDocumentPaths.Count,
            nextManifestVersion,
            batchId,
            [],
            $"Pushed {analysis.OutgoingDocumentPaths.Count} documents and published manifest {nextManifestVersion}.");
    }

    public WorkspaceSyncResult Pull(
        WorkspacePaths workspacePaths,
        SignaturePublicKey publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return Pull(
            workspacePaths,
            new Dictionary<string, SignaturePublicKey>(StringComparer.Ordinal)
            {
                [publicKey.KeyId] = publicKey,
            });
    }

    public WorkspaceSyncResult Pull(
        WorkspacePaths workspacePaths,
        IReadOnlyDictionary<string, SignaturePublicKey> publicKeys)
    {
        return Pull(workspacePaths, publicKeys, publicKeys);
    }

    public WorkspaceSyncResult Pull(
        WorkspacePaths workspacePaths,
        IReadOnlyDictionary<string, SignaturePublicKey> publicKeys,
        IReadOnlyDictionary<string, SignaturePublicKey> auditPublicKeys)
    {
        ArgumentNullException.ThrowIfNull(workspacePaths);
        ArgumentNullException.ThrowIfNull(publicKeys);
        ArgumentNullException.ThrowIfNull(auditPublicKeys);

        var state = _syncStateStore.Load(workspacePaths.LocalWorkspaceRoot);
        SignedManifestReadResult manifestResult;

        try
        {
            manifestResult = ReadManifest(workspacePaths.SharedProjectRoot, publicKeys);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new WorkspaceSyncResult(
                true,
                "pull",
                0,
                state.LastPulledManifestVersion,
                string.Empty,
                [],
                "Shared manifest does not exist yet.");
        }

        if (!manifestResult.IsSignatureValid)
        {
            return new WorkspaceSyncResult(
                false,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                [],
                "Pull blocked because the shared manifest signature is invalid.");
        }

        if (manifestResult.Document.ManifestVersion < state.LastPulledManifestVersion)
        {
            return new WorkspaceSyncResult(
                false,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                [],
                $"Pull blocked because the shared manifest rolled back from version {state.LastPulledManifestVersion} to {manifestResult.Document.ManifestVersion}.");
        }

        if (manifestResult.Document.ManifestVersion == state.LastPulledManifestVersion &&
            state.LastPulledManifestVersion > 0 &&
            !state.KnownRemoteBatchIds.Contains(
                manifestResult.Document.BatchId,
                StringComparer.Ordinal))
        {
            return new WorkspaceSyncResult(
                false,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                [],
                "Pull blocked because a known manifest version was reused with an unknown batch.");
        }

        var manifestContinuityFailures = FindManifestContinuityFailures(
            workspacePaths.SharedProjectRoot,
            manifestResult.Document.Entries);
        if (manifestContinuityFailures.Count > 0)
        {
            return new WorkspaceSyncResult(
                false,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                manifestContinuityFailures,
                "Pull blocked because the shared manifest no longer matches the shared folder content.");
        }

        var auditValidation = _auditLogService.Validate(
            workspacePaths.SharedProjectRoot,
            auditPublicKeys);
        if (!auditValidation.IsValid)
        {
            return new WorkspaceSyncResult(
                false,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                auditValidation.InvalidEntryPaths,
                "Pull blocked because the shared audit log is invalid.");
        }

        var analysis = _analyzer.Analyze(workspacePaths, state.TrackedEntries);
        if (analysis.HasConflicts)
        {
            _syncStateStore.Save(
                workspacePaths.LocalWorkspaceRoot,
                state with
                {
                    UnresolvedConflicts = analysis.PotentialConflictDocumentPaths,
                });

            return new WorkspaceSyncResult(
                false,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                analysis.PotentialConflictDocumentPaths,
                "Pull blocked because local and shared changes overlap.");
        }

        var deletedRequiredDocuments = analysis.IncomingDocumentPaths
            .Where(path =>
                IsRequiredDocument(path) &&
                !File.Exists(ResolveWorkspacePath(workspacePaths.SharedProjectRoot, path)))
            .ToArray();
        if (deletedRequiredDocuments.Length > 0)
        {
            return new WorkspaceSyncResult(
                false,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                deletedRequiredDocuments,
                "Pull blocked because required project documents are missing from the shared workspace.");
        }

        if (!analysis.HasIncomingChanges && manifestResult.Document.ManifestVersion <= state.LastPulledManifestVersion)
        {
            return new WorkspaceSyncResult(
                true,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                [],
                "No incoming changes detected.");
        }

        var validationResult = _exchangeValidator.Validate(
            workspacePaths.SharedProjectRoot,
            analysis.IncomingDocumentPaths.Where(path =>
                File.Exists(ResolveWorkspacePath(workspacePaths.SharedProjectRoot, path))),
            publicKeys);
        if (!validationResult.IsValid)
        {
            return new WorkspaceSyncResult(
                false,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                validationResult.InvalidDocumentPaths,
                "Pull blocked because one or more incoming document signatures are invalid.");
        }

        var inboxRoot = Path.Combine(
            workspacePaths.LocalWorkspaceRoot,
            "sync",
            "inbox",
            manifestResult.Document.BatchId);
        var incomingRoot = Path.Combine(inboxRoot, "incoming");
        var rollbackRoot = Path.Combine(inboxRoot, "rollback");
        foreach (var documentPath in analysis.IncomingDocumentPaths)
        {
            if (File.Exists(ResolveWorkspacePath(workspacePaths.SharedProjectRoot, documentPath)))
            {
                CopyDocumentPair(
                    workspacePaths.SharedProjectRoot,
                    incomingRoot,
                    documentPath);
            }
            else
            {
                WriteDeletionMarker(
                    incomingRoot,
                    documentPath);
            }

            SnapshotOrMarkDeletion(
                workspacePaths.LocalWorkspaceRoot,
                rollbackRoot,
                documentPath);
        }

        var stagedValidation = _exchangeValidator.Validate(
            incomingRoot,
            analysis.IncomingDocumentPaths.Where(path =>
                File.Exists(ResolveWorkspacePath(incomingRoot, path))),
            publicKeys);
        var stagedContinuityFailures = FindStagedContinuityFailures(
            incomingRoot,
            analysis.IncomingDocumentPaths,
            manifestResult.Document.Entries);
        if (!stagedValidation.IsValid || stagedContinuityFailures.Count > 0)
        {
            return new WorkspaceSyncResult(
                false,
                "pull",
                0,
                state.LastPulledManifestVersion,
                manifestResult.Document.BatchId,
                stagedValidation.InvalidDocumentPaths
                    .Concat(stagedContinuityFailures)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                "Pull blocked because the staged inbox no longer matches the signed shared manifest.");
        }

        try
        {
            foreach (var documentPath in analysis.IncomingDocumentPaths)
            {
                MirrorDocumentPair(
                    incomingRoot,
                    workspacePaths.LocalWorkspaceRoot,
                    documentPath);
            }

            _syncStateStore.Save(
                workspacePaths.LocalWorkspaceRoot,
                state with
                {
                    LastPulledManifestVersion = manifestResult.Document.ManifestVersion,
                    LastSuccessfulTrustValidationUtc = DateTimeOffset.UtcNow,
                    KnownRemoteBatchIds = AppendUnique(state.KnownRemoteBatchIds, manifestResult.Document.BatchId),
                    UnresolvedConflicts = [],
                    TrackedEntries = manifestResult.Document.Entries
                        .Select(static entry => new SyncTrackedEntry(entry.DocumentPath, entry.DocumentHash, entry.SignatureHash))
                        .ToArray(),
                });
        }
        catch
        {
            foreach (var documentPath in analysis.IncomingDocumentPaths)
            {
                MirrorDocumentPair(
                    rollbackRoot,
                    workspacePaths.LocalWorkspaceRoot,
                    documentPath);
            }

            throw;
        }

        return new WorkspaceSyncResult(
            true,
            "pull",
            analysis.IncomingDocumentPaths.Count,
            manifestResult.Document.ManifestVersion,
            manifestResult.Document.BatchId,
            [],
            $"Pulled {analysis.IncomingDocumentPaths.Count} documents from manifest {manifestResult.Document.ManifestVersion}.");
    }

    public SharedManifestStatus InspectSharedManifest(
        string sharedProjectRoot,
        IReadOnlyDictionary<string, SignaturePublicKey> publicKeys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedProjectRoot);
        ArgumentNullException.ThrowIfNull(publicKeys);

        try
        {
            var result = _manifestStore.Read(sharedProjectRoot, publicKeys);
            return new SharedManifestStatus(
                true,
                result.IsSignatureValid,
                result.Document.ManifestVersion,
                result.Document.BatchId,
                result.IsSignatureValid
                    ? $"Shared manifest {result.Document.ManifestVersion} is signed by a trusted project key."
                    : "The shared manifest signature is not trusted.");
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new SharedManifestStatus(
                false,
                false,
                null,
                string.Empty,
                "No shared manifest has been published yet.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or JsonException)
        {
            return new SharedManifestStatus(
                true,
                false,
                null,
                string.Empty,
                $"The shared manifest could not be read: {exception.Message}");
        }
    }

    private SignedManifestReadResult ReadManifest(
        string sharedProjectRoot,
        IReadOnlyDictionary<string, SignaturePublicKey> publicKeys)
    {
        var result = _manifestStore.Read(sharedProjectRoot, publicKeys);
        return new SignedManifestReadResult(result.Document, result.IsSignatureValid);
    }

    private int TryReadManifestVersion(
        string sharedProjectRoot,
        IReadOnlyDictionary<string, SignaturePublicKey> publicKeys)
    {
        try
        {
            var result = _manifestStore.Read(sharedProjectRoot, publicKeys);
            return result.IsSignatureValid ? result.Document.ManifestVersion : 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return 0;
        }
    }

    private IReadOnlyList<string> FindManifestContinuityFailures(
        string sharedProjectRoot,
        IReadOnlyList<SyncManifestEntry> manifestEntries)
    {
        try
        {
            var sharedEntries = _snapshotBuilder.Build(sharedProjectRoot)
                .ToDictionary(static entry => entry.DocumentPath, StringComparer.Ordinal);
            return manifestEntries
                .Where(entry => !sharedEntries.TryGetValue(entry.DocumentPath, out var sharedEntry) || !Matches(entry, sharedEntry))
                .Select(static entry => entry.DocumentPath)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
        }
        catch (FileNotFoundException exception)
        {
            return [Path.GetRelativePath(sharedProjectRoot, exception.FileName ?? sharedProjectRoot).Replace('\\', '/')];
        }
    }

    private IReadOnlyList<string> FindStagedContinuityFailures(
        string incomingRoot,
        IReadOnlyList<string> incomingDocumentPaths,
        IReadOnlyList<SyncManifestEntry> manifestEntries)
    {
        var expected = manifestEntries.ToDictionary(
            static entry => entry.DocumentPath,
            StringComparer.Ordinal);
        var staged = _snapshotBuilder.Build(incomingRoot).ToDictionary(
            static entry => entry.DocumentPath,
            StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var path in incomingDocumentPaths)
        {
            var expectsDocument = expected.TryGetValue(path, out var expectedEntry);
            var hasStagedDocument = staged.TryGetValue(path, out var stagedEntry);
            if (expectsDocument != hasStagedDocument)
            {
                failures.Add(path);
                continue;
            }

            if (expectsDocument &&
                (!string.Equals(
                    expectedEntry!.DocumentHash,
                    stagedEntry!.DocumentHash,
                    StringComparison.Ordinal) ||
                 !string.Equals(
                    expectedEntry.SignatureHash,
                    stagedEntry.SignatureHash,
                    StringComparison.Ordinal)))
            {
                failures.Add(path);
            }
        }

        return failures;
    }

    private static bool Matches(SyncManifestEntry left, SyncManifestEntry right) =>
        string.Equals(left.DocumentHash, right.DocumentHash, StringComparison.Ordinal)
        && string.Equals(left.SignatureHash, right.SignatureHash, StringComparison.Ordinal);

    private static void CopyDocumentPair(string sourceRoot, string destinationRoot, string documentPath)
    {
        CopyFile(sourceRoot, destinationRoot, documentPath);
        CopyFile(sourceRoot, destinationRoot, Path.ChangeExtension(documentPath, ".sig"));
    }

    private static void SnapshotOrMarkDeletion(
        string sourceRoot,
        string destinationRoot,
        string documentPath)
    {
        var signaturePath = Path.ChangeExtension(documentPath, ".sig");
        var documentExists = File.Exists(ResolveWorkspacePath(sourceRoot, documentPath));
        var signatureExists = File.Exists(ResolveWorkspacePath(sourceRoot, signaturePath));
        if (documentExists != signatureExists)
        {
            throw new InvalidOperationException(
                $"Document/signature pair is incomplete for {documentPath}.");
        }

        if (!documentExists)
        {
            WriteDeletionMarker(destinationRoot, documentPath);
            return;
        }

        CopyDocumentPair(sourceRoot, destinationRoot, documentPath);
    }

    private static void MirrorDocumentPair(
        string sourceRoot,
        string destinationRoot,
        string documentPath)
    {
        var signaturePath = Path.ChangeExtension(documentPath, ".sig");
        var documentExists = File.Exists(ResolveWorkspacePath(sourceRoot, documentPath));
        var signatureExists = File.Exists(ResolveWorkspacePath(sourceRoot, signaturePath));
        if (documentExists != signatureExists)
        {
            throw new InvalidOperationException(
                $"Document/signature pair is incomplete for {documentPath}.");
        }

        if (!documentExists)
        {
            DeleteFileIfPresent(destinationRoot, documentPath);
            DeleteFileIfPresent(destinationRoot, signaturePath);
            return;
        }

        CopyDocumentPair(sourceRoot, destinationRoot, documentPath);
    }

    private static void WriteDeletionMarker(string root, string documentPath)
    {
        var markerPath = ResolveWorkspacePath(root, documentPath + ".deleted");
        var directory = Path.GetDirectoryName(markerPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(markerPath, documentPath);
    }

    private static void DeleteFileIfPresent(string root, string relativePath)
    {
        var path = ResolveWorkspacePath(root, relativePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool IsRequiredDocument(string documentPath) =>
        string.Equals(documentPath, "project/project.json", StringComparison.Ordinal) ||
        string.Equals(documentPath, "project/members.json", StringComparison.Ordinal);

    private static void CopyFile(string sourceRoot, string destinationRoot, string relativePath)
    {
        var sourcePath = ResolveWorkspacePath(sourceRoot, relativePath);
        var destinationPath = ResolveWorkspacePath(destinationRoot, relativePath);
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = destinationPath + ".tmp";
        File.Copy(sourcePath, tempPath, overwrite: true);

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(tempPath, destinationPath);
    }

    private static string ResolveWorkspacePath(string workspaceRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Workspace document path must be relative.");
        }

        var fullRoot = Path.GetFullPath(workspaceRoot);
        var fullPath = Path.GetFullPath(
            Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relativeToRoot = Path.GetRelativePath(fullRoot, fullPath);
        if (relativeToRoot == ".." ||
            relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Workspace document path escapes its expected root.");
        }

        return fullPath;
    }

    private static string CreateBatchId() =>
        $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}_{Guid.NewGuid():N}";

    private static IReadOnlyList<string> AppendUnique(IReadOnlyList<string> values, string newValue)
    {
        if (values.Contains(newValue, StringComparer.Ordinal))
        {
            return values;
        }

        return values.Concat([newValue]).ToArray();
    }

    private sealed record SignedManifestReadResult(
        SyncManifestDocument Document,
        bool IsSignatureValid);
}
