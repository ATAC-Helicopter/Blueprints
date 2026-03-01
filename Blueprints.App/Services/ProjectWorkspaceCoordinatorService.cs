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
        var normalizedName = request.Name.Trim();
        var normalizedNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Version name is required.");
        }

        if (request.VersionId is Guid versionId)
        {
            var existingIndex = versions.FindIndex(snapshot => snapshot.Version.VersionId == versionId);
            if (existingIndex < 0)
            {
                throw new InvalidOperationException("The selected version was not found.");
            }

            var existing = versions[existingIndex];
            EnsureVersionEditable(existing.Version.Status);
            if (request.Status == ReleaseStatus.Released)
            {
                throw new InvalidOperationException("Use the release workflow to mark a version as released.");
            }

            versions[existingIndex] = existing with
            {
                Version = existing.Version with
                {
                    Name = normalizedName,
                    Status = request.Status,
                    Notes = normalizedNotes,
                },
            };
        }
        else
        {
            if (request.Status == ReleaseStatus.Released)
            {
                throw new InvalidOperationException("New versions cannot be created directly as released.");
            }

            var createdUtc = DateTimeOffset.UtcNow;
            versions.Add(
                new VersionWorkspaceSnapshot(
                    new VersionDocument(
                        1,
                        workspace.Project.ProjectId,
                        Guid.NewGuid(),
                        normalizedName,
                        request.Status,
                        createdUtc,
                        null,
                        normalizedNotes,
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
        EnsureItemChangesAllowed(targetVersion.Version.Status);
        var items = targetVersion.Items.ToList();
        var normalizedTitle = request.Title.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new InvalidOperationException("Item title is required.");
        }

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
                Title = normalizedTitle,
                Description = normalizedDescription,
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
                    normalizedTitle,
                    normalizedDescription,
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

    public LocalWorkspaceSession ReleaseVersion(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Guid versionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        var workspace = session.LoadResult.Workspace;
        var versions = workspace.Versions.ToList();
        var versionIndex = versions.FindIndex(snapshot => snapshot.Version.VersionId == versionId);
        if (versionIndex < 0)
        {
            throw new InvalidOperationException("The selected version was not found.");
        }

        var existing = versions[versionIndex];
        if (existing.Version.Status == ReleaseStatus.Released)
        {
            throw new InvalidOperationException("The selected version is already released.");
        }

        versions[versionIndex] = existing with
        {
            Version = existing.Version with
            {
                Status = ReleaseStatus.Released,
                ReleasedUtc = DateTimeOffset.UtcNow,
            },
        };

        return SaveWorkspace(localWorkspaceRoot, sharedWorkspaceRoot, identity, workspace with
        {
            Versions = versions.ToArray(),
        });
    }

    public ChangelogExportResult ExportVersionChangelog(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Guid versionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        var version = session.LoadResult.Workspace.Versions
            .FirstOrDefault(entry => entry.Version.VersionId == versionId);
        if (version is null)
        {
            throw new InvalidOperationException("The selected version was not found.");
        }

        var markdown = MarkdownChangelogBuilder.Build(session.LoadResult.Workspace, version);
        var exportsRoot = Path.Combine(localWorkspaceRoot, "exports");
        Directory.CreateDirectory(exportsRoot);

        var fileName = $"{session.LoadResult.Workspace.Project.ProjectCode}-{SanitizeFileName(version.Version.Name)}-changelog.md";
        var filePath = Path.Combine(exportsRoot, fileName);
        File.WriteAllText(filePath, markdown);

        return new ChangelogExportResult(
            version.Version.VersionId,
            version.Version.Name,
            filePath,
            markdown);
    }

    public LocalWorkspaceSession InviteMember(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        MemberInviteRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureTrustedWorkspace(session);
        EnsureCurrentIdentityIsAdmin(session);

        if (!Guid.TryParse(request.UserId.Trim(), out var invitedUserId))
        {
            throw new InvalidOperationException("Invitee user ID must be a valid GUID.");
        }

        var displayName = request.DisplayName.Trim();
        var publicKey = request.PublicKey.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Invitee display name is required.");
        }

        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new InvalidOperationException("Invitee public key is required.");
        }

        ValidatePublicKey(publicKey);

        var members = session.LoadResult.Workspace.Members.Members.ToList();
        if (members.Any(member => member.UserId == invitedUserId))
        {
            throw new InvalidOperationException("A member with that user ID already exists.");
        }

        if (members.Any(member => string.Equals(member.PublicKey, publicKey, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("A member with that public key already exists.");
        }

        members.Add(
            new ProjectMember(
                invitedUserId,
                displayName,
                publicKey,
                request.Role,
                DateTimeOffset.UtcNow,
                true));

        return SaveMembers(localWorkspaceRoot, sharedWorkspaceRoot, identity, session.LoadResult.Workspace, members);
    }

    public LocalWorkspaceSession UpdateMember(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        MemberUpdateRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureTrustedWorkspace(session);
        EnsureCurrentIdentityIsAdmin(session);

        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Member display name is required.");
        }

        var members = session.LoadResult.Workspace.Members.Members.ToList();
        var memberIndex = members.FindIndex(member => member.UserId == request.UserId);
        if (memberIndex < 0)
        {
            throw new InvalidOperationException("The selected member was not found.");
        }

        var existing = members[memberIndex];
        members[memberIndex] = existing with
        {
            DisplayName = displayName,
            Role = request.Role,
            IsActive = request.IsActive,
        };

        EnsureAdminCoverage(members);
        return SaveMembers(localWorkspaceRoot, sharedWorkspaceRoot, identity, session.LoadResult.Workspace, members);
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

    private LocalWorkspaceSession SaveMembers(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Security.Models.StoredIdentity identity,
        ProjectWorkspaceSnapshot workspace,
        IReadOnlyList<ProjectMember> members)
    {
        var nextRevision = workspace.Members.MembershipRevision + 1;
        return SaveWorkspace(localWorkspaceRoot, sharedWorkspaceRoot, identity, workspace with
        {
            Members = workspace.Members with
            {
                MembershipRevision = nextRevision,
                Members = members
                    .OrderByDescending(static member => member.IsActive)
                    .ThenByDescending(static member => member.Role)
                    .ThenBy(static member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            },
        });
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

    private static void EnsureVersionEditable(ReleaseStatus status)
    {
        if (status == ReleaseStatus.Released)
        {
            throw new InvalidOperationException("Released versions are immutable.");
        }

        if (status == ReleaseStatus.Frozen)
        {
            throw new InvalidOperationException("Frozen versions are read-only until they are explicitly released.");
        }
    }

    private static void EnsureItemChangesAllowed(ReleaseStatus status)
    {
        if (status == ReleaseStatus.Released)
        {
            throw new InvalidOperationException("Released versions are immutable.");
        }

        if (status == ReleaseStatus.Frozen)
        {
            throw new InvalidOperationException("Frozen versions do not accept item changes.");
        }
    }

    private static (int Major, int Minor) ParseVersion(string versionName)
    {
        var parts = versionName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var major = parts.Length > 0 && int.TryParse(parts[0], out var parsedMajor) ? parsedMajor : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var parsedMinor) ? parsedMinor : 0;
        return (major, minor);
    }

    private static void ValidatePublicKey(string publicKeyBase64)
    {
        try
        {
            _ = Convert.FromBase64String(publicKeyBase64);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Invitee public key must be valid Base64.", exception);
        }
    }

    private static void EnsureTrustedWorkspace(LocalWorkspaceSession session)
    {
        if (session.LoadResult.TrustReport.State != TrustState.Trusted)
        {
            throw new InvalidOperationException("Membership changes require a trusted workspace.");
        }
    }

    private static void EnsureCurrentIdentityIsAdmin(LocalWorkspaceSession session)
    {
        var currentUserId = session.Identity.Profile.UserId;
        var currentMember = session.LoadResult.Workspace.Members.Members
            .FirstOrDefault(member => member.UserId == currentUserId && member.IsActive);

        if (currentMember is null || currentMember.Role != MemberRole.Admin)
        {
            throw new InvalidOperationException("Only active admins can change project membership.");
        }
    }

    private static void EnsureAdminCoverage(IReadOnlyCollection<ProjectMember> members)
    {
        if (!members.Any(member => member.IsActive && member.Role == MemberRole.Admin))
        {
            throw new InvalidOperationException("At least one active admin must remain in the project.");
        }
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

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Trim().Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "version" : sanitized;
    }
}
