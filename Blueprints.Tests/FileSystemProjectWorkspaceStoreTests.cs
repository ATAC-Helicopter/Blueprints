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

    [Fact]
    public void SaveAndLoad_RoundTripsOptionalSignedCanvasLayout()
    {
        Directory.CreateDirectory(_workspaceRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("workspace-admin");
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var workspace = CreateWorkspaceSnapshot();
        var projectId = workspace.Project.ProjectId;
        var layout = new CanvasLayoutDocument(
            1,
            projectId,
            3,
            [new CanvasNodePosition("project", projectId, 80, 320)],
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "Layout Author");

        workspaceStore.Save(
            _workspaceRoot,
            workspace with { CanvasLayout = layout },
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var result = workspaceStore.Load(
            _workspaceRoot,
            new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes));

        Assert.Equal(TrustState.Trusted, result.TrustReport.State);
        var loadedLayout = Assert.IsType<CanvasLayoutDocument>(result.Workspace.CanvasLayout);
        Assert.Equal(layout.SchemaVersion, loadedLayout.SchemaVersion);
        Assert.Equal(layout.ProjectId, loadedLayout.ProjectId);
        Assert.Equal(layout.Revision, loadedLayout.Revision);
        Assert.Equal(layout.Nodes, loadedLayout.Nodes);
        Assert.Equal(layout.UpdatedUtc, loadedLayout.UpdatedUtc);
        Assert.Equal(layout.LastModifiedByUserId, loadedLayout.LastModifiedByUserId);
        Assert.Equal(layout.LastModifiedByName, loadedLayout.LastModifiedByName);
        Assert.True(File.Exists(Path.Combine(_workspaceRoot, "project", "canvas-layout.sig")));
    }

    [Fact]
    public void Load_ReturnsUntrusted_WhenCanvasLayoutSignatureIsTampered()
    {
        Directory.CreateDirectory(_workspaceRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("workspace-admin");
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var workspace = CreateWorkspaceSnapshot();
        var projectId = workspace.Project.ProjectId;

        workspaceStore.Save(
            _workspaceRoot,
            workspace with
            {
                CanvasLayout = new CanvasLayoutDocument(
                    1,
                    projectId,
                    1,
                    [new CanvasNodePosition("project", projectId, 80, 320)],
                    DateTimeOffset.UtcNow,
                    Guid.NewGuid(),
                    "Layout Author"),
            },
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        File.AppendAllText(Path.Combine(_workspaceRoot, "project", "canvas-layout.json"), " ");

        var result = workspaceStore.Load(
            _workspaceRoot,
            new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes));

        Assert.Equal(TrustState.Untrusted, result.TrustReport.State);
    }

    [Fact]
    public void Load_ReturnsCorrupt_WhenValidlySignedCanvasLayoutReferencesUnknownEntity()
    {
        Directory.CreateDirectory(_workspaceRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("workspace-admin");
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var workspace = CreateWorkspaceSnapshot();
        var projectId = workspace.Project.ProjectId;

        workspaceStore.Save(
            _workspaceRoot,
            workspace with
            {
                CanvasLayout = new CanvasLayoutDocument(
                    1,
                    projectId,
                    1,
                    [new CanvasNodePosition("item", Guid.NewGuid(), 100, 100)],
                    DateTimeOffset.UtcNow,
                    Guid.NewGuid(),
                    "Layout Author"),
            },
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var result = workspaceStore.Load(
            _workspaceRoot,
            new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes));

        Assert.Equal(TrustState.Corrupt, result.TrustReport.State);
        Assert.Contains("does not reference", result.TrustReport.Summary, StringComparison.OrdinalIgnoreCase);
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
