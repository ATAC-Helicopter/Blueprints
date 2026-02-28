using Blueprints.Collaboration.Services;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Models;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class FileSystemSyncManifestStoreTests : IDisposable
{
    private readonly string _sharedRoot = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "SyncManifest",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriteAndRead_RoundTripsSignedManifest()
    {
        Directory.CreateDirectory(_sharedRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("sync-admin");
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var manifestStore = new FileSystemSyncManifestStore(signedStore, new WorkspaceExchangeSnapshotBuilder());
        var workspace = TestWorkspaceFactory.CreateWorkspaceSnapshot();

        workspaceStore.Save(
            _sharedRoot,
            workspace,
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        manifestStore.Write(
            _sharedRoot,
            workspace.Project.ProjectId,
            manifestVersion: 1,
            batchId: "batch-0001",
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var result = manifestStore.Read(
            _sharedRoot,
            new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes));

        Assert.True(result.IsSignatureValid);
        Assert.Equal(1, result.Document.ManifestVersion);
        Assert.Equal("batch-0001", result.Document.BatchId);
        Assert.NotEmpty(result.Document.Entries);
        Assert.Contains(result.Document.Entries, static entry => entry.DocumentPath == "project/project.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_sharedRoot))
        {
            Directory.Delete(_sharedRoot, recursive: true);
        }
    }
}
