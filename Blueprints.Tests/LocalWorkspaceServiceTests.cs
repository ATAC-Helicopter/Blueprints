using Blueprints.App.Services;
using Blueprints.Core.Enums;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class LocalWorkspaceServiceTests : IDisposable
{
    private readonly string _workspaceRoot = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "LocalWorkspaceService",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetOrCreateDefaultWorkspace_CreatesTrustedStarterWorkspace()
    {
        Directory.CreateDirectory(_workspaceRoot);

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
        var service = new LocalWorkspaceService(_workspaceRoot, workspaceStore);

        var session = service.GetOrCreateDefaultWorkspace(identity);

        Assert.Equal(TrustState.Trusted, session.LoadResult.TrustReport.State);
        Assert.Equal("Blueprints", session.LoadResult.Workspace.Project.Name);
        Assert.Equal("BP", session.LoadResult.Workspace.Project.ProjectCode);
        Assert.Single(session.LoadResult.Workspace.Members.Members);
        Assert.Single(session.LoadResult.Workspace.Versions);
        Assert.Equal(3, session.LoadResult.Workspace.Versions[0].Items.Count);
    }

    [Fact]
    public void GetOrCreateDefaultWorkspace_ReusesExistingWorkspace()
    {
        Directory.CreateDirectory(_workspaceRoot);

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
        var service = new LocalWorkspaceService(_workspaceRoot, workspaceStore);

        var first = service.GetOrCreateDefaultWorkspace(identity);
        var second = service.GetOrCreateDefaultWorkspace(identity);

        Assert.Equal(first.LoadResult.Workspace.Project.ProjectId, second.LoadResult.Workspace.Project.ProjectId);
        Assert.Equal(first.LoadResult.Workspace.Versions[0].Version.VersionId, second.LoadResult.Workspace.Versions[0].Version.VersionId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }
}
