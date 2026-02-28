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
