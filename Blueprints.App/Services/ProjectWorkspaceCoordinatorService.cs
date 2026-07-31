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
using Blueprints.Security.Services;
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
    private readonly IWorkspaceTransactionService _workspaceTransactionService;
    private readonly WorkspaceMigrationService _workspaceMigrationService;
    private readonly FileSystemProjectTrustStore _projectTrustStore = new();
    private readonly IdentityInvitationService _identityInvitationService =
        new(new Ed25519SignatureService());
    private readonly ProjectInvitationService _projectInvitationService =
        new(new Ed25519SignatureService());

    public ProjectWorkspaceCoordinatorService(
        IIdentityService identityService,
        IProjectWorkspaceStore workspaceStore,
        FileSystemSyncStateStore syncStateStore,
        WorkspaceSyncAnalyzer syncAnalyzer,
        FileSystemWorkspaceSyncService workspaceSyncService,
        RecentProjectsStore recentProjectsStore,
        FileSystemAuditLogService auditLogService,
        SharedFolderSafetyInspector sharedFolderSafetyInspector,
        IWorkspaceTransactionService? workspaceTransactionService = null,
        WorkspaceMigrationService? workspaceMigrationService = null)
    {
        _identityService = identityService;
        _workspaceStore = workspaceStore;
        _syncStateStore = syncStateStore;
        _syncAnalyzer = syncAnalyzer;
        _workspaceSyncService = workspaceSyncService;
        _recentProjectsStore = recentProjectsStore;
        _auditLogService = auditLogService;
        _sharedFolderSafetyInspector = sharedFolderSafetyInspector;
        _workspaceTransactionService =
            workspaceTransactionService ?? new FileSystemWorkspaceTransactionService();
        _workspaceMigrationService =
            workspaceMigrationService ?? new WorkspaceMigrationService(_workspaceTransactionService);
    }

    public IReadOnlyList<RecentProjectReference> GetRecentProjects() =>
        _recentProjectsStore.Load();

    public IReadOnlyList<AuditLogEntry> GetAuditHistory(
        string localWorkspaceRoot,
        int maximumCount = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var keys = _projectTrustStore.LoadKeys(localWorkspaceRoot, identity);
        return _auditLogService.ReadVerifiedEntries(
            localWorkspaceRoot,
            keys,
            maximumCount);
    }

    public bool HasLocalIdentity => _identityService.ListProfiles().Count > 0;

    public IdentitySummary CreateInitialIdentity(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (HasLocalIdentity)
        {
            throw new InvalidOperationException(
                "A local signing identity is already configured.");
        }

        var identity = _identityService.CreateIdentity(displayName.Trim());
        return new IdentitySummary(
            identity.Profile.DisplayName,
            identity.Profile.UserId.ToString(),
            identity.Profile.KeyStorageProvider);
    }

    public string ExportIdentityInvitation(string filePath)
    {
        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        return _identityInvitationService.Write(filePath, identity);
    }

    public string ExportIdentityBackup(string filePath, string passphrase) =>
        _identityService.ExportBackup(filePath, passphrase);

    public IdentitySummary ImportIdentityBackup(string filePath, string passphrase)
    {
        if (HasLocalIdentity)
        {
            throw new InvalidOperationException(
                "A signing identity is already configured on this device.");
        }

        var identity = _identityService.ImportBackup(filePath, passphrase);
        return new IdentitySummary(
            identity.Profile.DisplayName,
            identity.Profile.UserId.ToString(),
            identity.Profile.KeyStorageProvider);
    }

    public MemberInviteRequest ReadIdentityInvitation(string filePath)
    {
        var invitation = _identityInvitationService.Read(filePath);
        return new MemberInviteRequest(
            invitation.UserId.ToString(),
            invitation.DisplayName,
            invitation.PublicKeyBase64,
            MemberRole.Editor,
            invitation.KeyId);
    }

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
        _workspaceTransactionService.Execute(localRoot, stagedRoot =>
        {
            _workspaceStore.Save(stagedRoot, snapshot, identity.SigningKey);
            _projectTrustStore.Initialize(
                stagedRoot,
                snapshot.Project.ProjectId,
                [
                    new TrustedProjectKey(
                        identity.Profile.UserId,
                        identity.Profile.DisplayName,
                        identity.Profile.KeyId,
                        identity.Profile.PublicKeyBase64,
                        DateTimeOffset.UtcNow,
                        MemberRole.Admin,
                        true),
                ]);
            AppendAuditEntry(
                stagedRoot,
                identity,
                snapshot,
                "project.create",
                $"Created project {snapshot.Project.Name}.");
        });
        Directory.CreateDirectory(sharedRoot);

        var session = OpenProject(localRoot, sharedRoot);
        return session;
    }

    public LocalWorkspaceSession OpenProject(string localWorkspaceRoot, string sharedWorkspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        _workspaceTransactionService.Recover(localWorkspaceRoot);
        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        _workspaceMigrationService.MigrateIfNeeded(
            localWorkspaceRoot,
            identity.SigningKey);
        var paths = WorkspacePathResolver.Create(localWorkspaceRoot, sharedWorkspaceRoot);
        Directory.CreateDirectory(paths.SharedProjectRoot);
        var safetyReport = _sharedFolderSafetyInspector.Inspect(paths.SharedProjectRoot, paths.LocalWorkspaceRoot);
        var trustedKeys = _projectTrustStore.LoadKeys(paths.LocalWorkspaceRoot, identity);
        var activeContributorKeys = _projectTrustStore.LoadActiveContributorKeys(
            paths.LocalWorkspaceRoot,
            identity);

        var loadResult = _workspaceStore.Load(paths.LocalWorkspaceRoot, activeContributorKeys);
        var auditValidation = _auditLogService.Validate(paths.LocalWorkspaceRoot, trustedKeys);
        loadResult = ApplyWorkspaceSafety(loadResult, safetyReport, auditValidation);
        if (loadResult.TrustReport.State == TrustState.Trusted)
        {
            _projectTrustStore.MergeVerifiedMembers(
                paths.LocalWorkspaceRoot,
                loadResult.Workspace.Project.ProjectId,
                loadResult.Workspace.Members.Members);
        }
        var syncState = _syncStateStore.Load(paths.LocalWorkspaceRoot);
        var analysis = _syncAnalyzer.Analyze(paths, syncState.TrackedEntries);
        var conflictPaths = syncState.UnresolvedConflicts
            .Union(analysis.PotentialConflictDocumentPaths, StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var sharedManifest = _workspaceSyncService.InspectSharedManifest(
            paths.SharedProjectRoot,
            activeContributorKeys);
        var sync = new SyncSummary(
            DetermineHealth(analysis, conflictPaths.Length),
            analysis.OutgoingDocumentPaths.Count,
            analysis.IncomingDocumentPaths.Count,
            conflictPaths.Length,
            syncState.LastPulledManifestVersion,
            syncState.LastPushedManifestVersion,
            syncState.LastSuccessfulTrustValidationUtc,
            sharedManifest.ManifestVersion,
            sharedManifest.BatchId,
            sharedManifest.Exists ? sharedManifest.SignatureValid : null,
            auditValidation.IsValid,
            auditValidation.EntryCount);

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
        var workflowState = request.IsDone
            ? WorkItemLifecycle.Complete
            : request.WorkflowState is null or WorkItemLifecycle.Complete
                ? WorkItemLifecycle.Planned
                : request.WorkflowState.Value;

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
                IsDone = workflowState == WorkItemLifecycle.Complete,
                WorkflowState = workflowState,
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
                    workflowState == WorkItemLifecycle.Complete,
                    [],
                    createdUtc,
                    createdUtc,
                    identity.Profile.UserId,
                    identity.Profile.DisplayName,
                    workflowState));
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

        if (request.Items.Count is < 1 or > RepositorySourceDiscoveryService.MaximumCandidatesPerRepository)
        {
            throw new InvalidOperationException(
                $"Approve between 1 and {RepositorySourceDiscoveryService.MaximumCandidatesPerRepository} source proposals at a time.");
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
                        identity.Profile.DisplayName,
                        import.IsDone
                            ? WorkItemLifecycle.Complete
                            : WorkItemLifecycle.Planned))
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

        _workspaceTransactionService.Execute(localWorkspaceRoot, stagedRoot =>
        {
            _workspaceStore.SaveCanvasLayout(stagedRoot, layout, identity.SigningKey);
            AppendAuditEntry(
                stagedRoot,
                identity,
                workspace,
                "canvas.layout.save",
                $"Saved canvas layout revision {layout.Revision} with {layout.Nodes.Count} nodes.");
        });
        return OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
    }

    public LocalWorkspaceSession SaveRelationshipType(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        RelationshipTypeEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureWorkspaceMutable(session);
        var workspace = session.LoadResult.Workspace;
        var typeId = request.TypeId.Trim().ToLowerInvariant();
        var existing = workspace.Relationships?.Types.FirstOrDefault(type =>
            string.Equals(type.TypeId, typeId, StringComparison.Ordinal));
        if (existing is not null &&
            existing.IsDirectional != request.IsDirectional &&
            workspace.Relationships!.Relationships.Any(edge =>
                string.Equals(edge.TypeId, typeId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "A relationship type's direction cannot change while relationships use it.");
        }

        var types = (workspace.Relationships?.Types ?? [])
            .Where(type => !string.Equals(type.TypeId, typeId, StringComparison.Ordinal))
            .Append(new RelationshipTypeDefinition(
                typeId,
                request.Name.Trim(),
                string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                request.ColorHex.Trim().ToUpperInvariant(),
                request.IsDirectional))
            .OrderBy(static type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static type => type.TypeId, StringComparer.Ordinal)
            .ToArray();
        var document = CreateRelationshipDocument(
            workspace,
            identity,
            types,
            workspace.Relationships?.Relationships ?? []);
        SaveRelationshipDocument(
            localWorkspaceRoot,
            identity,
            workspace,
            document,
            existing is null ? "relationship.type.create" : "relationship.type.update",
            $"{(existing is null ? "Created" : "Updated")} relationship type {document.Types.First(type => type.TypeId == typeId).Name}.");
        return OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
    }

    public LocalWorkspaceSession SaveRelationship(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        RelationshipEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureWorkspaceMutable(session);
        var workspace = session.LoadResult.Workspace;
        var relationships = workspace.Relationships
            ?? throw new InvalidOperationException(
                "Create a relationship type before adding relationships.");
        var relationshipId = request.RelationshipId ?? Guid.NewGuid();
        if (request.RelationshipId is not null &&
            relationships.Relationships.All(edge => edge.RelationshipId != relationshipId))
        {
            throw new InvalidOperationException("The selected relationship was not found.");
        }

        var edges = relationships.Relationships
            .Where(edge => edge.RelationshipId != relationshipId)
            .Append(new RelationshipEdge(
                relationshipId,
                request.TypeId.Trim().ToLowerInvariant(),
                request.Source,
                request.Target,
                string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim()))
            .OrderBy(static edge => edge.TypeId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.RelationshipId)
            .ToArray();
        var document = CreateRelationshipDocument(
            workspace,
            identity,
            relationships.Types,
            edges);
        SaveRelationshipDocument(
            localWorkspaceRoot,
            identity,
            workspace,
            document,
            request.RelationshipId is null ? "relationship.create" : "relationship.update",
            $"{(request.RelationshipId is null ? "Created" : "Updated")} relationship {relationshipId:N}.");
        return OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
    }

    public LocalWorkspaceSession RemoveRelationship(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Guid relationshipId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureWorkspaceMutable(session);
        var workspace = session.LoadResult.Workspace;
        var relationships = workspace.Relationships
            ?? throw new InvalidOperationException("The selected relationship was not found.");
        if (relationships.Relationships.All(edge => edge.RelationshipId != relationshipId))
        {
            throw new InvalidOperationException("The selected relationship was not found.");
        }

        var document = CreateRelationshipDocument(
            workspace,
            identity,
            relationships.Types,
            relationships.Relationships
                .Where(edge => edge.RelationshipId != relationshipId)
                .ToArray());
        SaveRelationshipDocument(
            localWorkspaceRoot,
            identity,
            workspace,
            document,
            "relationship.remove",
            $"Removed relationship {relationshipId:N}.");
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

    public WorkspaceArchiveResult ArchiveVersion(
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
        var version = workspace.Versions.FirstOrDefault(entry =>
            entry.Version.VersionId == versionId)
            ?? throw new InvalidOperationException("The selected version was not found.");
        EnsureArchivable(version.Version.Status, "version");

        var archiveId = CreateArchiveId("version", versionId);
        var archiveDirectory = Path.Combine(
            localWorkspaceRoot,
            ".blueprints",
            "archive",
            archiveId);
        var removedEntityIds = version.Items
            .Select(static item => item.ItemId)
            .Append(versionId)
            .ToHashSet();
        var updatedWorkspace = workspace with
        {
            Versions = workspace.Versions
                .Where(entry => entry.Version.VersionId != versionId)
                .ToArray(),
            CanvasLayout = RemoveArchivedLayoutNodes(
                workspace.CanvasLayout,
                removedEntityIds,
                identity),
            Relationships = RemoveArchivedRelationships(
                workspace.Relationships,
                removedEntityIds,
                identity),
        };
        _workspaceTransactionService.Execute(localWorkspaceRoot, stagedRoot =>
        {
            var versionDirectory = Path.Combine(
                stagedRoot,
                "versions",
                versionId.ToString("N"));
            var stagedArchiveDirectory = Path.Combine(
                stagedRoot,
                ".blueprints",
                "archive",
                archiveId);
            var archivedVersionDirectory = Path.Combine(
                stagedArchiveDirectory,
                "versions",
                versionId.ToString("N"));
            CopyDirectory(versionDirectory, archivedVersionDirectory);
            WriteArchiveRecord(
                stagedArchiveDirectory,
                new WorkspaceArchiveRecord(
                    CurrentSchemaVersion,
                    archiveId,
                    "version",
                    versionId,
                    version.Version.Name,
                    DateTimeOffset.UtcNow,
                    identity.Profile.UserId,
                    identity.Profile.DisplayName,
                    "Applied"));
            Directory.Delete(versionDirectory, recursive: true);
            _workspaceStore.Save(stagedRoot, updatedWorkspace, identity.SigningKey);
            AppendAuditEntry(
                stagedRoot,
                identity,
                updatedWorkspace,
                "version.archive",
                $"Archived draft version {version.Version.Name}.");
        });
        return new WorkspaceArchiveResult(
            OpenProject(localWorkspaceRoot, sharedWorkspaceRoot),
            archiveDirectory,
            $"Archived version {version.Version.Name}. Recovery copy: {archiveDirectory}");
    }

    public WorkspaceArchiveResult ArchiveItem(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Guid versionId,
        Guid itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureWorkspaceMutable(session);
        var workspace = session.LoadResult.Workspace;
        var versionIndex = workspace.Versions
            .Select((entry, index) => (entry, index))
            .FirstOrDefault(pair => pair.entry.Version.VersionId == versionId);
        if (versionIndex.entry is null)
        {
            throw new InvalidOperationException("The selected version was not found.");
        }

        EnsureArchivable(versionIndex.entry.Version.Status, "item");
        var item = versionIndex.entry.Items.FirstOrDefault(candidate =>
            candidate.ItemId == itemId)
            ?? throw new InvalidOperationException("The selected item was not found.");
        var relativeDocumentPath =
            $"versions/{versionId:N}/items/{itemId:N}.json";
        var archiveId = CreateArchiveId("item", itemId);
        var archiveDirectory = Path.Combine(
            localWorkspaceRoot,
            ".blueprints",
            "archive",
            archiveId);
        var versions = workspace.Versions.ToArray();
        versions[versionIndex.index] = versionIndex.entry with
        {
            Version = versionIndex.entry.Version with
            {
                ManualOrder = versionIndex.entry.Version.ManualOrder
                    .Where(id => id != itemId)
                    .ToArray(),
            },
            Items = versionIndex.entry.Items
                .Where(candidate => candidate.ItemId != itemId)
                .ToArray(),
        };
        var updatedWorkspace = workspace with
        {
            Versions = versions,
            CanvasLayout = RemoveArchivedLayoutNodes(
                workspace.CanvasLayout,
                new HashSet<Guid> { itemId },
                identity),
            Relationships = RemoveArchivedRelationships(
                workspace.Relationships,
                new HashSet<Guid> { itemId },
                identity),
        };
        _workspaceTransactionService.Execute(localWorkspaceRoot, stagedRoot =>
        {
            var stagedArchiveDirectory = Path.Combine(
                stagedRoot,
                ".blueprints",
                "archive",
                archiveId);
            CopyDocumentPair(
                stagedRoot,
                stagedArchiveDirectory,
                relativeDocumentPath);
            WriteArchiveRecord(
                stagedArchiveDirectory,
                new WorkspaceArchiveRecord(
                    CurrentSchemaVersion,
                    archiveId,
                    "item",
                    itemId,
                    item.ItemKey,
                    DateTimeOffset.UtcNow,
                    identity.Profile.UserId,
                    identity.Profile.DisplayName,
                    "Applied"));
            DeleteFileIfPresent(stagedRoot, relativeDocumentPath);
            DeleteFileIfPresent(
                stagedRoot,
                Path.ChangeExtension(relativeDocumentPath, ".sig"));
            _workspaceStore.Save(stagedRoot, updatedWorkspace, identity.SigningKey);
            AppendAuditEntry(
                stagedRoot,
                identity,
                updatedWorkspace,
                "item.archive",
                $"Archived item {item.ItemKey}.");
        });
        return new WorkspaceArchiveResult(
            OpenProject(localWorkspaceRoot, sharedWorkspaceRoot),
            archiveDirectory,
            $"Archived item {item.ItemKey}. Recovery copy: {archiveDirectory}");
    }

    public ChangelogExportResult ExportVersionChangelog(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Guid versionId,
        IReadOnlyList<SourceChangeSummary>? sourceChanges = null,
        ChangelogRules? rulesOverride = null)
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

        var markdown = MarkdownChangelogBuilder.Build(
            session.LoadResult.Workspace,
            version,
            sourceChanges,
            rulesOverride);
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

    public string PreviewVersionChangelog(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Guid versionId,
        IReadOnlyList<SourceChangeSummary>? sourceChanges = null,
        ChangelogRules? rulesOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureTrustedWorkspace(session);
        var version = session.LoadResult.Workspace.Versions
            .FirstOrDefault(entry => entry.Version.VersionId == versionId)
            ?? throw new InvalidOperationException("The selected version was not found.");
        return MarkdownChangelogBuilder.Build(
            session.LoadResult.Workspace,
            version,
            sourceChanges,
            rulesOverride);
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
        var keyId = string.IsNullOrWhiteSpace(request.KeyId)
            ? invitedUserId.ToString("N")
            : request.KeyId.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Invitee display name is required.");
        }

        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new InvalidOperationException("Invitee public key is required.");
        }

        if (keyId.Length > 128)
        {
            throw new InvalidOperationException("Invitee key ID is too long.");
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
                true,
                keyId));

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

    public string ExportProjectInvitation(
        string localWorkspaceRoot,
        string sharedWorkspaceRoot,
        Guid invitedUserId,
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var session = OpenProject(localWorkspaceRoot, sharedWorkspaceRoot);
        EnsureTrustedWorkspace(session);
        EnsureCurrentIdentityIsAdmin(session);
        EnsureNoSyncConflicts(session);

        var invitedMember = session.LoadResult.Workspace.Members.Members
            .FirstOrDefault(member => member.UserId == invitedUserId && member.IsActive)
            ?? throw new InvalidOperationException(
                "Select an active invited member before exporting a project invitation.");
        var members = session.LoadResult.Workspace.Members;
        var trustedKeys = members.Members
            .Select(member => new TrustedProjectKey(
                member.UserId,
                member.DisplayName,
                ResolveMemberKeyId(member),
                member.PublicKey,
                member.JoinedUtc,
                member.Role,
                member.IsActive))
            .ToArray();
        var project = session.LoadResult.Workspace.Project;
        var payload = new ProjectInvitationPayload(
            1,
            project.ProjectId,
            project.Name,
            project.ProjectCode,
            session.Paths.SharedProjectRoot,
            members.MembershipRevision,
            invitedMember.UserId,
            ResolveMemberKeyId(invitedMember),
            identity.Profile.UserId,
            identity.Profile.DisplayName,
            identity.Profile.KeyId,
            identity.Profile.PublicKeyBase64,
            trustedKeys,
            DateTimeOffset.UtcNow);
        return _projectInvitationService.Write(filePath, payload, identity.SigningKey);
    }

    public LocalWorkspaceSession JoinProjectFromInvitation(
        string invitationFilePath,
        string localWorkspaceRoot,
        string? sharedWorkspaceRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invitationFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);

        var identity = _identityService.GetOrCreateDefaultIdentity("Local Admin");
        var invitation = _projectInvitationService.Read(invitationFilePath);
        if (invitation.InvitedUserId != identity.Profile.UserId ||
            !string.Equals(
                invitation.InvitedKeyId,
                identity.Profile.KeyId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "This project invitation targets a different local identity.");
        }

        var resolvedSharedRoot = string.IsNullOrWhiteSpace(sharedWorkspaceRoot)
            ? invitation.SharedWorkspaceRoot
            : sharedWorkspaceRoot.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedSharedRoot);
        EnsureSharedFolderSafe(localWorkspaceRoot, resolvedSharedRoot);
        if (Directory.Exists(localWorkspaceRoot) &&
            Directory.EnumerateFileSystemEntries(localWorkspaceRoot).Any())
        {
            throw new InvalidOperationException(
                "The selected local workspace folder must be empty before joining.");
        }

        var fullLocalRoot = Path.GetFullPath(localWorkspaceRoot);
        var parent = Path.GetDirectoryName(fullLocalRoot)
            ?? throw new InvalidOperationException("Local workspace path has no parent.");
        Directory.CreateDirectory(parent);
        var stageRoot = Path.Combine(
            parent,
            $".blueprints-join-{Guid.NewGuid():N}");

        try
        {
            _projectTrustStore.Initialize(
                stageRoot,
                invitation.ProjectId,
                invitation.TrustedKeys);
            var trustedKeys = _projectTrustStore.LoadKeys(stageRoot, identity);
            var activeContributorKeys = _projectTrustStore.LoadActiveContributorKeys(
                stageRoot,
                identity);
            var pull = _workspaceSyncService.Pull(
                new WorkspacePaths(stageRoot, resolvedSharedRoot),
                activeContributorKeys,
                trustedKeys);
            if (!pull.Success)
            {
                throw new InvalidOperationException(
                    $"Project join was blocked: {pull.Summary}");
            }

            var loadResult = _workspaceStore.Load(stageRoot, activeContributorKeys);
            var auditValidation = _auditLogService.Validate(stageRoot, trustedKeys);
            if (loadResult.TrustReport.State != TrustState.Trusted ||
                !auditValidation.IsValid)
            {
                throw new InvalidOperationException(
                    "Project join was blocked because the staged workspace did not validate.");
            }

            ValidateJoinedWorkspace(invitation, identity, loadResult.Workspace);
            if (Directory.Exists(fullLocalRoot))
            {
                Directory.Delete(fullLocalRoot);
            }

            Directory.Move(stageRoot, fullLocalRoot);
            return OpenProject(fullLocalRoot, resolvedSharedRoot);
        }
        catch
        {
            if (Directory.Exists(stageRoot))
            {
                Directory.Delete(stageRoot, recursive: true);
            }

            throw;
        }
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
        EnsureCurrentIdentityCanContribute(session);
        var trustedKeys = _projectTrustStore.LoadKeys(localWorkspaceRoot, identity);

        return _workspaceSyncService.Push(
            session.Paths,
            session.LoadResult.Workspace.Project.ProjectId,
            identity.SigningKey,
            trustedKeys);
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
        var trustedKeys = _projectTrustStore.LoadActiveContributorKeys(
            localWorkspaceRoot,
            identity);
        var auditKeys = _projectTrustStore.LoadKeys(localWorkspaceRoot, identity);

        return _workspaceSyncService.Pull(session.Paths, trustedKeys, auditKeys);
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
        EnsureCurrentIdentityCanContribute(session);

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
                        true,
                        identity.Profile.KeyId),
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
        _workspaceTransactionService.Execute(localWorkspaceRoot, stagedRoot =>
        {
            _workspaceStore.Save(stagedRoot, workspace, identity.SigningKey);
            AppendAuditEntry(stagedRoot, identity, workspace, operation, summary);
        });
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

    private static void EnsureArchivable(ReleaseStatus status, string entityType)
    {
        if (status is ReleaseStatus.Frozen or ReleaseStatus.Released)
        {
            throw new InvalidOperationException(
                $"Cannot archive a {entityType} from a frozen or released version.");
        }
    }

    private static CanvasLayoutDocument? RemoveArchivedLayoutNodes(
        CanvasLayoutDocument? layout,
        IReadOnlySet<Guid> removedEntityIds,
        Security.Models.StoredIdentity identity)
    {
        if (layout is null)
        {
            return null;
        }

        return layout with
        {
            Revision = layout.Revision + 1,
            Nodes = layout.Nodes
                .Where(node => !removedEntityIds.Contains(node.EntityId))
                .ToArray(),
            UpdatedUtc = DateTimeOffset.UtcNow,
            LastModifiedByUserId = identity.Profile.UserId,
            LastModifiedByName = identity.Profile.DisplayName,
        };
    }

    private void SaveRelationshipDocument(
        string localWorkspaceRoot,
        Security.Models.StoredIdentity identity,
        ProjectWorkspaceSnapshot workspace,
        RelationshipDocument document,
        string operation,
        string summary)
    {
        RelationshipDocumentValidator.Validate(document, workspace.Project.ProjectId);
        RelationshipDocumentValidator.ValidateEntityReferences(
            document,
            workspace.Project.ProjectId,
            workspace.Versions.Select(static version => version.Version.VersionId).ToHashSet(),
            workspace.Versions
                .SelectMany(static version => version.Items)
                .Select(static item => item.ItemId)
                .ToHashSet());
        _workspaceTransactionService.Execute(localWorkspaceRoot, stagedRoot =>
        {
            _workspaceStore.SaveRelationships(stagedRoot, document, identity.SigningKey);
            AppendAuditEntry(
                stagedRoot,
                identity,
                workspace with { Relationships = document },
                operation,
                summary);
        });
    }

    private static RelationshipDocument CreateRelationshipDocument(
        ProjectWorkspaceSnapshot workspace,
        Security.Models.StoredIdentity identity,
        IReadOnlyList<RelationshipTypeDefinition> types,
        IReadOnlyList<RelationshipEdge> relationships) =>
        new(
            CurrentSchemaVersion,
            workspace.Project.ProjectId,
            (workspace.Relationships?.Revision ?? 0) + 1,
            types,
            relationships,
            DateTimeOffset.UtcNow,
            identity.Profile.UserId,
            identity.Profile.DisplayName);

    private static RelationshipDocument? RemoveArchivedRelationships(
        RelationshipDocument? document,
        IReadOnlySet<Guid> removedEntityIds,
        Security.Models.StoredIdentity identity)
    {
        if (document is null)
        {
            return null;
        }

        var relationships = document.Relationships
            .Where(edge =>
                !removedEntityIds.Contains(edge.Source.EntityId) &&
                !removedEntityIds.Contains(edge.Target.EntityId))
            .ToArray();
        if (relationships.Length == document.Relationships.Count)
        {
            return document;
        }

        return document with
        {
            Revision = document.Revision + 1,
            Relationships = relationships,
            UpdatedUtc = DateTimeOffset.UtcNow,
            LastModifiedByUserId = identity.Profile.UserId,
            LastModifiedByName = identity.Profile.DisplayName,
        };
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
        EnsureCurrentIdentityCanContribute(session);
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

    private static void EnsureCurrentIdentityCanContribute(LocalWorkspaceSession session)
    {
        var currentMember = session.LoadResult.Workspace.Members.Members
            .FirstOrDefault(member =>
                member.UserId == session.Identity.Profile.UserId &&
                member.IsActive);
        if (currentMember is null ||
            currentMember.Role is not MemberRole.Editor and not MemberRole.Admin)
        {
            throw new InvalidOperationException(
                "Only active editors or administrators can change or publish this project.");
        }
    }

    private static void EnsureAdminCoverage(IReadOnlyCollection<ProjectMember> members)
    {
        if (!members.Any(member => member.IsActive && member.Role == MemberRole.Admin))
        {
            throw new InvalidOperationException("At least one active admin must remain in the project.");
        }
    }

    private static void ValidateJoinedWorkspace(
        ProjectInvitationPayload invitation,
        Security.Models.StoredIdentity identity,
        ProjectWorkspaceSnapshot workspace)
    {
        if (workspace.Project.ProjectId != invitation.ProjectId ||
            workspace.Members.ProjectId != invitation.ProjectId ||
            workspace.Members.MembershipRevision < invitation.MembershipRevision)
        {
            throw new InvalidOperationException(
                "The shared workspace does not match the project invitation.");
        }

        var invitedMember = workspace.Members.Members.FirstOrDefault(member =>
            member.UserId == identity.Profile.UserId &&
            member.IsActive &&
            string.Equals(member.PublicKey, identity.Profile.PublicKeyBase64, StringComparison.Ordinal) &&
            string.Equals(ResolveMemberKeyId(member), identity.Profile.KeyId, StringComparison.Ordinal));
        if (invitedMember is null)
        {
            throw new InvalidOperationException(
                "The current identity is not an active member of the invited project.");
        }

        var inviter = workspace.Members.Members.FirstOrDefault(member =>
            member.UserId == invitation.InviterUserId &&
            member.IsActive &&
            member.Role == MemberRole.Admin &&
            string.Equals(member.PublicKey, invitation.InviterPublicKeyBase64, StringComparison.Ordinal) &&
            string.Equals(ResolveMemberKeyId(member), invitation.InviterKeyId, StringComparison.Ordinal));
        if (inviter is null)
        {
            throw new InvalidOperationException(
                "The project invitation signer is not an active project administrator.");
        }
    }

    private static string ResolveMemberKeyId(ProjectMember member) =>
        string.IsNullOrWhiteSpace(member.KeyId)
            ? member.UserId.ToString("N")
            : member.KeyId;

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

    private static string CreateArchiveId(
        string entityType,
        Guid entityId)
        => $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{entityType}-{entityId:N}-{Guid.NewGuid():N}";

    private static void WriteArchiveRecord(
        string archiveDirectory,
        WorkspaceArchiveRecord record)
    {
        Directory.CreateDirectory(archiveDirectory);
        var path = Path.Combine(archiveDirectory, "archive.json");
        var tempPath = path + ".tmp";
        File.WriteAllText(
            tempPath,
            JsonSerializer.Serialize(
                record,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }));
        File.Move(tempPath, path, overwrite: true);
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Archive source directory was not found: {sourceDirectory}");
        }

        Directory.CreateDirectory(destinationDirectory);
        foreach (var directory in Directory.EnumerateDirectories(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destinationDirectory,
                Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            var destination = Path.Combine(
                destinationDirectory,
                Path.GetRelativePath(sourceDirectory, file));
            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.Copy(file, destination, overwrite: true);
        }
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
