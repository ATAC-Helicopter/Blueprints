using Blueprints.App.Models;
using Blueprints.Collaboration.Enums;
using Blueprints.Collaboration.Models;
using Blueprints.Collaboration.Services;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Core.Services;
using Blueprints.Security.Abstractions;
using Blueprints.Storage.Abstractions;
using Blueprints.Storage.Models;
using Blueprints.Storage.Services;

namespace Blueprints.App.Services;

public sealed class ProjectWorkspaceCoordinatorService
{
    private const int CurrentSchemaVersion = 1;
    private readonly IIdentityService _identityService;
    private readonly IProjectWorkspaceStore _workspaceStore;
    private readonly FileSystemSyncStateStore _syncStateStore;
    private readonly WorkspaceSyncAnalyzer _syncAnalyzer;
    private readonly RecentProjectsStore _recentProjectsStore;

    public ProjectWorkspaceCoordinatorService(
        IIdentityService identityService,
        IProjectWorkspaceStore workspaceStore,
        FileSystemSyncStateStore syncStateStore,
        WorkspaceSyncAnalyzer syncAnalyzer,
        RecentProjectsStore recentProjectsStore)
    {
        _identityService = identityService;
        _workspaceStore = workspaceStore;
        _syncStateStore = syncStateStore;
        _syncAnalyzer = syncAnalyzer;
        _recentProjectsStore = recentProjectsStore;
    }

    public IReadOnlyList<RecentProjectReference> GetRecentProjects() =>
        _recentProjectsStore.Load();

    public LocalWorkspaceSession CreateProject(ProjectCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var localRoot = ResolveLocalWorkspaceRoot(request.Name, request.ProjectCode, request.LocalWorkspaceRoot);
        var sharedRoot = ResolveSharedWorkspaceRoot(request.Name, request.ProjectCode, request.SharedWorkspaceRoot);

        if (File.Exists(Path.Combine(localRoot, "project", "project.json")))
        {
            throw new InvalidOperationException("A signed project already exists at the chosen local workspace path.");
        }

        var snapshot = CreateProjectSnapshot(identity, request);
        _workspaceStore.Save(localRoot, snapshot, identity.SigningKey);
        Directory.CreateDirectory(sharedRoot);

        var session = OpenProject(localRoot, sharedRoot);
        return session;
    }

    public LocalWorkspaceSession OpenProject(string localWorkspaceRoot, string sharedWorkspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var paths = WorkspacePathResolver.Create(localWorkspaceRoot, sharedWorkspaceRoot);
        Directory.CreateDirectory(paths.SharedProjectRoot);

        var loadResult = _workspaceStore.Load(paths.LocalWorkspaceRoot, identity.PublicKey);
        var syncState = _syncStateStore.Load(paths.LocalWorkspaceRoot);
        var analysis = _syncAnalyzer.Analyze(paths, syncState.TrackedEntries);
        var sync = new SyncSummary(
            DetermineHealth(analysis),
            analysis.OutgoingDocumentPaths.Count,
            analysis.IncomingDocumentPaths.Count,
            analysis.PotentialConflictDocumentPaths.Count);

        var session = new LocalWorkspaceSession(identity, paths, loadResult, sync);
        RecordRecentProject(session);
        return session;
    }

    public LocalWorkspaceSession SaveVersion(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        VersionEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        var workspace = session.LoadResult.Workspace;
        var versions = workspace.Versions.ToList();

        if (request.VersionId is Guid versionId)
        {
            var existingIndex = versions.FindIndex(snapshot => snapshot.Version.VersionId == versionId);
            if (existingIndex < 0)
            {
                throw new InvalidOperationException("The selected version was not found.");
            }

            var existing = versions[existingIndex];
            versions[existingIndex] = existing with
            {
                Version = existing.Version with
                {
                    Name = request.Name.Trim(),
                    Status = request.Status,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                },
            };
        }
        else
        {
            var createdUtc = DateTimeOffset.UtcNow;
            versions.Add(
                new VersionWorkspaceSnapshot(
                    new VersionDocument(
                        1,
                        workspace.Project.ProjectId,
                        Guid.NewGuid(),
                        request.Name.Trim(),
                        request.Status,
                        createdUtc,
                        null,
                        string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                        []),
                    []));
        }

        return SaveWorkspace(localWorkspaceRoot, sharedWorkspaceRoot, identity, workspace with
        {
            Versions = versions.OrderByDescending(static version => version.Version.CreatedUtc).ToArray(),
        });
    }

