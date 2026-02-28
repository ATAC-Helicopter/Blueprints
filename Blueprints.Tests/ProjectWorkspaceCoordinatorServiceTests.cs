using System.Runtime.Versioning;
using Blueprints.App.Models;
using Blueprints.App.Services;
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
