using Blueprints.App.Services;
using Blueprints.Collaboration.Enums;
using Blueprints.Collaboration.Services;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class LocalWorkspaceSessionServiceTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "LocalWorkspaceSessionService",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetOrCreateDefaultSession_ComputesSyncSummaryAgainstSharedRoot()
    {
        var localRoot = Path.Combine(_rootDirectory, "local");
        var sharedRoot = Path.Combine(_rootDirectory, "shared");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(sharedRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("workspace-admin");
        var identity = new StoredIdentity(
            new IdentityProfile(
                Guid.NewGuid(),
                "Flavio",
                keyPair.KeyId,
                Convert.ToBase64String(keyPair.PublicKeyBytes),
                "Test Provider",
                DateTimeOffset.UtcNow),
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes),
            new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes));

        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var workspaceService = new LocalWorkspaceService(localRoot, workspaceStore);
        var sessionService = new LocalWorkspaceSessionService(
            workspaceService,
            new FileSystemSyncStateStore(),
            new WorkspaceSyncAnalyzer(new WorkspaceExchangeSnapshotBuilder()));

        var session = sessionService.GetOrCreateDefaultSession(identity, sharedRoot);

        Assert.Equal(localRoot, session.Paths.LocalWorkspaceRoot);
        Assert.Equal(sharedRoot, session.Paths.SharedProjectRoot);
        Assert.Equal(SyncHealth.Ready, session.Sync.Health);
        Assert.True(session.Sync.PendingOutgoingChanges > 0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