    public LocalWorkspaceSession SaveItem(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        ItemEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        var workspace = session.LoadResult.Workspace;
        var versions = workspace.Versions.ToList();
        var versionIndex = versions.FindIndex(snapshot => snapshot.Version.VersionId == request.VersionId);
        if (versionIndex < 0)
        {
            throw new InvalidOperationException("The selected version was not found.");
        }

        var targetVersion = versions[versionIndex];
        var items = targetVersion.Items.ToList();

        if (request.ItemId is Guid itemId)
        {
            var itemIndex = items.FindIndex(item => item.ItemId == itemId);
            if (itemIndex < 0)
            {
                throw new InvalidOperationException("The selected item was not found.");
            }

            var existing = items[itemIndex];
            items[itemIndex] = existing with
            {
                ItemKeyTypeId = request.ItemTypeId,
                CategoryId = request.CategoryId,
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsDone = request.IsDone,
                UpdatedUtc = DateTimeOffset.UtcNow,
                LastModifiedByUserId = identity.Profile.UserId,
                LastModifiedByName = identity.Profile.DisplayName,
            };
        }
        else
        {
            var createdUtc = DateTimeOffset.UtcNow;
            items.Add(
                new ItemDocument(
                    1,
                    workspace.Project.ProjectId,
                    request.VersionId,
                    Guid.NewGuid(),
                    GenerateItemKey(workspace, targetVersion, request.ItemTypeId),
                    request.ItemTypeId,
                    request.CategoryId,
                    request.Title.Trim(),
                    string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    request.IsDone,
                    [],
                    createdUtc,
                    createdUtc,
                    identity.Profile.UserId,
                    identity.Profile.DisplayName));
        }

        versions[versionIndex] = targetVersion with
        {
            Items = items
                .OrderBy(static item => item.CreatedUtc)
                .ToArray(),
            Version = targetVersion.Version with
            {
                ManualOrder = items.Select(static item => item.ItemId).ToArray(),
            },
        };

        return SaveWorkspace(localWorkspaceRoot, sharedWorkspaceRoot, identity, workspace with
        {
            Versions = versions.ToArray(),
        });
    }

    public string GetSuggestedLocalWorkspaceRoot(string projectName, string projectCode) =>
        ResolveLocalWorkspaceRoot(projectName, projectCode, string.Empty);

    public string GetSuggestedSharedWorkspaceRoot(string projectName, string projectCode) =>
        ResolveSharedWorkspaceRoot(projectName, projectCode, string.Empty);

    private void RecordRecentProject(LocalWorkspaceSession session)
    {
        var project = session.LoadResult.Workspace.Project;
        if (string.IsNullOrWhiteSpace(project.Name))
        {
            return;
        }

        _recentProjectsStore.AddOrUpdate(
            new RecentProjectReference(
                project.Name,
                project.ProjectCode,
                session.Paths.LocalWorkspaceRoot,
                session.Paths.SharedProjectRoot,
                DateTimeOffset.UtcNow));
    }

    private static SyncHealth DetermineHealth(WorkspaceSyncAnalysis analysis)
    {
        if (analysis.HasConflicts)
        {
            return SyncHealth.NeedsAttention;
        }

        if (analysis.HasIncomingChanges || analysis.HasOutgoingChanges)
        {
            return SyncHealth.Ready;
        }

        return SyncHealth.Idle;
    }

