using Blueprints.Collaboration.Services;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Models;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class TwoWorkspaceCollaborationTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "TwoWorkspaceCollaboration",
        Guid.NewGuid().ToString("N"));
    private readonly SignatureKeyMaterial _signingKey;
    private readonly SignaturePublicKey _publicKey;
    private readonly FileSystemProjectWorkspaceStore _workspaceStore;
    private readonly FileSystemAuditLogService _auditLog;
    private readonly FileSystemWorkspaceSyncService _syncService;

    public TwoWorkspaceCollaborationTests()
    {
        var keyPair = new Ed25519KeyPairGenerator().Generate("collaboration-admin");
        _signingKey = new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes);
        _publicKey = new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes);
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        _workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        _auditLog = new FileSystemAuditLogService(signedStore);

        var snapshotBuilder = new WorkspaceExchangeSnapshotBuilder();
        _syncService = new FileSystemWorkspaceSyncService(
            snapshotBuilder,
            new WorkspaceSyncAnalyzer(snapshotBuilder),
            new FileSystemSyncManifestStore(signedStore, snapshotBuilder),
            new FileSystemSyncStateStore(),
            new WorkspaceExchangeValidator(new Ed25519SignatureService()),
            _auditLog);
    }

    [Fact]
    public void TwoWorkspaces_CanRoundTripChangesAndRejectAnOverlappingEdit()
    {
        var aliceRoot = Path.Combine(_rootDirectory, "alice");
        var bobRoot = Path.Combine(_rootDirectory, "bob");
        var sharedRoot = Path.Combine(_rootDirectory, "shared");
        var workspace = TestWorkspaceFactory.CreateWorkspaceSnapshot();
        var member = Assert.Single(workspace.Members.Members);

        _workspaceStore.Save(aliceRoot, workspace, _signingKey);
        _auditLog.Append(
            aliceRoot,
            workspace.Project.ProjectId,
            "project.create",
            "Created collaboration test project.",
            member.UserId,
            member.DisplayName,
            workspace.Members.MembershipRevision,
            _signingKey);

        var alicePush = _syncService.Push(
            new WorkspacePaths(aliceRoot, sharedRoot),
            workspace.Project.ProjectId,
            _signingKey,
            _publicKey);
        var bobPull = _syncService.Pull(new WorkspacePaths(bobRoot, sharedRoot), _publicKey);

        Assert.True(alicePush.Success);
        Assert.True(bobPull.Success);
        Assert.Equal("1.0.0", LoadWorkspace(bobRoot).Versions.Single().Version.Name);

        SaveVersionNotes(bobRoot, "Prepared by Bob", appendAuditEntry: true);
        var bobPush = _syncService.Push(
            new WorkspacePaths(bobRoot, sharedRoot),
            workspace.Project.ProjectId,
            _signingKey,
            _publicKey);
        var alicePull = _syncService.Pull(new WorkspacePaths(aliceRoot, sharedRoot), _publicKey);

        Assert.True(bobPush.Success);
        Assert.True(alicePull.Success);
        Assert.Equal("Prepared by Bob", LoadWorkspace(aliceRoot).Versions.Single().Version.Notes);

        SaveVersionNotes(aliceRoot, "Alice local edit", appendAuditEntry: false);
        SaveVersionNotes(bobRoot, "Bob shared edit", appendAuditEntry: false);
        var secondBobPush = _syncService.Push(
            new WorkspacePaths(bobRoot, sharedRoot),
            workspace.Project.ProjectId,
            _signingKey,
            _publicKey);
        var blockedAlicePull = _syncService.Pull(
            new WorkspacePaths(aliceRoot, sharedRoot),
            _publicKey);

        Assert.True(secondBobPush.Success);
        Assert.False(blockedAlicePull.Success);
        Assert.Contains(
            blockedAlicePull.Conflicts,
            static path => path.EndsWith("/version.json", StringComparison.Ordinal));
        Assert.Equal("Alice local edit", LoadWorkspace(aliceRoot).Versions.Single().Version.Notes);
    }

    private ProjectWorkspaceSnapshot LoadWorkspace(string workspaceRoot)
    {
        var result = _workspaceStore.Load(workspaceRoot, _publicKey);
        Assert.Equal(Blueprints.Core.Enums.TrustState.Trusted, result.TrustReport.State);
        return result.Workspace;
    }

    private void SaveVersionNotes(
        string workspaceRoot,
        string notes,
        bool appendAuditEntry)
    {
        var workspace = LoadWorkspace(workspaceRoot);
        var version = Assert.Single(workspace.Versions);
        var updated = workspace with
        {
            Versions =
            [
                version with
                {
                    Version = version.Version with { Notes = notes },
                },
            ],
        };
        _workspaceStore.Save(workspaceRoot, updated, _signingKey);

        if (!appendAuditEntry)
        {
            return;
        }

        var member = Assert.Single(workspace.Members.Members);
        _auditLog.Append(
            workspaceRoot,
            workspace.Project.ProjectId,
            "version.save",
            $"Changed version notes to {notes}.",
            member.UserId,
            member.DisplayName,
            workspace.Members.MembershipRevision,
            _signingKey);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
