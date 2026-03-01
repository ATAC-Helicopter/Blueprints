using Blueprints.App.Models;
using Blueprints.Collaboration.Enums;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Core.Services;
using Blueprints.Collaboration.Models;
using Blueprints.Security.Models;
using Blueprints.Storage.Abstractions;
using Blueprints.Storage.Models;
using Blueprints.Storage.Services;

namespace Blueprints.App.Services;

public sealed class LocalWorkspaceService
{
    private const int CurrentSchemaVersion = 1;
    private readonly string _workspaceRoot;
    private readonly IProjectWorkspaceStore _workspaceStore;

    public LocalWorkspaceService(
        string workspaceRoot,
        IProjectWorkspaceStore workspaceStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(workspaceStore);

        _workspaceRoot = workspaceRoot;
        _workspaceStore = workspaceStore;
    }

    public LocalWorkspaceSession GetOrCreateDefaultWorkspace(StoredIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!WorkspaceExists(_workspaceRoot))
        {
            _workspaceStore.Save(_workspaceRoot, CreateStarterWorkspace(identity), identity.SigningKey);
        }

        var loadResult = _workspaceStore.Load(_workspaceRoot, identity.PublicKey);
        return new LocalWorkspaceSession(
            identity,
            WorkspacePathResolver.Create(_workspaceRoot, _workspaceRoot),
            loadResult,
            new SyncSummary(SyncHealth.Idle, 0, 0, 0),
            []);
    }

    private static ProjectWorkspaceSnapshot CreateStarterWorkspace(StoredIdentity identity)
    {
        var createdUtc = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var featureItemId = Guid.NewGuid();
        var bugItemId = Guid.NewGuid();
        var issueItemId = Guid.NewGuid();

        var project = new ProjectConfigurationDocument(
            CurrentSchemaVersion,
            projectId,
            "Blueprints",
            "BP",
            "SemVer",
            createdUtc,
            [
                new CategoryDefinition("feature", "Feature"),
                new CategoryDefinition("bug", "Bug"),
                new CategoryDefinition("issue", "Issue"),
            ],
            new Dictionary<string, ItemTypeDefinition>(StringComparer.Ordinal)
            {
                ["feature"] = new("feature", "Feature"),
                ["bug"] = new("bug", "Bug"),
                ["issue"] = new("issue", "Issue"),
            },
            new Dictionary<string, ItemKeyRule>(StringComparer.Ordinal)
            {
                ["feature"] = new("BP", ItemKeyScope.Version),
                ["bug"] = new("BUG", ItemKeyScope.Project),
                ["issue"] = new("ISS", ItemKeyScope.Project),
            },
            new ChangelogRules(false, true, false, false));

        var members = new MemberDocument(
            CurrentSchemaVersion,
            projectId,
            1,
            [
                new ProjectMember(
                    identity.Profile.UserId,
                    identity.Profile.DisplayName,
                    Convert.ToBase64String(identity.PublicKey.PublicKeyBytes),
                    MemberRole.Admin,
                    createdUtc,
                    true),
            ]);

        var version = new VersionDocument(
            CurrentSchemaVersion,
            projectId,
            versionId,
            "1.0.0",
            ReleaseStatus.InProgress,
            createdUtc,
            null,
            "Starter workspace created from the signed local bootstrap flow.",
            [featureItemId, bugItemId, issueItemId]);

        return new ProjectWorkspaceSnapshot(
            project,
            members,
            [
                new VersionWorkspaceSnapshot(
                    version,
                    [
                        new ItemDocument(
                            CurrentSchemaVersion,
                            projectId,
                            versionId,
                            featureItemId,
                            ItemKeyFormatter.FormatVersionScoped(project.ProjectCode, 1, 0, 1),
                            "feature",
                            "feature",
                            "Wire the shell to signed workspace state",
                            "Replace the static dashboard with live project, trust, and version summaries.",
                            true,
                            ["starter", "ui"],
                            createdUtc,
                            createdUtc,
                            identity.Profile.UserId,
                            identity.Profile.DisplayName),
                        new ItemDocument(
                            CurrentSchemaVersion,
                            projectId,
                            versionId,
                            bugItemId,
                            ItemKeyFormatter.FormatProjectScoped("BUG", 1),
                            "bug",
                            "bug",
                            "Detect signature tampering during workspace load",
                            "Loading should surface untrusted state instead of silently accepting modified files.",
                            true,
                            ["trust"],
                            createdUtc,
                            createdUtc,
                            identity.Profile.UserId,
                            identity.Profile.DisplayName),
                        new ItemDocument(
                            CurrentSchemaVersion,
                            projectId,
                            versionId,
                            issueItemId,
                            ItemKeyFormatter.FormatProjectScoped("ISS", 1),
                            "issue",
                            "issue",
                            "Implement shared-folder sync",
                            "Shared-folder publish and import is the next planned feature branch after live workspace wiring.",
                            false,
                            ["sync", "next"],
                            createdUtc,
                            createdUtc,
                            identity.Profile.UserId,
                            identity.Profile.DisplayName),
                    ]),
            ]);
    }

    private static bool WorkspaceExists(string workspaceRoot) =>
        File.Exists(Path.Combine(workspaceRoot, "project", "project.json"));
}
