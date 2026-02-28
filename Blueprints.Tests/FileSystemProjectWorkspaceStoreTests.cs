using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class FileSystemProjectWorkspaceStoreTests : IDisposable
{
    private readonly string _workspaceRoot = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "WorkspaceStore",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsWorkspaceWithTrustedState()
    {
        Directory.CreateDirectory(_workspaceRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("workspace-admin");
        var serializer = new CanonicalJsonSerializer();
        var signatureService = new Ed25519SignatureService();
        var signedStore = new FileSystemSignedDocumentStore(serializer, signatureService);
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);

        var workspace = CreateWorkspaceSnapshot();

        workspaceStore.Save(
            _workspaceRoot,
            workspace,
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var result = workspaceStore.Load(
            _workspaceRoot,
            new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes));

        Assert.Equal(TrustState.Trusted, result.TrustReport.State);
        Assert.Equal(workspace.Project.ProjectCode, result.Workspace.Project.ProjectCode);
        Assert.Single(result.Workspace.Versions);
        Assert.Single(result.Workspace.Versions[0].Items);
    }

    [Fact]
    public void Load_ReturnsUntrusted_WhenSignedDocumentIsTampered()
    {
        Directory.CreateDirectory(_workspaceRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("workspace-admin");
        var serializer = new CanonicalJsonSerializer();
        var signatureService = new Ed25519SignatureService();
        var signedStore = new FileSystemSignedDocumentStore(serializer, signatureService);
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);

        var workspace = CreateWorkspaceSnapshot();

        workspaceStore.Save(
            _workspaceRoot,
            workspace,
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var projectPath = Path.Combine(_workspaceRoot, "project", "project.json");
        File.AppendAllText(projectPath, " ");

        var result = workspaceStore.Load(
            _workspaceRoot,
            new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes));

        Assert.Equal(TrustState.Untrusted, result.TrustReport.State);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }

    private static Storage.Models.ProjectWorkspaceSnapshot CreateWorkspaceSnapshot() =>
        TestWorkspaceFactory.CreateWorkspaceSnapshot();
}
