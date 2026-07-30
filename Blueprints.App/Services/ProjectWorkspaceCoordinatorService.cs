using System.Text.Json;
using Blueprints.App.Models;
using Blueprints.Collaboration.Enums;
using Blueprints.Collaboration.Models;
using Blueprints.Collaboration.Services;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Core.Services;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;
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
    private readonly FileSystemWorkspaceSyncService _workspaceSyncService;
    private readonly RecentProjectsStore _recentProjectsStore;
    private readonly FileSystemAuditLogService _auditLogService;
    private readonly SharedFolderSafetyInspector _sharedFolderSafetyInspector;

    public ProjectWorkspaceCoordinatorService(
        IIdentityService identityService,
        IProjectWorkspaceStore workspaceStore,
        FileSystemSyncStateStore syncStateStore,
        WorkspaceSyncAnalyzer syncAnalyzer,
        FileSystemWorkspaceSyncService workspaceSyncService,
        RecentProjectsStore recentProjectsStore,
        FileSystemAuditLogService auditLogService,
        SharedFolderSafetyInspector sharedFolderSafetyInspector)
    {
        _identityService = identityService;
        _workspaceStore = workspaceStore;
        _syncStateStore = syncStateStore;
        _syncAnalyzer = syncAnalyzer;
        _workspaceSyncService = workspaceSyncService;
        _recentProjectsStore = recentProjectsStore;
        _auditLogService = auditLogService;
        _sharedFolderSafetyInspector = sharedFolderSafetyInspector;
    }

    public IReadOnlyList<RecentProjectReference> GetRecentProjects() =>
        _recentProjectsStore.Load();

    public LocalWorkspaceSession CreateProject(ProjectCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var localRoot = ResolveLocalWorkspaceRoot(request.Name, request.ProjectCode, request.LocalWorkspaceRoot);
        var sharedRoot = ResolveSharedWorkspaceRoot(request.Name, request.ProjectCode, request.SharedWorkspaceRoot);
        EnsureSharedFolderSafe(localRoot, sharedRoot);

        if (File.Exists(Path.Combine(localRoot, "project", "project.json")))
        {
            throw new InvalidOperationException("A signed project already exists at the chosen local workspace path.");
        }

        var snapshot = CreateProjectSnapshot(identity, request);
        _workspaceStore.Save(localRoot, snapshot, identity.SigningKey);
        AppendAuditEntry(localRoot, identity, snapshot, "project.create", $"Created project {snapshot.Project.Name}.");
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
        var safetyReport = _sharedFolderSafetyInspector.Inspect(paths.SharedProjectRoot, paths.LocalWorkspaceRoot);

        var loadResult = _workspaceStore.Load(paths.LocalWorkspaceRoot, identity.PublicKey);
        var auditValidation = _auditLogService.Validate(paths.LocalWorkspaceRoot, identity.PublicKey);
        loadResult = ApplyWorkspaceSafety(loadResult, safetyReport, auditValidation);
        var syncState = _syncStateStore.Load(paths.LocalWorkspaceRoot);
        var analysis = _syncAnalyzer.Analyze(paths, syncState.TrackedEntries);
        var conflictPaths = syncState.UnresolvedConflicts
            .Union(analysis.PotentialConflictDocumentPaths, StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var sync = new SyncSummary(
            DetermineHealth(analysis, conflictPaths.Length),
            analysis.OutgoingDocumentPaths.Count,
            analysis.IncomingDocumentPaths.Count,
            conflictPaths.Length,
            syncState.LastPulledManifestVersion,
            syncState.LastPushedManifestVersion,
            syncState.LastSuccessfulTrustValidationUtc);

        var session = new LocalWorkspaceSession(
            identity,
            paths,
            loadResult,
            sync,
            conflictPaths,
            auditValidation,
            safetyReport);
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
        EnsureWorkspaceMutable(session);
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
        }, "version.save", $"Saved version {normalizedName}.");
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
        EnsureWorkspaceMutable(session);
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
        }, "item.save", $"Saved item {normalizedTitle}.");
    }

    public LocalWorkspaceSession ApplyApprovedSourceImport(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        ApprovedSourceImportRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Items.Count is < 1 or > 100)
        {
            throw new InvalidOperationException("Approve between 1 and 100 source proposals at a time.");
        }

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureWorkspaceMutable(session);
        var workspace = session.LoadResult.Workspace;
        var versions = workspace.Versions.ToList();

        foreach (var import in request.Items)
        {
            var versionIndex = versions.FindIndex(snapshot => snapshot.Version.VersionId == import.VersionId);
            if (versionIndex < 0)
            {
                throw new InvalidOperationException($"The target version for “{import.Title}” was not found.");
            }

            if (!workspace.Project.ItemTypes.ContainsKey(import.ItemTypeId))
            {
                throw new InvalidOperationException($"Item type “{import.ItemTypeId}” is not configured for this project.");
            }

            if (!workspace.Project.DefaultCategories.Any(category =>
                    string.Equals(category.Id, import.CategoryId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Category “{import.CategoryId}” is not configured for this project.");
            }

            var title = import.Title.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new InvalidOperationException("Every approved proposal needs a title.");
            }

            var targetVersion = versions[versionIndex];
            EnsureItemChangesAllowed(targetVersion.Version.Status);
            var currentWorkspace = workspace with { Versions = versions.ToArray() };
            var createdUtc = DateTimeOffset.UtcNow;
            var items = targetVersion.Items
                .Append(
                    new ItemDocument(
                        CurrentSchemaVersion,
                        workspace.Project.ProjectId,
                        import.VersionId,
                        Guid.NewGuid(),
                        GenerateItemKey(currentWorkspace, targetVersion, import.ItemTypeId),
                        import.ItemTypeId,
                        import.CategoryId,
                        title,
                        string.IsNullOrWhiteSpace(import.Description) ? null : import.Description.Trim(),
                        import.IsDone,
                        [
                            "source-import",
                            $"source:{import.SourceKind.ToString().ToLowerInvariant()}",
                            import.SourceReference,
                        ],
                        createdUtc,
                        createdUtc,
                        identity.Profile.UserId,
                        identity.Profile.DisplayName))
                .ToArray();
            versions[versionIndex] = targetVersion with
            {
                Items = items,
                Version = targetVersion.Version with
                {
                    ManualOrder = items.Select(static item => item.ItemId).ToArray(),
                },
            };
        }

        return SaveWorkspace(
            localWorkspaceRoot,
            sharedWorkspaceRoot,
            identity,
            workspace with { Versions = versions.ToArray() },
            "source.import.apply",
            $"Approved and imported {request.Items.Count} source proposals.");
    }

    public LocalWorkspaceSession SaveCanvasLayout(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        CanvasLayoutEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureWorkspaceMutable(session);
        var workspace = session.LoadResult.Workspace;

        var layout = new CanvasLayoutDocument(
            CurrentSchemaVersion,
            workspace.Project.ProjectId,
            (workspace.CanvasLayout?.Revision ?? 0) + 1,
            request.Nodes
                .Select(static node => new CanvasNodePosition(
                    node.NodeType,
                    node.EntityId,
                    node.X,
                    node.Y))
                .OrderBy(static node => node.NodeType, StringComparer.Ordinal)
                .ThenBy(static node => node.EntityId)
                .ToArray(),
            DateTimeOffset.UtcNow,
            identity.Profile.UserId,
            identity.Profile.DisplayName);
        CanvasLayoutValidator.Validate(layout, workspace.Project.ProjectId);
        CanvasLayoutValidator.ValidateEntityReferences(
            layout,
            workspace.Project.ProjectId,
            workspace.Versions.Select(static version => version.Version.VersionId).ToHashSet(),
            workspace.Versions
                .SelectMany(static version => version.Items)
                .Select(static item => item.ItemId)
                .ToHashSet());

        _workspaceStore.SaveCanvasLayout(localWorkspaceRoot, layout, identity.SigningKey);
        AppendAuditEntry(
            localWorkspaceRoot,
            identity,
            workspace,
            "canvas.layout.save",
            $"Saved canvas layout revision {layout.Revision} with {layout.Nodes.Count} nodes.");
        return OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
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
        EnsureWorkspaceMutable(session);
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
        }, "version.release", $"Released version {existing.Version.Name}.");
    }

    public ChangelogExportResult ExportVersionChangelog(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Guid versionId,
        IReadOnlyList<SourceChangeSummary>? sourceChanges = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureTrustedWorkspace(session);
        var version = session.LoadResult.Workspace.Versions
            .FirstOrDefault(entry => entry.Version.VersionId == versionId);
        if (version is null)
        {
            throw new InvalidOperationException("The selected version was not found.");
        }

        var markdown = MarkdownChangelogBuilder.Build(session.LoadResult.Workspace, version, sourceChanges);
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
        EnsureNoSyncConflicts(session);

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
        EnsureNoSyncConflicts(session);

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

    public LocalWorkspaceSession RefreshProject(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot) =>
        OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);

    public WorkspaceSyncResult PushWorkspace(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureTrustedWorkspace(session);

        return _workspaceSyncService.Push(
            session.Paths,
            session.LoadResult.Workspace.Project.ProjectId,
            identity.SigningKey,
            identity.PublicKey);
    }

    public WorkspaceSyncResult PullWorkspace(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureTrustedWorkspace(session);

        return _workspaceSyncService.Pull(session.Paths, identity.PublicKey);
    }

    public ConflictResolutionResult ResolveConflict(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        string documentPath,
        ConflictResolutionChoice choice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureConflictExists(session, documentPath);

        var recoveryDirectory = CreateConflictRecovery(
            localWorkspaceRoot,
            sharedWorkspaceRoot,
            documentPath,
            choice);

        try
        {
            switch (choice)
            {
                case ConflictResolutionChoice.KeepLocal:
                    MirrorDocumentPair(localWorkspaceRoot, sharedWorkspaceRoot, documentPath);
                    break;
                case ConflictResolutionChoice.AcceptShared:
                    MirrorDocumentPair(sharedWorkspaceRoot, localWorkspaceRoot, documentPath);
                    break;
                default:
                    throw new InvalidOperationException("Unknown conflict resolution choice.");
            }

            UpdateConflictRecoveryStatus(recoveryDirectory, "Applied");
        }
        catch (Exception exception)
        {
            UpdateConflictRecoveryStatus(recoveryDirectory, $"Failed: {exception.Message}");
            throw;
        }

        var state = _syncStateStore.Load(localWorkspaceRoot);
        var refreshedSession = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        _syncStateStore.Save(
            localWorkspaceRoot,
            state with
            {
                UnresolvedConflicts = refreshedSession.ConflictPaths
                    .Where(path => !string.Equals(path, documentPath, StringComparison.Ordinal))
                    .ToArray(),
            });

        return new ConflictResolutionResult(
            documentPath,
            choice,
            recoveryDirectory,
            choice == ConflictResolutionChoice.KeepLocal
                ? $"Kept local copy for {documentPath}. Recovery copy: {recoveryDirectory}"
                : $"Accepted shared copy for {documentPath}. Recovery copy: {recoveryDirectory}");
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

    private static SyncHealth DetermineHealth(WorkspaceSyncAnalysis analysis, int conflictCount)
    {
        if (conflictCount > 0)
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
        ProjectWorkspaceSnapshot workspace,
        string operation,
        string summary)
    {
        _workspaceStore.Save(localWorkspaceRoot, workspace, identity.SigningKey);
        AppendAuditEntry(localWorkspaceRoot, identity, workspace, operation, summary);
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
        }, "members.save", $"Saved membership revision {nextRevision}.");
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

    private void AppendAuditEntry(
        string localWorkspaceRoot,
        Security.Models.StoredIdentity identity,
        ProjectWorkspaceSnapshot workspace,
        string operation,
        string summary)
    {
        _auditLogService.Append(
            localWorkspaceRoot,
            workspace.Project.ProjectId,
            operation,
            summary,
            identity.Profile.UserId,
            identity.Profile.DisplayName,
            workspace.Members.MembershipRevision,
            identity.SigningKey);
    }

    private void EnsureSharedFolderSafe(string localWorkspaceRoot, string sharedWorkspaceRoot)
    {
        var safetyReport = _sharedFolderSafetyInspector.Inspect(sharedWorkspaceRoot, localWorkspaceRoot);
        var blockingFinding = safetyReport.Findings.FirstOrDefault(static finding => string.Equals(finding.Severity, "Error", StringComparison.Ordinal));
        if (blockingFinding is not null)
        {
            throw new InvalidOperationException(blockingFinding.Message);
        }
    }

    private ProjectWorkspaceLoadResult ApplyWorkspaceSafety(
        ProjectWorkspaceLoadResult loadResult,
        SharedFolderSafetyReport safetyReport,
        AuditLogValidationResult auditValidation)
    {
        if (loadResult.TrustReport.State != TrustState.Trusted)
        {
            return loadResult;
        }

        if (!safetyReport.IsSafe)
        {
            return loadResult with
            {
                TrustReport = new TrustReport(
                    TrustState.Corrupt,
                    string.Join(" ", safetyReport.Findings.Select(static finding => finding.Message)),
                    DateTimeOffset.UtcNow),
            };
        }

        if (!auditValidation.IsValid)
        {
            return loadResult with
            {
                TrustReport = new TrustReport(
                    TrustState.Corrupt,
                    auditValidation.Summary,
                    DateTimeOffset.UtcNow),
            };
        }

        var warningSummary = safetyReport.Findings.Count == 0
            ? string.Empty
            : " " + string.Join(" ", safetyReport.Findings.Select(static finding => finding.Message));
        return loadResult with
        {
            TrustReport = loadResult.TrustReport with
            {
                Summary = $"{loadResult.TrustReport.Summary} {auditValidation.Summary}{warningSummary}",
            },
        };
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
            throw new InvalidOperationException("This action requires a trusted workspace.");
        }
    }

    private static void EnsureWorkspaceMutable(LocalWorkspaceSession session)
    {
        if (session.LoadResult.TrustReport.State != TrustState.Trusted)
        {
            throw new InvalidOperationException("This workspace is read-only until trust issues are resolved.");
        }

        EnsureNoSyncConflicts(session);
    }

    private static void EnsureNoSyncConflicts(LocalWorkspaceSession session)
    {
        if (session.ConflictPaths.Count > 0)
        {
            throw new InvalidOperationException("Resolve sync conflicts before making more changes.");
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

    private static void EnsureConflictExists(LocalWorkspaceSession session, string documentPath)
    {
        if (!session.ConflictPaths.Contains(documentPath, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The selected conflict was not found.");
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

    private static void CopyDocumentPair(string sourceRoot, string destinationRoot, string relativePath)
    {
        CopyFile(sourceRoot, destinationRoot, relativePath);
        CopyFile(sourceRoot, destinationRoot, Path.ChangeExtension(relativePath, ".sig"));
    }

    private static string CreateConflictRecovery(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        string documentPath,
        ConflictResolutionChoice choice)
    {
        var recoveryId = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
        var recoveryDirectory = Path.Combine(
            localWorkspaceRoot,
            ".blueprints",
            "recovery",
            "conflicts",
            recoveryId);
        var localState = SnapshotDocumentPair(
            localWorkspaceRoot,
            Path.Combine(recoveryDirectory, "local"),
            documentPath);
        var sharedState = SnapshotDocumentPair(
            sharedWorkspaceRoot,
            Path.Combine(recoveryDirectory, "shared"),
            documentPath);
        var record = new ConflictRecoveryRecord(
            CurrentSchemaVersion,
            recoveryId,
            DateTimeOffset.UtcNow,
            documentPath,
            choice,
            localState.DocumentPresent,
            localState.SignaturePresent,
            sharedState.DocumentPresent,
            sharedState.SignaturePresent,
            "Prepared");
        WriteConflictRecoveryRecord(recoveryDirectory, record);
        return recoveryDirectory;
    }

    private static (bool DocumentPresent, bool SignaturePresent) SnapshotDocumentPair(
        string sourceRoot,
        string recoveryRoot,
        string documentPath)
    {
        var sourceDocumentPath = ResolveWorkspacePath(sourceRoot, documentPath);
        var signaturePath = Path.ChangeExtension(documentPath, ".sig");
        var sourceSignaturePath = ResolveWorkspacePath(sourceRoot, signaturePath);
        var documentPresent = File.Exists(sourceDocumentPath);
        var signaturePresent = File.Exists(sourceSignaturePath);

        if (documentPresent)
        {
            CopyFile(sourceRoot, recoveryRoot, documentPath);
        }

        if (signaturePresent)
        {
            CopyFile(sourceRoot, recoveryRoot, signaturePath);
        }

        return (documentPresent, signaturePresent);
    }

    private static void MirrorDocumentPair(
        string sourceRoot,
        string destinationRoot,
        string documentPath)
    {
        var signaturePath = Path.ChangeExtension(documentPath, ".sig");
        var sourceDocumentPresent = File.Exists(ResolveWorkspacePath(sourceRoot, documentPath));
        var sourceSignaturePresent = File.Exists(ResolveWorkspacePath(sourceRoot, signaturePath));

        if (sourceDocumentPresent != sourceSignaturePresent)
        {
            throw new InvalidOperationException(
                $"Cannot resolve {documentPath} because the selected source has an incomplete document/signature pair.");
        }

        if (!sourceDocumentPresent)
        {
            DeleteFileIfPresent(destinationRoot, documentPath);
            DeleteFileIfPresent(destinationRoot, signaturePath);
            return;
        }

        CopyFile(sourceRoot, destinationRoot, documentPath);
        CopyFile(sourceRoot, destinationRoot, signaturePath);
    }

    private static void DeleteFileIfPresent(string workspaceRoot, string relativePath)
    {
        var path = ResolveWorkspacePath(workspaceRoot, relativePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void UpdateConflictRecoveryStatus(string recoveryDirectory, string status)
    {
        var manifestPath = Path.Combine(recoveryDirectory, "resolution.json");
        var record = JsonSerializer.Deserialize<ConflictRecoveryRecord>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException("Conflict recovery metadata could not be read.");
        WriteConflictRecoveryRecord(recoveryDirectory, record with { Status = status });
    }

    private static void WriteConflictRecoveryRecord(
        string recoveryDirectory,
        ConflictRecoveryRecord record)
    {
        Directory.CreateDirectory(recoveryDirectory);
        var manifestPath = Path.Combine(recoveryDirectory, "resolution.json");
        var tempPath = manifestPath + ".tmp";
        File.WriteAllText(
            tempPath,
            JsonSerializer.Serialize(
                record,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }));
        File.Move(tempPath, manifestPath, overwrite: true);
    }

    private static void CopyFile(string sourceRoot, string destinationRoot, string relativePath)
    {
        var sourcePath = ResolveWorkspacePath(sourceRoot, relativePath);
        var destinationPath = ResolveWorkspacePath(destinationRoot, relativePath);
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = destinationPath + ".tmp";
        File.Copy(sourcePath, tempPath, overwrite: true);
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(tempPath, destinationPath);
    }

    private static string ResolveWorkspacePath(string workspaceRoot, string relativePath)
    {
        var fullRoot = Path.GetFullPath(workspaceRoot);
        var fullPath = Path.GetFullPath(
            Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relativeToRoot = Path.GetRelativePath(fullRoot, fullPath);
        if (relativeToRoot == ".." ||
            relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Workspace document path escapes its expected root.");
        }

        return fullPath;
    }
}