    private static ProjectWorkspaceSnapshot CreateProjectSnapshot(
        Security.Models.StoredIdentity identity,
        ProjectCreateRequest request)
    {
        var createdUtc = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();

        return new ProjectWorkspaceSnapshot(
            new ProjectConfigurationDocument(
                CurrentSchemaVersion,
                projectId,
                request.Name.Trim(),
                request.ProjectCode.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(request.VersioningScheme) ? "SemVer" : request.VersioningScheme.Trim(),
                createdUtc,
                [
                    new CategoryDefinition("added", "Added"),
                    new CategoryDefinition("changed", "Changed"),
                    new CategoryDefinition("fixed", "Fixed"),
                    new CategoryDefinition("removed", "Removed"),
                    new CategoryDefinition("security", "Security"),
                ],
                new Dictionary<string, ItemTypeDefinition>(StringComparer.Ordinal)
                {
                    ["feature"] = new("feature", "Feature"),
                    ["bug"] = new("bug", "Bug"),
                    ["issue"] = new("issue", "Issue"),
                    ["security"] = new("security", "Security"),
                },
                new Dictionary<string, ItemKeyRule>(StringComparer.Ordinal)
                {
                    ["feature"] = new(request.ProjectCode.Trim().ToUpperInvariant(), ItemKeyScope.Version),
                    ["bug"] = new("BUG", ItemKeyScope.Project),
                    ["issue"] = new("ISS", ItemKeyScope.Project),
                    ["security"] = new("SEC", ItemKeyScope.Project),
                },
                new ChangelogRules(false, true, false, false)),
            new MemberDocument(
                CurrentSchemaVersion,
                projectId,
                1,
                [
                    new ProjectMember(
                        identity.Profile.UserId,
                        identity.Profile.DisplayName,
                        identity.Profile.PublicKeyBase64,
                        MemberRole.Admin,
                        createdUtc,
                        true),
                ]),
            []);
    }

    private LocalWorkspaceSession SaveWorkspace(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Security.Models.StoredIdentity identity,
        ProjectWorkspaceSnapshot workspace)
    {
        _workspaceStore.Save(localWorkspaceRoot, workspace, identity.SigningKey);
        return OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
    }

    private static string GenerateItemKey(
        ProjectWorkspaceSnapshot workspace,
        VersionWorkspaceSnapshot version,
        string itemTypeId)
    {
        if (!workspace.Project.ItemKeyRules.TryGetValue(itemTypeId, out var rule))
        {
            return Guid.NewGuid().ToString("N");
        }

        if (rule.Scope == ItemKeyScope.Project)
        {
            var sequence = workspace.Versions
                .SelectMany(static entry => entry.Items)
                .Count(item => string.Equals(item.ItemKeyTypeId, itemTypeId, StringComparison.Ordinal))
                + 1;
            return ItemKeyFormatter.FormatProjectScoped(rule.Prefix, sequence);
        }

        var (major, minor) = ParseVersion(version.Version.Name);
        var versionSequence = version.Items.Count(item => string.Equals(item.ItemKeyTypeId, itemTypeId, StringComparison.Ordinal)) + 1;
        return ItemKeyFormatter.FormatVersionScoped(rule.Prefix, major, minor, versionSequence);
    }

    private static (int Major, int Minor) ParseVersion(string versionName)
    {
        var parts = versionName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var major = parts.Length > 0 && int.TryParse(parts[0], out var parsedMajor) ? parsedMajor : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var parsedMinor) ? parsedMinor : 0;
        return (major, minor);
    }

    private static string ResolveLocalWorkspaceRoot(string projectName, string projectCode, string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.Trim();
        }

        return Path.Combine(AppEnvironment.GetWorkspaceCatalogRoot(), BuildFolderName(projectName, projectCode));
    }

    private static string ResolveSharedWorkspaceRoot(string projectName, string projectCode, string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.Trim();
        }

        return Path.Combine(AppEnvironment.GetSharedProjectsRoot(), BuildFolderName(projectName, projectCode));
    }

    private static string BuildFolderName(string projectName, string projectCode)
    {
        var basis = string.IsNullOrWhiteSpace(projectCode) ? projectName : projectCode;
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string((basis ?? "Project").Trim().Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Project" : sanitized;
    }
}
