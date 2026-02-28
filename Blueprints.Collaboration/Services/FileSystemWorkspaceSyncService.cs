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

    public FileSystemWorkspaceSyncService(
        WorkspaceExchangeSnapshotBuilder snapshotBuilder,
        WorkspaceSyncAnalyzer analyzer,
        FileSystemSyncManifestStore manifestStore,
        FileSystemSyncStateStore syncStateStore)
    {
        _snapshotBuilder = snapshotBuilder;
        _analyzer = analyzer;
        _manifestStore = manifestStore;
        _syncStateStore = syncStateStore;
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
        catch (FileNotFoundException)
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
        catch (FileNotFoundException)
        {
            return 0;
        }
    }

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
