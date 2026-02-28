using Blueprints.Collaboration.Models;
using Blueprints.Security.Models;
using Blueprints.Storage.Abstractions;
using Blueprints.Storage.Models;

namespace Blueprints.Collaboration.Services;

public sealed class FileSystemSyncManifestStore
{
    private const int CurrentSchemaVersion = 1;
    private readonly ISignedDocumentStore _signedDocumentStore;
    private readonly WorkspaceExchangeSnapshotBuilder _snapshotBuilder;

    public FileSystemSyncManifestStore(
        ISignedDocumentStore signedDocumentStore,
        WorkspaceExchangeSnapshotBuilder snapshotBuilder)
    {
        _signedDocumentStore = signedDocumentStore;
        _snapshotBuilder = snapshotBuilder;
    }

    public SignedDocumentReadResult<SyncManifestDocument> Read(
        string sharedProjectRoot,
        SignaturePublicKey publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedProjectRoot);

        return _signedDocumentStore.Read<SyncManifestDocument>(
            GetManifestPath(sharedProjectRoot),
            publicKey);
    }

    public SignedDocumentWriteResult Write(
        string sharedProjectRoot,
        Guid projectId,
        int manifestVersion,
        string batchId,
        SignatureKeyMaterial signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);

        var document = new SyncManifestDocument(
            CurrentSchemaVersion,
            projectId,
            manifestVersion,
            batchId,
            DateTimeOffset.UtcNow,
            _snapshotBuilder.Build(sharedProjectRoot));

        return _signedDocumentStore.Write(GetManifestPath(sharedProjectRoot), document, signingKey);
    }

    private static string GetManifestPath(string sharedProjectRoot) =>
        Path.Combine(sharedProjectRoot, "manifest", "sync-manifest.json");
}
