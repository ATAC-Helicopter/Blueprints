using System.Runtime.Versioning;
using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.Core.Enums;
using Blueprints.Security.Services;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

[SupportedOSPlatform("windows")]
public sealed class ProjectWorkspaceCoordinatorServiceTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "ProjectWorkspaceCoordinator",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateProject_CreatesConfiguredWorkspaceAndRecordsRecentProject()
    {
        var localRoot = Path.Combine(_rootDirectory, "local", "AP");
        var sharedRoot = Path.Combine(_rootDirectory, "shared", "AP");
        var service = CreateService();

        var session = service.CreateProject(
            new ProjectCreateRequest(
                "Atlas Planner",
                "AP",
                "SemVer",
                localRoot,
                sharedRoot));

        Assert.Equal("Atlas Planner", session.LoadResult.Workspace.Project.Name);
        Assert.Equal("AP", session.LoadResult.Workspace.Project.ProjectCode);
        Assert.Equal(localRoot, session.Paths.LocalWorkspaceRoot);
        Assert.Equal(sharedRoot, session.Paths.SharedProjectRoot);
        Assert.Empty(session.LoadResult.Workspace.Versions);

        var recent = service.GetRecentProjects();
        Assert.Contains(recent, static project => project.Name == "Atlas Planner" && project.ProjectCode == "AP");
    }

    [Fact]
    public void OpenProject_LoadsExistingWorkspaceAndRefreshesRecentProject()
    {
        var localRoot = Path.Combine(_rootDirectory, "existing-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "existing-shared", "BP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var opened = service.OpenProject(localRoot, sharedRoot);

        Assert.Equal("Blueprints", opened.LoadResult.Workspace.Project.Name);
        Assert.Equal(localRoot, opened.Paths.LocalWorkspaceRoot);
        Assert.Equal(sharedRoot, opened.Paths.SharedProjectRoot);
    }

    [Fact]
    public void SaveVersion_CreatesVersionInProjectWorkspace()
    {
        var localRoot = Path.Combine(_rootDirectory, "version-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "version-shared", "BP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var updated = service.SaveVersion(
            localRoot,
            sharedRoot,
            new VersionEditRequest(
                null,
                "1.1.0",
                ReleaseStatus.InProgress,
                "First active milestone"));

        Assert.Single(updated.LoadResult.Workspace.Versions);
        Assert.Equal("1.1.0", updated.LoadResult.Workspace.Versions[0].Version.Name);
    }

    [Fact]
    public void SaveItem_CreatesItemAndGeneratesExpectedKey()
    {
        var localRoot = Path.Combine(_rootDirectory, "item-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "item-shared", "BP");
        var service = CreateService();

        var created = service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var versionSession = service.SaveVersion(
            localRoot,
            sharedRoot,
            new VersionEditRequest(
                null,
                "1.5.0",
                ReleaseStatus.InProgress,
                null));
        var versionId = versionSession.LoadResult.Workspace.Versions[0].Version.VersionId;

        var updated = service.SaveItem(
            localRoot,
            sharedRoot,
            new ItemEditRequest(
                versionId,
                null,
                "feature",
                "added",
                "Ship create and open workflow",
                "Adds the project bootstrap UI.",
                false));

        var item = updated.LoadResult.Workspace.Versions[0].Items.Single();
        Assert.Equal("BP-151", item.ItemKey);
        Assert.Equal("Ship create and open workflow", item.Title);
    }

    [Fact]
    public void ReleaseVersion_MarksVersionReleasedAndBlocksFurtherEdits()
    {
        var localRoot = Path.Combine(_rootDirectory, "release-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "release-shared", "BP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var versionSession = service.SaveVersion(
            localRoot,
            sharedRoot,
            new VersionEditRequest(
                null,
                "1.2.0",
                ReleaseStatus.Frozen,
                "Ready to ship"));
        var versionId = versionSession.LoadResult.Workspace.Versions[0].Version.VersionId;

        var released = service.ReleaseVersion(localRoot, sharedRoot, versionId);
        var releasedVersion = released.LoadResult.Workspace.Versions.Single().Version;

        Assert.Equal(ReleaseStatus.Released, releasedVersion.Status);
        Assert.NotNull(releasedVersion.ReleasedUtc);

        var versionException = Assert.Throws<InvalidOperationException>(
            () => service.SaveVersion(
                localRoot,
                sharedRoot,
                new VersionEditRequest(
                    versionId,
                    "1.2.1",
                    ReleaseStatus.InProgress,
                    "Should fail")));
        Assert.Contains("immutable", versionException.Message, StringComparison.OrdinalIgnoreCase);

        var itemException = Assert.Throws<InvalidOperationException>(
            () => service.SaveItem(
                localRoot,
                sharedRoot,
                new ItemEditRequest(
                    versionId,
                    null,
                    "feature",
                    "added",
                    "Late feature",
                    null,
                    false)));
        Assert.Contains("immutable", itemException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportVersionChangelog_WritesMarkdownAndExcludesIncompleteItemsByDefault()
    {
        var localRoot = Path.Combine(_rootDirectory, "changelog-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "changelog-shared", "BP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var versionSession = service.SaveVersion(
            localRoot,
            sharedRoot,
            new VersionEditRequest(
                null,
                "1.5.0",
                ReleaseStatus.InProgress,
                "Release candidate"));
        var versionId = versionSession.LoadResult.Workspace.Versions[0].Version.VersionId;

        service.SaveItem(
            localRoot,
            sharedRoot,
            new ItemEditRequest(
                versionId,
                null,
                "feature",
                "added",
                "Ship project workflow",
                "Create and open real workspaces.",
                true));

        service.SaveItem(
            localRoot,
            sharedRoot,
            new ItemEditRequest(
                versionId,
                null,
                "bug",
                "fixed",
                "Deferred bugfix",
                "Still in progress.",
                false));

        var export = service.ExportVersionChangelog(localRoot, sharedRoot, versionId);

        Assert.True(File.Exists(export.FilePath));
        Assert.Contains("# Blueprints 1.5.0", export.Markdown, StringComparison.Ordinal);
        Assert.Contains("## Added", export.Markdown, StringComparison.Ordinal);
        Assert.Contains("`BP-151` Ship project workflow", export.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Deferred bugfix", export.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void InviteMember_AddsSignedMemberAndIncrementsRevision()
    {
        var localRoot = Path.Combine(_rootDirectory, "member-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "member-shared", "BP");
        var service = CreateService();

        var created = service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var invited = service.InviteMember(
            localRoot,
            sharedRoot,
            new MemberInviteRequest(
                Guid.NewGuid().ToString(),
                "Editor One",
                Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                Blueprints.Core.Enums.MemberRole.Editor));

        Assert.Equal(created.LoadResult.Workspace.Members.MembershipRevision + 1, invited.LoadResult.Workspace.Members.MembershipRevision);
        Assert.Contains(invited.LoadResult.Workspace.Members.Members, static member => member.DisplayName == "Editor One" && member.Role == Blueprints.Core.Enums.MemberRole.Editor);
    }

    [Fact]
    public void UpdateMember_CanDeactivateSecondaryAdminButRejectsRemovingLastAdmin()
    {
        var localRoot = Path.Combine(_rootDirectory, "member-update-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "member-update-shared", "BP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var secondAdminId = Guid.NewGuid();
        var invited = service.InviteMember(
            localRoot,
            sharedRoot,
            new MemberInviteRequest(
                secondAdminId.ToString(),
                "Admin Two",
                Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                Blueprints.Core.Enums.MemberRole.Admin));

        var deactivated = service.UpdateMember(
            localRoot,
            sharedRoot,
            new MemberUpdateRequest(
                secondAdminId,
                "Admin Two",
                Blueprints.Core.Enums.MemberRole.Admin,
                false));

        var updatedMember = deactivated.LoadResult.Workspace.Members.Members.Single(member => member.UserId == secondAdminId);
        Assert.False(updatedMember.IsActive);

        var currentAdminId = invited.Identity.Profile.UserId;
        var exception = Assert.Throws<InvalidOperationException>(
            () => service.UpdateMember(
                localRoot,
                sharedRoot,
                new MemberUpdateRequest(
                    currentAdminId,
                    invited.Identity.Profile.DisplayName,
                    Blueprints.Core.Enums.MemberRole.Editor,
                    true)));
        Assert.Contains("active admin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private ProjectWorkspaceCoordinatorService CreateService()
    {
        var identityRoot = Path.Combine(_rootDirectory, "identities");
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var identityService = new IdentityService(
            identityRoot,
            new FileSystemIdentityStore(
                identityRoot,
                new Ed25519KeyPairGenerator(),
                new DpapiPrivateKeyProtector()));
        var snapshotBuilder = new Blueprints.Collaboration.Services.WorkspaceExchangeSnapshotBuilder();

        return new ProjectWorkspaceCoordinatorService(
            identityService,
            workspaceStore,
            new Blueprints.Collaboration.Services.FileSystemSyncStateStore(),
            new Blueprints.Collaboration.Services.WorkspaceSyncAnalyzer(snapshotBuilder),
            new RecentProjectsStore(Path.Combine(_rootDirectory, "recent-projects.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
