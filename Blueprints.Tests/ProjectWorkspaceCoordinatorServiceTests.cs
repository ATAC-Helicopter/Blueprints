using System.Runtime.Versioning;
using System.Text.Json;
using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.Collaboration.Models;
using Blueprints.Core.Enums;
using Blueprints.Security.Abstractions;
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
    public void CreateProject_WritesSignedAuditEntry()
    {
        var localRoot = Path.Combine(_rootDirectory, "audit-create-local", "AP");
        var sharedRoot = Path.Combine(_rootDirectory, "audit-create-shared", "AP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Atlas Planner",
                "AP",
                "SemVer",
                localRoot,
                sharedRoot));

        var auditEntries = Directory.EnumerateFiles(Path.Combine(localRoot, "log"), "*.json").ToArray();
        Assert.Single(auditEntries);
        Assert.True(File.Exists(Path.ChangeExtension(auditEntries.Single(), ".sig")));
    }

    [Fact]
    public void CreateProject_BlocksSharedFolderInsideLocalWorkspace()
    {
        var localRoot = Path.Combine(_rootDirectory, "unsafe-local", "BP");
        var sharedRoot = Path.Combine(localRoot, "shared");
        var service = CreateService();

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.CreateProject(
                new ProjectCreateRequest(
                    "Blueprints",
                    "BP",
                    "SemVer",
                    localRoot,
                    sharedRoot)));

        Assert.Contains("shared sync folder", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenProject_MarksWorkspaceCorruptWhenAuditChainIsBroken()
    {
        var localRoot = Path.Combine(_rootDirectory, "audit-tamper-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "audit-tamper-shared", "BP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));
        service.SaveVersion(
            localRoot,
            sharedRoot,
            new VersionEditRequest(
                null,
                "1.0.0",
                ReleaseStatus.InProgress,
                "Baseline"));

        File.Delete(FindGenesisAuditEntry(Path.Combine(localRoot, "log")));

        var opened = service.OpenProject(localRoot, sharedRoot);

        Assert.Equal(TrustState.Corrupt, opened.LoadResult.TrustReport.State);
        Assert.Contains("audit log", opened.LoadResult.TrustReport.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.False(opened.AuditLogValidation.IsValid);
        Assert.NotEmpty(opened.AuditLogValidation.InvalidEntryPaths);
        Assert.NotNull(opened.SharedFolderSafety);
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
    public void PushWorkspace_PublishesLocalChangesAndRefreshesSyncState()
    {
        var localRoot = Path.Combine(_rootDirectory, "push-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "push-shared", "BP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));
        service.SaveVersion(
            localRoot,
            sharedRoot,
            new VersionEditRequest(
                null,
                "1.0.0",
                ReleaseStatus.InProgress,
                "Baseline"));

        var beforePush = service.OpenProject(localRoot, sharedRoot);
        Assert.True(beforePush.Sync.PendingOutgoingChanges > 0);

        var result = service.PushWorkspace(localRoot, sharedRoot);

        Assert.True(result.Success);
        Assert.Equal("push", result.Operation);
        Assert.True(result.AppliedDocumentCount > 0);
        Assert.True(File.Exists(Path.Combine(sharedRoot, "manifest", "sync-manifest.json")));
        Assert.True(File.Exists(Path.Combine(sharedRoot, "project", "project.json")));

        var refreshed = service.OpenProject(localRoot, sharedRoot);
        Assert.Equal(0, refreshed.Sync.PendingOutgoingChanges);
        Assert.Equal(0, refreshed.Sync.PendingIncomingChanges);
    }

    [Fact]
    public void PullWorkspace_ImportsIncomingSharedChangesAndRefreshesSyncState()
    {
        var localRoot = Path.Combine(_rootDirectory, "pull-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "pull-shared", "BP");
        var peerRoot = Path.Combine(_rootDirectory, "pull-peer", "BP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));
        service.PushWorkspace(localRoot, sharedRoot);
        CopyDirectory(localRoot, peerRoot);

        service.SaveVersion(
            peerRoot,
            sharedRoot,
            new VersionEditRequest(
                null,
                "1.1.0",
                ReleaseStatus.InProgress,
                "Peer milestone"));
        var pushResult = service.PushWorkspace(peerRoot, sharedRoot);
        Assert.True(pushResult.Success);

        var beforePull = service.OpenProject(localRoot, sharedRoot);
        Assert.True(beforePull.Sync.PendingIncomingChanges > 0);

        var pullResult = service.PullWorkspace(localRoot, sharedRoot);

        Assert.True(pullResult.Success);
        Assert.Equal("pull", pullResult.Operation);
        Assert.True(pullResult.AppliedDocumentCount > 0);

        var refreshed = service.OpenProject(localRoot, sharedRoot);
        Assert.Contains(refreshed.LoadResult.Workspace.Versions, static version => version.Version.Name == "1.1.0");
        Assert.Equal(0, refreshed.Sync.PendingIncomingChanges);
        Assert.Equal(0, refreshed.Sync.PendingOutgoingChanges);
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

    [Fact]
    public void SaveVersion_BlocksMutationWhenWorkspaceTrustIsBroken()
    {
        var localRoot = Path.Combine(_rootDirectory, "trust-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "trust-shared", "BP");
        var service = CreateService();

        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var projectPath = Path.Combine(localRoot, "project", "project.json");
        var projectJson = File.ReadAllText(projectPath);
        using var document = JsonDocument.Parse(projectJson);
        var tamperedJson = document.RootElement.GetRawText().Replace("\"Blueprints\"", "\"Tampered\"", StringComparison.Ordinal);
        File.WriteAllText(projectPath, tamperedJson);

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.SaveVersion(
                localRoot,
                sharedRoot,
                new VersionEditRequest(
                    null,
                    "1.0.0",
                    ReleaseStatus.InProgress,
                    null)));

        Assert.Contains("read-only", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveConflict_CanKeepLocalCopyAndClearConflict()
    {
        var localRoot = Path.Combine(_rootDirectory, "conflict-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "conflict-shared", "BP");
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
                "1.0.0",
                ReleaseStatus.InProgress,
                "Baseline"));
        var versionId = versionSession.LoadResult.Workspace.Versions.Single().Version.VersionId;

        var syncStateStore = new Blueprints.Collaboration.Services.FileSystemSyncStateStore();
        var snapshotBuilder = new Blueprints.Collaboration.Services.WorkspaceExchangeSnapshotBuilder();
        var state = syncStateStore.Load(localRoot);
        syncStateStore.Save(
            localRoot,
            state with
            {
                TrackedEntries = snapshotBuilder.Build(localRoot)
                    .Select(static entry => new SyncTrackedEntry(entry.DocumentPath, entry.DocumentHash, entry.SignatureHash))
                    .ToArray(),
            });

        service.SaveVersion(
            localRoot,
            sharedRoot,
            new VersionEditRequest(
                versionId,
                "1.0.0",
                ReleaseStatus.InProgress,
                "Local edit"));

        var versionDirectory = Directory.EnumerateDirectories(Path.Combine(localRoot, "versions")).Single();
        var localVersionPath = Path.Combine(versionDirectory, "version.json");
        var sharedVersionDirectory = Path.Combine(sharedRoot, "versions", Path.GetFileName(versionDirectory));
        Directory.CreateDirectory(sharedVersionDirectory);
        File.Copy(localVersionPath, Path.Combine(sharedVersionDirectory, "version.json"), overwrite: true);
        File.Copy(Path.ChangeExtension(localVersionPath, ".sig"), Path.Combine(sharedVersionDirectory, "version.sig"), overwrite: true);

        var sharedVersionPath = Path.Combine(sharedVersionDirectory, "version.json");
        var sharedJson = File.ReadAllText(sharedVersionPath).Replace("Local edit", "Shared edit", StringComparison.Ordinal);
        File.WriteAllText(sharedVersionPath, sharedJson);

        var opened = service.OpenProject(localRoot, sharedRoot);
        var conflictPath = opened.ConflictPaths.Single();
        Assert.Contains("version.json", conflictPath, StringComparison.Ordinal);

        var resolution = service.ResolveConflict(
            localRoot,
            sharedRoot,
            conflictPath,
            ConflictResolutionChoice.KeepLocal);

        Assert.Contains("Kept local", resolution.Summary, StringComparison.Ordinal);

        var refreshed = service.OpenProject(localRoot, sharedRoot);
        Assert.Empty(refreshed.ConflictPaths);
        Assert.Equal(TrustState.Trusted, refreshed.LoadResult.TrustReport.State);
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
                new TestPrivateKeyProtector()));
        var snapshotBuilder = new Blueprints.Collaboration.Services.WorkspaceExchangeSnapshotBuilder();
        var syncStateStore = new Blueprints.Collaboration.Services.FileSystemSyncStateStore();
        var syncAnalyzer = new Blueprints.Collaboration.Services.WorkspaceSyncAnalyzer(snapshotBuilder);
        var auditLogService = new Blueprints.Collaboration.Services.FileSystemAuditLogService(signedStore);
        var workspaceSyncService = new Blueprints.Collaboration.Services.FileSystemWorkspaceSyncService(
            snapshotBuilder,
            syncAnalyzer,
            new Blueprints.Collaboration.Services.FileSystemSyncManifestStore(signedStore, snapshotBuilder),
            syncStateStore,
            new Blueprints.Collaboration.Services.WorkspaceExchangeValidator(new Ed25519SignatureService()),
            auditLogService);

        return new ProjectWorkspaceCoordinatorService(
            identityService,
            workspaceStore,
            syncStateStore,
            syncAnalyzer,
            workspaceSyncService,
            new RecentProjectsStore(Path.Combine(_rootDirectory, "recent-projects.json")),
            auditLogService,
            new Blueprints.Collaboration.Services.SharedFolderSafetyInspector());
    }

    private static string FindGenesisAuditEntry(string logRoot) =>
        Directory.EnumerateFiles(logRoot, "*.json")
            .Single(path => File.ReadAllText(path).Contains("\"previousEntryHash\":null", StringComparison.Ordinal));

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var filePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var destinationPath = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, filePath));
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(filePath, destinationPath, overwrite: true);
        }
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

        public byte[] Protect(ReadOnlySpan<byte> privateKeyBytes) => privateKeyBytes.ToArray();

        public byte[] Unprotect(ReadOnlySpan<byte> protectedPrivateKeyBytes) => protectedPrivateKeyBytes.ToArray();
    }
}
