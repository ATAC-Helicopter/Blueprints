using System.Collections.ObjectModel;
using System.Linq;
using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.Collaboration.Enums;
using Blueprints.Collaboration.Models;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Models;
using CommunityToolkit.Mvvm.Input;

namespace Blueprints.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ProjectWorkspaceCoordinatorService? _coordinatorService;
    private string _title = "Blueprints Setup";
    private ProjectSummary _currentProject = new(string.Empty, string.Empty, TrustState.Corrupt, string.Empty);
    private IdentitySummary _identity = new(string.Empty, string.Empty, string.Empty);
    private SyncSummary _sync = new(SyncHealth.Idle, 0, 0, 0);
    private string _trustSummary = "Create a new project or open an existing workspace.";
    private string _workspacePath = string.Empty;
    private string _sharedSyncPath = string.Empty;
    private string _versioningScheme = string.Empty;
    private int _versionCount;
    private int _itemCount;
    private int _activeMemberCount;
    private int _membershipRevision;
    private bool _hasActiveSession;
    private string _setupMessage = string.Empty;
    private string _createProjectName = "Blueprints";
    private string _createProjectCode = "BP";
    private string _createVersioningScheme = "SemVer";
    private string _createLocalWorkspaceRoot = string.Empty;
    private string _createSharedWorkspaceRoot = string.Empty;
    private string _openLocalWorkspaceRoot = string.Empty;
    private string _openSharedWorkspaceRoot = string.Empty;
    private RecentProjectReference? _selectedRecentProject;

    public MainWindowViewModel()
    {
        Versions = new ObservableCollection<VersionSummary>();
        RecentProjects = new ObservableCollection<RecentProjectReference>();
        ApplyDesignSession(CreateDesignSession());
    }

    public MainWindowViewModel(ProjectWorkspaceCoordinatorService coordinatorService)
    {
        _coordinatorService = coordinatorService;
        Versions = new ObservableCollection<VersionSummary>();
        RecentProjects = new ObservableCollection<RecentProjectReference>();

        RefreshRecentProjects();
        RefreshSuggestedPaths();
        ApplySetupState("Create a new project or open an existing workspace.");
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public ProjectSummary CurrentProject
    {
        get => _currentProject;
        private set => SetProperty(ref _currentProject, value);
    }

    public IdentitySummary Identity
    {
        get => _identity;
        private set => SetProperty(ref _identity, value);
    }

    public string IdentityId => Identity.UserId;

    public SyncSummary Sync
    {
        get => _sync;
        private set
        {
            if (SetProperty(ref _sync, value))
            {
                OnPropertyChanged(nameof(SyncStatus));
            }
        }
    }

    public ObservableCollection<VersionSummary> Versions { get; }

    public ObservableCollection<RecentProjectReference> RecentProjects { get; }

    public string TrustSummary
    {
        get => _trustSummary;
        private set => SetProperty(ref _trustSummary, value);
    }

    public string WorkspacePath
    {
        get => _workspacePath;
        private set => SetProperty(ref _workspacePath, value);
    }

    public string SharedSyncPath
    {
        get => _sharedSyncPath;
        private set => SetProperty(ref _sharedSyncPath, value);
    }

    public string VersioningScheme
    {
        get => _versioningScheme;
        private set => SetProperty(ref _versioningScheme, value);
    }

    public int VersionCount
    {
        get => _versionCount;
        private set => SetProperty(ref _versionCount, value);
    }

    public int ItemCount
    {
        get => _itemCount;
        private set => SetProperty(ref _itemCount, value);
    }

    public int ActiveMemberCount
    {
        get => _activeMemberCount;
        private set => SetProperty(ref _activeMemberCount, value);
    }

    public int MembershipRevision
    {
        get => _membershipRevision;
        private set => SetProperty(ref _membershipRevision, value);
    }

    public bool HasActiveSession
    {
        get => _hasActiveSession;
        private set
        {
            if (SetProperty(ref _hasActiveSession, value))
            {
                OnPropertyChanged(nameof(IsSetupMode));
            }
        }
    }

    public bool IsSetupMode => !HasActiveSession;

    public string SetupMessage
    {
        get => _setupMessage;
        private set => SetProperty(ref _setupMessage, value);
    }

    public string CreateProjectName
    {
        get => _createProjectName;
        set
        {
            if (SetProperty(ref _createProjectName, value))
            {
                RefreshSuggestedPaths();
            }
        }
    }

    public string CreateProjectCode
    {
        get => _createProjectCode;
        set
        {
            if (SetProperty(ref _createProjectCode, value))
            {
                RefreshSuggestedPaths();
            }
        }
    }

    public string CreateVersioningScheme
    {
        get => _createVersioningScheme;
        set => SetProperty(ref _createVersioningScheme, value);
    }

    public string CreateLocalWorkspaceRoot
    {
        get => _createLocalWorkspaceRoot;
        set => SetProperty(ref _createLocalWorkspaceRoot, value);
    }

    public string CreateSharedWorkspaceRoot
    {
        get => _createSharedWorkspaceRoot;
        set => SetProperty(ref _createSharedWorkspaceRoot, value);
    }

    public string OpenLocalWorkspaceRoot
    {
        get => _openLocalWorkspaceRoot;
        set => SetProperty(ref _openLocalWorkspaceRoot, value);
    }

    public string OpenSharedWorkspaceRoot
    {
        get => _openSharedWorkspaceRoot;
        set => SetProperty(ref _openSharedWorkspaceRoot, value);
    }

    public RecentProjectReference? SelectedRecentProject
    {
        get => _selectedRecentProject;
        set => SetProperty(ref _selectedRecentProject, value);
    }

    public string TrustBadge => TrustStatePresenter.ToDisplayText(CurrentProject.TrustState);

    public string SyncStatus =>
        Sync.Health switch
        {
            SyncHealth.Ready => $"{Sync.PendingOutgoingChanges} outgoing, {Sync.PendingIncomingChanges} incoming",
            SyncHealth.NeedsAttention => $"{Sync.ConflictCount} conflicts need attention",
            SyncHealth.Idle => "Sync baseline is current",
            _ => "Sync unavailable",
        };

    [RelayCommand]
    private void CreateProject()
    {
        if (_coordinatorService is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CreateProjectName) || string.IsNullOrWhiteSpace(CreateProjectCode))
        {
            SetupMessage = "Project name and project code are required.";
            return;
        }

        try
        {
            var session = _coordinatorService.CreateProject(
                new ProjectCreateRequest(
                    CreateProjectName,
                    CreateProjectCode,
                    CreateVersioningScheme,
                    CreateLocalWorkspaceRoot,
                    CreateSharedWorkspaceRoot));

            ApplySession(session);
            RefreshRecentProjects();
            SetupMessage = $"Created project {session.LoadResult.Workspace.Project.Name}.";
        }
        catch (Exception exception)
        {
            SetupMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void OpenProject()
    {
        if (_coordinatorService is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(OpenLocalWorkspaceRoot) || string.IsNullOrWhiteSpace(OpenSharedWorkspaceRoot))
        {
            SetupMessage = "Both local workspace root and shared sync root are required to open a project.";
            return;
        }

        try
        {
            var session = _coordinatorService.OpenProject(OpenLocalWorkspaceRoot, OpenSharedWorkspaceRoot);
            ApplySession(session);
            RefreshRecentProjects();
            SetupMessage = $"Opened project {session.LoadResult.Workspace.Project.Name}.";
        }
        catch (Exception exception)
        {
            SetupMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void OpenSelectedRecentProject()
    {
        if (SelectedRecentProject is null)
        {
            SetupMessage = "Select a recent project first.";
            return;
        }

        OpenLocalWorkspaceRoot = SelectedRecentProject.LocalWorkspaceRoot;
        OpenSharedWorkspaceRoot = SelectedRecentProject.SharedWorkspaceRoot;
        OpenProject();
    }

    [RelayCommand]
    private void ReturnToProjectSetup()
    {
        ApplySetupState("Choose a project to create or open.");
        RefreshRecentProjects();
        RefreshSuggestedPaths();
    }

    private void ApplySession(LocalWorkspaceSession session)
    {
        var workspace = session.LoadResult.Workspace;
        var project = workspace.Project;

        CurrentProject = new ProjectSummary(
            project.Name,
            project.ProjectCode,
            session.LoadResult.TrustReport.State,
            session.Paths.SharedProjectRoot);

        Identity = new IdentitySummary(
            session.Identity.Profile.DisplayName,
            session.Identity.Profile.UserId.ToString(),
            session.Identity.Profile.KeyStorageProvider);

        Versions.Clear();
        foreach (var version in workspace.Versions
                     .OrderByDescending(static value => value.Version.CreatedUtc)
                     .Select(static value => new VersionSummary(
                         value.Version.Name,
                         value.Version.Status,
                         value.Items.Count,
                         value.Items.Count(static item => item.IsDone))))
        {
            Versions.Add(version);
        }

        Title = $"{project.Name} ({project.ProjectCode})";
        TrustSummary = session.LoadResult.TrustReport.Summary;
        WorkspacePath = session.Paths.LocalWorkspaceRoot;
        SharedSyncPath = session.Paths.SharedProjectRoot;
        VersioningScheme = project.VersioningScheme;
        VersionCount = workspace.Versions.Count;
        ItemCount = workspace.Versions.Sum(static version => version.Items.Count);
        ActiveMemberCount = workspace.Members.Members.Count(static member => member.IsActive);
        MembershipRevision = workspace.Members.MembershipRevision;
        Sync = session.Sync;
        HasActiveSession = true;
        OnPropertyChanged(nameof(TrustBadge));
        OnPropertyChanged(nameof(IdentityId));
    }

    private void ApplySetupState(string message)
    {
        Title = "Blueprints Setup";
        CurrentProject = new ProjectSummary(string.Empty, string.Empty, TrustState.Corrupt, string.Empty);
        TrustSummary = message;
        WorkspacePath = string.Empty;
        SharedSyncPath = string.Empty;
        VersioningScheme = string.Empty;
        VersionCount = 0;
        ItemCount = 0;
        ActiveMemberCount = 0;
        MembershipRevision = 0;
        Sync = new SyncSummary(SyncHealth.Idle, 0, 0, 0);
        Versions.Clear();
        HasActiveSession = false;
        OnPropertyChanged(nameof(TrustBadge));
        OnPropertyChanged(nameof(IdentityId));
    }

    private void ApplyDesignSession(LocalWorkspaceSession session)
    {
        ApplySession(session);
        RecentProjects.Clear();
        RecentProjects.Add(
            new RecentProjectReference(
                session.LoadResult.Workspace.Project.Name,
                session.LoadResult.Workspace.Project.ProjectCode,
                session.Paths.LocalWorkspaceRoot,
                session.Paths.SharedProjectRoot,
                DateTimeOffset.Parse("2026-02-28T12:00:00Z")));
    }

    private void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var project in _coordinatorService?.GetRecentProjects() ?? [])
        {
            RecentProjects.Add(project);
        }

        SelectedRecentProject = RecentProjects.FirstOrDefault();
    }

    private void RefreshSuggestedPaths()
    {
        if (_coordinatorService is null)
        {
            return;
        }

        CreateLocalWorkspaceRoot = _coordinatorService.GetSuggestedLocalWorkspaceRoot(CreateProjectName, CreateProjectCode);
        CreateSharedWorkspaceRoot = _coordinatorService.GetSuggestedSharedWorkspaceRoot(CreateProjectName, CreateProjectCode);
    }

    private static LocalWorkspaceSession CreateDesignSession()
    {
        var createdUtc = DateTimeOffset.Parse("2026-02-28T12:00:00Z");
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var versionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        return new LocalWorkspaceSession(
            new StoredIdentity(
                new IdentityProfile(
                    userId,
                    "Local Admin",
                    "design-key",
                    Convert.ToBase64String([4, 5, 6]),
                    "Windows DPAPI",
                    createdUtc),
                new SignatureKeyMaterial("design-key", [1, 2, 3]),
                new SignaturePublicKey("design-key", [4, 5, 6])),
            new WorkspacePaths(
                @"C:\Users\Example\AppData\Local\Blueprints\Workspaces\BP",
                @"C:\Users\Example\AppData\Local\Blueprints\SharedProjects\BP"),
            new ProjectWorkspaceLoadResult(
                new ProjectWorkspaceSnapshot(
                    new ProjectConfigurationDocument(
                        1,
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
                        },
                        new Dictionary<string, ItemKeyRule>(StringComparer.Ordinal)
                        {
                            ["feature"] = new("BP", ItemKeyScope.Version),
                        },
                        new ChangelogRules(false, true, false, false)),
                    new MemberDocument(
                        1,
                        projectId,
                        1,
                        [
                            new ProjectMember(
                                userId,
                                "Local Admin",
                                "design-public-key",
                                MemberRole.Admin,
                                createdUtc,
                                true),
                        ]),
                    [
                        new VersionWorkspaceSnapshot(
                            new VersionDocument(
                                1,
                                projectId,
                                versionId,
                                "1.0.0",
                                ReleaseStatus.InProgress,
                                createdUtc,
                                null,
                                null,
                                []),
                            [
                                new ItemDocument(
                                    1,
                                    projectId,
                                    versionId,
                                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                                    "BP-1001",
                                    "feature",
                                    "feature",
                                    "Starter item",
                                    null,
                                    true,
                                    [],
                                    createdUtc,
                                    createdUtc,
                                    userId,
                                    "Local Admin"),
                            ]),
                    ]),
                new TrustReport(TrustState.Trusted, "Validated 4 signed documents.", createdUtc)),
            new SyncSummary(SyncHealth.Ready, 3, 0, 0));
    }
}
