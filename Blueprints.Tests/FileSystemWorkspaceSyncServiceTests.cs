using Blueprints.Collaboration.Models;
using Blueprints.Collaboration.Services;
using Blueprints.Core.Models;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Models;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class FileSystemWorkspaceSyncServiceTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "WorkspaceSyncService",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Push_CopiesOutgoingDocumentsAndUpdatesManifestAndState()
    {
        var localRoot = Path.Combine(_rootDirectory, "local");
        var sharedRoot = Path.Combine(_rootDirectory, "shared");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(sharedRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("sync-admin");
        var signingKey = new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes);
        var publicKey = new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes);
        var signedStore = new FileSystemSignedDocumentStore(new CanonicalJsonSerializer(), new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var workspace = TestWorkspaceFactory.CreateWorkspaceSnapshot();
        workspaceStore.Save(localRoot, workspace, signingKey);

        var service = CreateService(signedStore);

        var result = service.Push(new WorkspacePaths(localRoot, sharedRoot), workspace.Project.ProjectId, signingKey, publicKey);
        var syncState = new FileSystemSyncStateStore().Load(localRoot);

        Assert.True(result.Success);
        Assert.Equal(1, result.ManifestVersion);
        Assert.True(File.Exists(Path.Combine(sharedRoot, "project", "project.json")));
        Assert.True(File.Exists(Path.Combine(sharedRoot, "manifest", "sync-manifest.json")));
        Assert.Equal(1, syncState.LastPushedManifestVersion);
        Assert.NotEmpty(syncState.TrackedEntries);
    }

    [Fact]
    public void Pull_CopiesIncomingDocumentsAndUpdatesState()
    {
        var localRoot = Path.Combine(_rootDirectory, "pull-local");
        var sharedRoot = Path.Combine(_rootDirectory, "pull-shared");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(sharedRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("sync-admin");
        var signingKey = new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes);
        var publicKey = new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes);
        var signedStore = new FileSystemSignedDocumentStore(new CanonicalJsonSerializer(), new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var workspace = TestWorkspaceFactory.CreateWorkspaceSnapshot();
        workspaceStore.Save(sharedRoot, workspace, signingKey);

        var manifestStore = new FileSystemSyncManifestStore(signedStore, new WorkspaceExchangeSnapshotBuilder());
        manifestStore.Write(sharedRoot, workspace.Project.ProjectId, 1, "batch-0001", signingKey);

        var service = CreateService(signedStore);
        var result = service.Pull(new WorkspacePaths(localRoot, sharedRoot), publicKey);
        var state = new FileSystemSyncStateStore().Load(localRoot);

        Assert.True(result.Success);
        Assert.Equal(1, result.ManifestVersion);
        Assert.True(File.Exists(Path.Combine(localRoot, "project", "project.json")));
        Assert.Equal(1, state.LastPulledManifestVersion);
        Assert.Contains("batch-0001", state.KnownRemoteBatchIds);
    }

    [Fact]
    public void Pull_BlocksWhenLocalAndSharedChangeSameDocument()
    {
        var localRoot = Path.Combine(_rootDirectory, "conflict-local");
        var sharedRoot = Path.Combine(_rootDirectory, "conflict-shared");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(sharedRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("sync-admin");
        var signingKey = new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes);
        var publicKey = new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes);
        var signedStore = new FileSystemSignedDocumentStore(new CanonicalJsonSerializer(), new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var workspace = TestWorkspaceFactory.CreateWorkspaceSnapshot();
        workspaceStore.Save(localRoot, workspace, signingKey);
        workspaceStore.Save(sharedRoot, workspace, signingKey);

        var baselineEntries = new WorkspaceExchangeSnapshotBuilder()
            .Build(sharedRoot)
            .Select(static entry => new SyncTrackedEntry(entry.DocumentPath, entry.DocumentHash, entry.SignatureHash))
            .ToArray();
        var stateStore = new FileSystemSyncStateStore();
        stateStore.Save(
            localRoot,
            stateStore.CreateDefault() with
            {
                LastPulledManifestVersion = 1,
                TrackedEntries = baselineEntries,
            });

        var version = workspace.Versions[0].Version;
        workspaceStore.Save(
            localRoot,
            new ProjectWorkspaceSnapshot(
                workspace.Project,
                workspace.Members,
                [new VersionWorkspaceSnapshot(version with { Notes = "local edit" }, workspace.Versions[0].Items)]),
            signingKey);
        workspaceStore.Save(
            sharedRoot,
            new ProjectWorkspaceSnapshot(
                workspace.Project,
                workspace.Members,
                [new VersionWorkspaceSnapshot(version with { Notes = "shared edit" }, workspace.Versions[0].Items)]),
            signingKey);

        var manifestStore = new FileSystemSyncManifestStore(signedStore, new WorkspaceExchangeSnapshotBuilder());
        manifestStore.Write(sharedRoot, workspace.Project.ProjectId, 2, "batch-0002", signingKey);

        var service = CreateService(signedStore);
        var result = service.Pull(new WorkspacePaths(localRoot, sharedRoot), publicKey);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Conflicts);
    }

    private static FileSystemWorkspaceSyncService CreateService(FileSystemSignedDocumentStore signedStore)
    {
        var builder = new WorkspaceExchangeSnapshotBuilder();
        var analyzer = new WorkspaceSyncAnalyzer(builder);
        var manifestStore = new FileSystemSyncManifestStore(signedStore, builder);
        var stateStore = new FileSystemSyncStateStore();
        return new FileSystemWorkspaceSyncService(builder, analyzer, manifestStore, stateStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
