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
        ArgumentNullException.ThrowIfNull(workspacePaths);
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(publicKey);

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

        var batchId = CreateBatchId();
        var stageRoot = Path.Combine(workspacePaths.LocalWorkspaceRoot, "sync", "staging", batchId);
        var packRoot = Path.Combine(workspacePaths.SharedProjectRoot, "packs", batchId);

        foreach (var documentPath in analysis.OutgoingDocumentPaths)
        {
            CopyDocumentPair(workspacePaths.LocalWorkspaceRoot, stageRoot, documentPath);
            CopyDocumentPair(workspacePaths.LocalWorkspaceRoot, workspacePaths.SharedProjectRoot, documentPath);
            CopyDocumentPair(workspacePaths.LocalWorkspaceRoot, packRoot, documentPath);
        }

        var currentManifestVersion = TryReadManifestVersion(workspacePaths.SharedProjectRoot, publicKey);
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
        ArgumentNullException.ThrowIfNull(workspacePaths);
        ArgumentNullException.ThrowIfNull(publicKey);

        var state = _syncStateStore.Load(workspacePaths.LocalWorkspaceRoot);
        SignedManifestReadResult manifestResult;

        try
        {
            manifestResult = ReadManifest(workspacePaths.SharedProjectRoot, publicKey);
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

        var auditValidation = _auditLogService.Validate(workspacePaths.SharedProjectRoot, publicKey);
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
            analysis.IncomingDocumentPaths,
            publicKey);
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

        var inboxRoot = Path.Combine(workspacePaths.LocalWorkspaceRoot, "sync", "inbox", manifestResult.Document.BatchId);
        foreach (var documentPath in analysis.IncomingDocumentPaths)
        {
            CopyDocumentPair(workspacePaths.SharedProjectRoot, inboxRoot, documentPath);
            CopyDocumentPair(workspacePaths.SharedProjectRoot, workspacePaths.LocalWorkspaceRoot, documentPath);
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

        return new WorkspaceSyncResult(
            true,
            "pull",
            analysis.IncomingDocumentPaths.Count,
            manifestResult.Document.ManifestVersion,
            manifestResult.Document.BatchId,
            [],
            $"Pulled {analysis.IncomingDocumentPaths.Count} documents from manifest {manifestResult.Document.ManifestVersion}.");
    }

    private SignedManifestReadResult ReadManifest(string sharedProjectRoot, SignaturePublicKey publicKey)
    {
        var result = _manifestStore.Read(sharedProjectRoot, publicKey);
        return new SignedManifestReadResult(result.Document, result.IsSignatureValid);
    }

    private int TryReadManifestVersion(string sharedProjectRoot, SignaturePublicKey publicKey)
    {
        try
        {
            var result = _manifestStore.Read(sharedProjectRoot, publicKey);
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

    private static bool Matches(SyncManifestEntry left, SyncManifestEntry right) =>
        string.Equals(left.DocumentHash, right.DocumentHash, StringComparison.Ordinal)
        && string.Equals(left.SignatureHash, right.SignatureHash, StringComparison.Ordinal);

    private static void CopyDocumentPair(string sourceRoot, string destinationRoot, string documentPath)
    {
        CopyFile(sourceRoot, destinationRoot, documentPath);
        CopyFile(sourceRoot, destinationRoot, Path.ChangeExtension(documentPath, ".sig"));
    }

    private static void CopyFile(string sourceRoot, string destinationRoot, string relativePath)
    {
        var sourcePath = Path.Combine(sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var destinationPath = Path.Combine(destinationRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
