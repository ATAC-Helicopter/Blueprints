using System.Text.Json;
using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.Collaboration.Services;
using Blueprints.Core.Enums;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class DistinctIdentityCollaborationTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "DistinctIdentityCollaboration",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SignedInvitations_EnableTwoDistinctIdentitiesToExchangeChanges()
    {
        var alice = CreateCoordinator("alice");
        var bob = CreateCoordinator("bob");
        var aliceLocal = Path.Combine(_rootDirectory, "workspaces", "alice", "BP");
        var bobLocal = Path.Combine(_rootDirectory, "workspaces", "bob", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "shared", "BP");
        var identityRequestPath = Path.Combine(_rootDirectory, "invites", "bob.identity.json");
        var projectInvitationPath = Path.Combine(_rootDirectory, "invites", "BP.project.json");

        var created = alice.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                aliceLocal,
                sharedRoot));
        bob.ExportIdentityInvitation(identityRequestPath);
        var bobRequest = alice.ReadIdentityInvitation(identityRequestPath);
        var membership = alice.InviteMember(
            aliceLocal,
            sharedRoot,
            bobRequest with { Role = MemberRole.Editor });
        var bobMember = Assert.Single(
            membership.LoadResult.Workspace.Members.Members,
            member => member.UserId.ToString() == bobRequest.UserId);

        var initialPush = alice.PushWorkspace(aliceLocal, sharedRoot);
        alice.ExportProjectInvitation(
            aliceLocal,
            sharedRoot,
            bobMember.UserId,
            projectInvitationPath);
        var joined = bob.JoinProjectFromInvitation(
            projectInvitationPath,
            bobLocal);

        Assert.True(initialPush.Success);
        Assert.Equal(created.LoadResult.Workspace.Project.ProjectId, joined.LoadResult.Workspace.Project.ProjectId);
        Assert.Contains(
            joined.LoadResult.Workspace.Members.Members,
            member => member.UserId == joined.Identity.Profile.UserId);

        bob.SaveVersion(
            bobLocal,
            sharedRoot,
            new VersionEditRequest(
                null,
                "0.3.0",
                ReleaseStatus.InProgress,
                "Distinct identity collaboration"));
        var bobPush = bob.PushWorkspace(bobLocal, sharedRoot);
        var alicePull = alice.PullWorkspace(aliceLocal, sharedRoot);
        var refreshedAlice = alice.OpenProject(aliceLocal, sharedRoot);

        Assert.True(bobPush.Success);
        Assert.True(alicePull.Success);
        Assert.Contains(
            refreshedAlice.LoadResult.Workspace.Versions,
            static version => version.Version.Name == "0.3.0");
        Assert.Equal(TrustState.Trusted, refreshedAlice.LoadResult.TrustReport.State);

        var bobVersionDirectory = Directory.EnumerateDirectories(
            Path.Combine(bobLocal, "versions"))
            .Single(directory =>
                File.ReadAllText(Path.Combine(directory, "version.json"))
                    .Contains("\"name\":\"0.3.0\"", StringComparison.Ordinal));
        var signature = JsonSerializer.Deserialize<DetachedSignature>(
            File.ReadAllText(Path.Combine(bobVersionDirectory, "version.sig")),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(signature);
        Assert.Equal(joined.Identity.Profile.KeyId, signature.KeyId);
    }

    [Fact]
    public void ProjectInvitation_RejectsTheWrongLocalIdentityBeforeCreatingAWorkspace()
    {
        var alice = CreateCoordinator("target-alice");
        var bob = CreateCoordinator("target-bob");
        var charlie = CreateCoordinator("target-charlie");
        var aliceLocal = Path.Combine(_rootDirectory, "target-workspaces", "alice", "BP");
        var charlieLocal = Path.Combine(_rootDirectory, "target-workspaces", "charlie", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "target-shared", "BP");
        var identityRequestPath = Path.Combine(_rootDirectory, "target-invites", "bob.identity.json");
        var projectInvitationPath = Path.Combine(_rootDirectory, "target-invites", "BP.project.json");

        alice.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                aliceLocal,
                sharedRoot));
        bob.ExportIdentityInvitation(identityRequestPath);
        var request = alice.ReadIdentityInvitation(identityRequestPath);
        var membership = alice.InviteMember(aliceLocal, sharedRoot, request);
        var bobMember = Assert.Single(
            membership.LoadResult.Workspace.Members.Members,
            member => member.UserId.ToString() == request.UserId);
        alice.PushWorkspace(aliceLocal, sharedRoot);
        alice.ExportProjectInvitation(
            aliceLocal,
            sharedRoot,
            bobMember.UserId,
            projectInvitationPath);

        var exception = Assert.Throws<InvalidOperationException>(
            () => charlie.JoinProjectFromInvitation(
                projectInvitationPath,
                charlieLocal));

        Assert.Contains("different local identity", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(charlieLocal));
    }

    private ProjectWorkspaceCoordinatorService CreateCoordinator(string name)
    {
        var identityRoot = Path.Combine(_rootDirectory, "identities", name);
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var identityService = new IdentityService(
            identityRoot,
            new FileSystemIdentityStore(
                identityRoot,
                new Ed25519KeyPairGenerator(),
                new TestPrivateKeyProtector()));
        var snapshotBuilder = new WorkspaceExchangeSnapshotBuilder();
        var syncStateStore = new FileSystemSyncStateStore();
        var syncAnalyzer = new WorkspaceSyncAnalyzer(snapshotBuilder);
        var auditLog = new FileSystemAuditLogService(signedStore);
        var syncService = new FileSystemWorkspaceSyncService(
            snapshotBuilder,
            syncAnalyzer,
            new FileSystemSyncManifestStore(signedStore, snapshotBuilder),
            syncStateStore,
            new WorkspaceExchangeValidator(new Ed25519SignatureService()),
            auditLog);

        return new ProjectWorkspaceCoordinatorService(
            identityService,
            workspaceStore,
            syncStateStore,
            syncAnalyzer,
            syncService,
            new RecentProjectsStore(
                Path.Combine(_rootDirectory, "recent", $"{name}.json")),
            auditLog,
            new SharedFolderSafetyInspector());
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private sealed class TestPrivateKeyProtector : IPrivateKeyProtector
    {
        public string ProviderName => "Test";

        public byte[] Protect(ReadOnlySpan<byte> privateKeyBytes) =>
            privateKeyBytes.ToArray();

        public byte[] Unprotect(ReadOnlySpan<byte> protectedBytes) =>
            protectedBytes.ToArray();
    }
}
