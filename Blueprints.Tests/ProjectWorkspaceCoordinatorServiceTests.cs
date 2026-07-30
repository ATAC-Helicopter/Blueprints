using System.Runtime.Versioning;
using System.Text.Json;
using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.Collaboration.Models;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;
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
    public void CreateInitialIdentity_RequiresAnExplicitNameAndCreatesItOnce()
    {
        var service = CreateService();

        Assert.False(service.HasLocalIdentity);
        var identity = service.CreateInitialIdentity("Flavio");

        Assert.True(service.HasLocalIdentity);
        Assert.Equal("Flavio", identity.DisplayName);
        Assert.Throws<InvalidOperationException>(
            () => service.CreateInitialIdentity("Replacement"));
    }

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
                "1.0.0",
                ReleaseStatus.InProgress,
                "Baseline"));
        service.SaveCanvasLayout(
            localRoot,
            sharedRoot,
            new CanvasLayoutEditRequest(
                [
                    new CanvasNodeLayoutEdit(
                        "project",
                        created.LoadResult.Workspace.Project.ProjectId,
                        50,
                        300),
                    new CanvasNodeLayoutEdit(
                        "version",
                        versionSession.LoadResult.Workspace.Versions.Single().Version.VersionId,
                        400,
                        100),
                ]));

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
    public void SaveCanvasLayout_PersistsSignedEntityPositionsAndAuditRevision()
    {
        var localRoot = Path.Combine(_rootDirectory, "canvas-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "canvas-shared", "BP");
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
            new VersionEditRequest(null, "1.0.0", ReleaseStatus.InProgress, null));
        var projectId = created.LoadResult.Workspace.Project.ProjectId;
        var versionId = versionSession.LoadResult.Workspace.Versions.Single().Version.VersionId;

        var updated = service.SaveCanvasLayout(
            localRoot,
            sharedRoot,
            new CanvasLayoutEditRequest(
                [
                    new CanvasNodeLayoutEdit("project", projectId, 50, 300),
                    new CanvasNodeLayoutEdit("version", versionId, 420, 90),
                ]));

        var layout = Assert.IsType<CanvasLayoutDocument>(updated.LoadResult.Workspace.CanvasLayout);
        Assert.Equal(1, layout.Revision);
        Assert.Equal(2, layout.Nodes.Count);
        Assert.True(File.Exists(Path.Combine(localRoot, "project", "canvas-layout.sig")));
        Assert.Contains(
            Directory.EnumerateFiles(Path.Combine(localRoot, "log"), "*.json"),
            path => File.ReadAllText(path).Contains("canvas.layout.save", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveCanvasLayout_RejectsReferencesOutsideTheWorkspace()
    {
        var localRoot = Path.Combine(_rootDirectory, "canvas-invalid-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "canvas-invalid-shared", "BP");
        var service = CreateService();
        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.SaveCanvasLayout(
                localRoot,
                sharedRoot,
                new CanvasLayoutEditRequest(
                    [new CanvasNodeLayoutEdit("version", Guid.NewGuid(), 100, 100)])));

        Assert.Contains("does not reference", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(localRoot, "project", "canvas-layout.json")));
    }

    [Fact]
    public void Relationships_AreTypedSignedAuditedAndRemovable()
    {
        var localRoot = Path.Combine(_rootDirectory, "relationships-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "relationships-shared", "BP");
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
            new VersionEditRequest(null, "0.4.0", ReleaseStatus.InProgress, null));
        var versionId = versionSession.LoadResult.Workspace.Versions.Single().Version.VersionId;

        var typeSession = service.SaveRelationshipType(
            localRoot,
            sharedRoot,
            new RelationshipTypeEditRequest(
                "blocks",
                "Blocks",
                "Must complete first",
                "#E05A47",
                true));
        var edgeSession = service.SaveRelationship(
            localRoot,
            sharedRoot,
            new RelationshipEditRequest(
                null,
                "blocks",
                new RelationshipEndpoint("project", created.LoadResult.Workspace.Project.ProjectId),
                new RelationshipEndpoint("version", versionId),
                "Release gate"));

        var document = Assert.IsType<RelationshipDocument>(
            edgeSession.LoadResult.Workspace.Relationships);
        Assert.Equal(2, document.Revision);
        Assert.Single(document.Types);
        var edge = Assert.Single(document.Relationships);
        Assert.True(File.Exists(Path.Combine(localRoot, "project", "relationships.sig")));
        Assert.Contains(
            Directory.EnumerateFiles(Path.Combine(localRoot, "log"), "*.json"),
            path => File.ReadAllText(path).Contains("relationship.create", StringComparison.Ordinal));

        var removed = service.RemoveRelationship(
            localRoot,
            sharedRoot,
            edge.RelationshipId);

        Assert.Empty(removed.LoadResult.Workspace.Relationships!.Relationships);
        Assert.Equal(3, removed.LoadResult.Workspace.Relationships.Revision);
        Assert.NotNull(typeSession.LoadResult.Workspace.Relationships);
    }

    [Fact]
    public void ArchiveItem_RemovesRelationshipsThatReferenceIt()
    {
        var localRoot = Path.Combine(_rootDirectory, "relationship-archive-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "relationship-archive-shared", "BP");
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
            new VersionEditRequest(null, "0.4.0", ReleaseStatus.InProgress, null));
        var versionId = versionSession.LoadResult.Workspace.Versions.Single().Version.VersionId;
        var itemSession = service.SaveItem(
            localRoot,
            sharedRoot,
            new ItemEditRequest(
                versionId,
                null,
                "feature",
                "added",
                "Relationship target",
                null,
                false));
        var itemId = itemSession.LoadResult.Workspace.Versions.Single().Items.Single().ItemId;
        service.SaveRelationshipType(
            localRoot,
            sharedRoot,
            new RelationshipTypeEditRequest(
                "related",
                "Related",
                null,
                "#52C7E8",
                false));
        service.SaveRelationship(
            localRoot,
            sharedRoot,
            new RelationshipEditRequest(
                null,
                "related",
                new RelationshipEndpoint("project", created.LoadResult.Workspace.Project.ProjectId),
                new RelationshipEndpoint("item", itemId),
                null));

        var archived = service.ArchiveItem(localRoot, sharedRoot, versionId, itemId);

        Assert.Empty(archived.Session.LoadResult.Workspace.Relationships!.Relationships);
        Assert.Equal(3, archived.Session.LoadResult.Workspace.Relationships.Revision);
    }

    [Fact]
    public void PushWorkspace_PublishesLocalChangesAndRefreshesSyncState()
    {
        var localRoot = Path.Combine(_rootDirectory, "push-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "push-shared", "BP");
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
                "1.0.0",
                ReleaseStatus.InProgress,
                "Baseline"));
        service.SaveCanvasLayout(
            localRoot,
            sharedRoot,
            new CanvasLayoutEditRequest(
                [
                    new CanvasNodeLayoutEdit(
                        "project",
                        created.LoadResult.Workspace.Project.ProjectId,
                        50,
                        300),
                    new CanvasNodeLayoutEdit(
                        "version",
                        versionSession.LoadResult.Workspace.Versions.Single().Version.VersionId,
                        400,
                        100),
                ]));

        var beforePush = service.OpenProject(localRoot, sharedRoot);
        Assert.True(beforePush.Sync.PendingOutgoingChanges > 0);

        var result = service.PushWorkspace(localRoot, sharedRoot);

        Assert.True(result.Success);
        Assert.Equal("push", result.Operation);
        Assert.True(result.AppliedDocumentCount > 0);
        Assert.True(File.Exists(Path.Combine(sharedRoot, "manifest", "sync-manifest.json")));
        Assert.True(File.Exists(Path.Combine(sharedRoot, "project", "project.json")));
        Assert.True(File.Exists(Path.Combine(sharedRoot, "project", "canvas-layout.json")));
        Assert.True(File.Exists(Path.Combine(sharedRoot, "project", "canvas-layout.sig")));

        var refreshed = service.OpenProject(localRoot, sharedRoot);
        Assert.Equal(0, refreshed.Sync.PendingOutgoingChanges);
        Assert.Equal(0, refreshed.Sync.PendingIncomingChanges);
        Assert.Equal(1, refreshed.Sync.LastPushedManifestVersion);
        Assert.Equal(1, refreshed.Sync.SharedManifestVersion);
        Assert.True(refreshed.Sync.SharedManifestSignatureValid);
        Assert.NotNull(refreshed.Sync.LastSuccessfulTrustValidationUtc);
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
        Assert.Equal(2, refreshed.Sync.LastPulledManifestVersion);
        Assert.Equal(2, refreshed.Sync.SharedManifestVersion);
        Assert.True(refreshed.Sync.SharedManifestSignatureValid);
        Assert.NotNull(refreshed.Sync.LastSuccessfulTrustValidationUtc);
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
    public void ArchiveDrafts_RemovesThemFromThePlanAndKeepsRecoveryCopies()
    {
        var localRoot = Path.Combine(_rootDirectory, "archive-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "archive-shared", "BP");
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
                "0.3.0",
                ReleaseStatus.InProgress,
                "Archive coverage"));
        var version = Assert.Single(versionSession.LoadResult.Workspace.Versions);
        var itemSession = service.SaveItem(
            localRoot,
            sharedRoot,
            new ItemEditRequest(
                version.Version.VersionId,
                null,
                "feature",
                "added",
                "Recoverable archive",
                null,
                false));
        var item = Assert.Single(itemSession.LoadResult.Workspace.Versions.Single().Items);

        var itemArchive = service.ArchiveItem(
            localRoot,
            sharedRoot,
            version.Version.VersionId,
            item.ItemId);

        Assert.Empty(itemArchive.Session.LoadResult.Workspace.Versions.Single().Items);
        Assert.True(File.Exists(Path.Combine(itemArchive.ArchiveDirectory, "archive.json")));
        Assert.True(Directory.EnumerateFiles(
            itemArchive.ArchiveDirectory,
            $"{item.ItemId:N}.json",
            SearchOption.AllDirectories).Any());
        Assert.False(File.Exists(Path.Combine(
            localRoot,
            "versions",
            version.Version.VersionId.ToString("N"),
            "items",
            $"{item.ItemId:N}.json")));

        var versionArchive = service.ArchiveVersion(
            localRoot,
            sharedRoot,
            version.Version.VersionId);

        Assert.Empty(versionArchive.Session.LoadResult.Workspace.Versions);
        Assert.True(File.Exists(Path.Combine(versionArchive.ArchiveDirectory, "archive.json")));
        Assert.False(Directory.Exists(Path.Combine(
            localRoot,
            "versions",
            version.Version.VersionId.ToString("N"))));
        Assert.Contains(
            Directory.EnumerateFiles(Path.Combine(localRoot, "log"), "*.json")
                .Select(File.ReadAllText),
            text => text.Contains("\"operation\":\"item.archive\"", StringComparison.Ordinal));
        Assert.Contains(
            Directory.EnumerateFiles(Path.Combine(localRoot, "log"), "*.json")
                .Select(File.ReadAllText),
            text => text.Contains("\"operation\":\"version.archive\"", StringComparison.Ordinal));
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

        var preview = service.PreviewVersionChangelog(
            localRoot,
            sharedRoot,
            versionId,
            rulesOverride: new ChangelogRules(true, false, true, true));

        Assert.False(Directory.Exists(Path.Combine(localRoot, "exports")));
        Assert.Contains("Deferred bugfix", preview, StringComparison.Ordinal);
        Assert.Contains("Still in progress.", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("Generated:", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("`BP-", preview, StringComparison.Ordinal);

        var export = service.ExportVersionChangelog(localRoot, sharedRoot, versionId);

        Assert.True(File.Exists(export.FilePath));
        Assert.Contains("# Blueprints 1.5.0", export.Markdown, StringComparison.Ordinal);
        Assert.Contains("## Added", export.Markdown, StringComparison.Ordinal);
        Assert.Contains("`BP-151` Ship project workflow", export.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Deferred bugfix", export.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportVersionChangelog_IncludesMatchedAndUnmatchedSourceChanges()
    {
        var localRoot = Path.Combine(_rootDirectory, "source-changelog-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "source-changelog-shared", "BP");
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

        var sourceChanges = new[]
        {
            new SourceChangeSummary(
                "abcdef1234567890",
                "abcdef1",
                "BP-151 Ship project workflow",
                "Flavio",
                DateTimeOffset.Parse("2026-05-17T12:00:00Z"),
                ["BP-151"]),
            new SourceChangeSummary(
                "1111111111111111",
                "1111111",
                "Tidy unmatched change",
                "Flavio",
                DateTimeOffset.Parse("2026-05-17T12:30:00Z"),
                []),
        };

        var export = service.ExportVersionChangelog(localRoot, sharedRoot, versionId, sourceChanges);

        Assert.Contains("## Source Changes", export.Markdown, StringComparison.Ordinal);
        Assert.Contains("Matched to this version:", export.Markdown, StringComparison.Ordinal);
        Assert.Contains("`abcdef1` BP-151 Ship project workflow (BP-151)", export.Markdown, StringComparison.Ordinal);
        Assert.Contains("Unmatched recent changes:", export.Markdown, StringComparison.Ordinal);
        Assert.Contains("`1111111` Tidy unmatched change", export.Markdown, StringComparison.Ordinal);
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
        Assert.True(Directory.Exists(resolution.RecoveryDirectory));
        Assert.True(File.Exists(Path.Combine(resolution.RecoveryDirectory, "resolution.json")));
        Assert.True(File.Exists(Path.Combine(
            resolution.RecoveryDirectory,
            "local",
            conflictPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(
            resolution.RecoveryDirectory,
            "shared",
            conflictPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Contains(
            "\"status\": \"Applied\"",
            File.ReadAllText(Path.Combine(resolution.RecoveryDirectory, "resolution.json")),
            StringComparison.Ordinal);

        var refreshed = service.OpenProject(localRoot, sharedRoot);
        Assert.Empty(refreshed.ConflictPaths);
        Assert.Equal(TrustState.Trusted, refreshed.LoadResult.TrustReport.State);
    }

    [Fact]
    public void ResolveConflict_RejectsAConflictPathOutsideTheWorkspace()
    {
        var localRoot = Path.Combine(_rootDirectory, "conflict-path-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "conflict-path-shared", "BP");
        var service = CreateService();
        service.CreateProject(
            new ProjectCreateRequest(
                "Blueprints",
                "BP",
                "SemVer",
                localRoot,
                sharedRoot));

        var stateStore = new Blueprints.Collaboration.Services.FileSystemSyncStateStore();
        var state = stateStore.Load(localRoot);
        stateStore.Save(
            localRoot,
            state with { UnresolvedConflicts = ["../outside.json"] });

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.ResolveConflict(
                localRoot,
                sharedRoot,
                "../outside.json",
                ConflictResolutionChoice.KeepLocal));

        Assert.Contains("escapes", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_rootDirectory, "conflict-path-local", "outside.json")));
    }

    [Fact]
    public void ApplyApprovedSourceImport_CreatesSignedItemsWithProvenanceInOneAuditAction()
    {
        var localRoot = Path.Combine(_rootDirectory, "source-import-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "source-import-shared", "BP");
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
            new VersionEditRequest(null, "0.2.0", ReleaseStatus.InProgress, null));
        var versionId = versionSession.LoadResult.Workspace.Versions.Single().Version.VersionId;

        var updated = service.ApplyApprovedSourceImport(
            localRoot,
            sharedRoot,
            new ApprovedSourceImportRequest(
                [
                    new ApprovedSourceImportItem(
                        versionId,
                        "feature",
                        "added",
                        "Visualize GitHub issues",
                        "Approval-first source ingestion.",
                        false,
                        SourceArtifactKind.GitHubIssue,
                        "github:#42"),
                    new ApprovedSourceImportItem(
                        versionId,
                        "bug",
                        "fixed",
                        "Repair roadmap parsing",
                        null,
                        true,
                        SourceArtifactKind.Roadmap,
                        "roadmap:Roadmap.md:18"),
                ]));

        var items = updated.LoadResult.Workspace.Versions.Single().Items;
        Assert.Equal(2, items.Count);
        Assert.Contains(items, static item =>
            item.Title == "Visualize GitHub issues" &&
            item.Tags.Contains("source-import") &&
            item.Tags.Contains("source:githubissue") &&
            item.Tags.Contains("github:#42"));
        Assert.Contains(items, static item => item.ItemKey.StartsWith("BUG-", StringComparison.Ordinal));

        var auditEntries = Directory.EnumerateFiles(Path.Combine(localRoot, "log"), "*.json");
        Assert.Single(
            auditEntries,
            path => File.ReadAllText(path)
                .Contains("\"operation\":\"source.import.apply\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyApprovedSourceImport_RejectsUnknownProjectTaxonomy()
    {
        var localRoot = Path.Combine(_rootDirectory, "source-invalid-local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "source-invalid-shared", "BP");
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
            new VersionEditRequest(null, "0.2.0", ReleaseStatus.InProgress, null));
        var versionId = versionSession.LoadResult.Workspace.Versions.Single().Version.VersionId;

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.ApplyApprovedSourceImport(
                localRoot,
                sharedRoot,
                new ApprovedSourceImportRequest(
                    [
                        new ApprovedSourceImportItem(
                            versionId,
                            "unknown",
                            "added",
                            "Invalid proposal",
                            null,
                            false,
                            SourceArtifactKind.Roadmap,
                            "roadmap:Roadmap.md:1"),
                    ])));

        Assert.Contains("not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
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
