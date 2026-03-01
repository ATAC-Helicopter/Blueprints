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
    private LocalWorkspaceSession? _currentSession;
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
    private string _workspaceMessage = string.Empty;
    private string _createProjectName = "Blueprints";
    private string _createProjectCode = "BP";
    private string _createVersioningScheme = "SemVer";
    private string _createLocalWorkspaceRoot = string.Empty;
    private string _createSharedWorkspaceRoot = string.Empty;
    private string _openLocalWorkspaceRoot = string.Empty;
    private string _openSharedWorkspaceRoot = string.Empty;
    private RecentProjectReference? _selectedRecentProject;
    private WorkspaceVersionCard? _selectedVersion;
    private WorkspaceItemCard? _selectedItem;
    private string _newVersionName = "1.0.0";
    private string _versionEditorName = string.Empty;
    private string _versionEditorNotes = string.Empty;
    private ReleaseStatus _versionEditorStatus = ReleaseStatus.InProgress;
    private string _itemEditorTitle = string.Empty;
    private string _itemEditorDescription = string.Empty;
    private bool _itemEditorIsDone;
    private string _selectedItemTypeId = "feature";
    private string _selectedCategoryId = "added";
    private string _changelogPreview = string.Empty;
    private string _lastChangelogExportPath = string.Empty;
    private string _identityPublicKey = string.Empty;
    private WorkspaceMemberCard? _selectedMember;
    private string _inviteUserId = string.Empty;
    private string _inviteDisplayName = string.Empty;
    private string _invitePublicKey = string.Empty;
    private MemberRole _inviteRole = MemberRole.Editor;
    private string _memberEditorDisplayName = string.Empty;
    private MemberRole _memberEditorRole = MemberRole.Editor;
    private bool _memberEditorIsActive = true;

    public MainWindowViewModel()
    {
        Versions = new ObservableCollection<WorkspaceVersionCard>();
        AvailableItemTypes = new ObservableCollection<string>();
        AvailableCategories = new ObservableCollection<string>();
        RecentProjects = new ObservableCollection<RecentProjectReference>();
        Members = new ObservableCollection<WorkspaceMemberCard>();
        ApplyDesignSession(CreateDesignSession());
    }

    public MainWindowViewModel(ProjectWorkspaceCoordinatorService coordinatorService)
    {
        _coordinatorService = coordinatorService;
        Versions = new ObservableCollection<WorkspaceVersionCard>();
        AvailableItemTypes = new ObservableCollection<string>();
        AvailableCategories = new ObservableCollection<string>();
        RecentProjects = new ObservableCollection<RecentProjectReference>();
        Members = new ObservableCollection<WorkspaceMemberCard>();

        RefreshRecentProjects();
        RefreshSuggestedPaths();
        ApplySetupState("Create a new project or open an existing workspace.");
    }

    public ObservableCollection<WorkspaceVersionCard> Versions { get; }

    public ObservableCollection<string> AvailableItemTypes { get; }

    public ObservableCollection<string> AvailableCategories { get; }

    public ObservableCollection<RecentProjectReference> RecentProjects { get; }

    public ObservableCollection<WorkspaceMemberCard> Members { get; }

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

    public string IdentityPublicKey
    {
        get => _identityPublicKey;
        private set => SetProperty(ref _identityPublicKey, value);
    }

    public string IdentityBundle =>
        string.IsNullOrWhiteSpace(IdentityId) || string.IsNullOrWhiteSpace(IdentityPublicKey)
            ? string.Empty
            : $"{IdentityId}|{Identity.DisplayName}|{IdentityPublicKey}";

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

    public string WorkspaceMessage
    {
        get => _workspaceMessage;
        private set => SetProperty(ref _workspaceMessage, value);
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

    public WorkspaceVersionCard? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (SetProperty(ref _selectedVersion, value))
            {
                PopulateVersionEditor();
                ChangelogPreview = string.Empty;
                LastChangelogExportPath = string.Empty;
                SelectedItem = value?.Items.FirstOrDefault();
                OnPropertyChanged(nameof(CanEditSelectedVersion));
                OnPropertyChanged(nameof(CanEditItems));
                OnPropertyChanged(nameof(CanReleaseSelectedVersion));
                OnPropertyChanged(nameof(SelectedVersionStateSummary));
            }
        }
    }

    public WorkspaceItemCard? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                PopulateItemEditor();
            }
        }
    }

    public string NewVersionName
    {
        get => _newVersionName;
        set => SetProperty(ref _newVersionName, value);
    }

    public string VersionEditorName
    {
        get => _versionEditorName;
        set => SetProperty(ref _versionEditorName, value);
    }

    public string VersionEditorNotes
    {
        get => _versionEditorNotes;
        set => SetProperty(ref _versionEditorNotes, value);
    }

    public ReleaseStatus VersionEditorStatus
    {
        get => _versionEditorStatus;
        set => SetProperty(ref _versionEditorStatus, value);
    }

    public string ItemEditorTitle
    {
        get => _itemEditorTitle;
        set => SetProperty(ref _itemEditorTitle, value);
    }

    public string ItemEditorDescription
    {
        get => _itemEditorDescription;
        set => SetProperty(ref _itemEditorDescription, value);
    }

    public bool ItemEditorIsDone
    {
        get => _itemEditorIsDone;
        set => SetProperty(ref _itemEditorIsDone, value);
    }

    public string SelectedItemTypeId
    {
        get => _selectedItemTypeId;
        set => SetProperty(ref _selectedItemTypeId, value);
    }

    public string SelectedCategoryId
    {
        get => _selectedCategoryId;
        set => SetProperty(ref _selectedCategoryId, value);
    }

    public string ChangelogPreview
    {
        get => _changelogPreview;
        private set => SetProperty(ref _changelogPreview, value);
    }

    public string LastChangelogExportPath
    {
        get => _lastChangelogExportPath;
        private set => SetProperty(ref _lastChangelogExportPath, value);
    }

    public WorkspaceMemberCard? SelectedMember
    {
        get => _selectedMember;
        set
        {
            if (SetProperty(ref _selectedMember, value))
            {
                PopulateMemberEditor();
                OnPropertyChanged(nameof(CanManageMembers));
                OnPropertyChanged(nameof(CanEditSelectedMember));
                OnPropertyChanged(nameof(SelectedMemberStateSummary));
            }
        }
    }

    public string InviteUserId
    {
        get => _inviteUserId;
        set => SetProperty(ref _inviteUserId, value);
    }

    public string InviteDisplayName
    {
        get => _inviteDisplayName;
        set => SetProperty(ref _inviteDisplayName, value);
    }

    public string InvitePublicKey
    {
        get => _invitePublicKey;
        set => SetProperty(ref _invitePublicKey, value);
    }

    public MemberRole InviteRole
    {
        get => _inviteRole;
        set => SetProperty(ref _inviteRole, value);
    }

    public string MemberEditorDisplayName
    {
        get => _memberEditorDisplayName;
        set => SetProperty(ref _memberEditorDisplayName, value);
    }

    public MemberRole MemberEditorRole
    {
        get => _memberEditorRole;
        set => SetProperty(ref _memberEditorRole, value);
    }

    public bool MemberEditorIsActive
    {
        get => _memberEditorIsActive;
        set => SetProperty(ref _memberEditorIsActive, value);
    }

    public IReadOnlyList<ReleaseStatus> AvailableStatuses { get; } =
        [ReleaseStatus.Planned, ReleaseStatus.InProgress, ReleaseStatus.Frozen, ReleaseStatus.Released];

    public IReadOnlyList<MemberRole> AvailableMemberRoles { get; } =
        [MemberRole.Viewer, MemberRole.Editor, MemberRole.Admin];

    public string TrustBadge => TrustStatePresenter.ToDisplayText(CurrentProject.TrustState);

    public string SyncStatus =>
        Sync.Health switch
        {
            SyncHealth.Ready => $"{Sync.PendingOutgoingChanges} outgoing, {Sync.PendingIncomingChanges} incoming",
            SyncHealth.NeedsAttention => $"{Sync.ConflictCount} conflicts need attention",
            SyncHealth.Idle => "Sync baseline is current",
            _ => "Sync unavailable",
        };

    public bool CanEditSelectedVersion =>
        SelectedVersion is not null &&
        SelectedVersion.Status is not ReleaseStatus.Frozen and not ReleaseStatus.Released;

    public bool CanEditItems =>
        SelectedVersion is not null &&
        SelectedVersion.Status is not ReleaseStatus.Frozen and not ReleaseStatus.Released;

    public bool CanReleaseSelectedVersion =>
        SelectedVersion is not null &&
        SelectedVersion.Status != ReleaseStatus.Released;

    public string SelectedVersionStateSummary =>
        SelectedVersion?.Status switch
        {
            ReleaseStatus.Frozen => "Frozen versions are read-only until they are explicitly released.",
            ReleaseStatus.Released => "Released versions are immutable.",
            _ when SelectedVersion is not null => "This version can still be edited.",
            _ => "Select a version to manage release state.",
        };

    public bool CanManageMembers =>
        _currentSession is not null &&
        Members.Any(member => member.UserId == _currentSession.Identity.Profile.UserId && member.IsActive && member.Role == MemberRole.Admin);

    public bool CanEditSelectedMember => CanManageMembers && SelectedMember is not null;

    public string SelectedMemberStateSummary =>
        SelectedMember switch
        {
            null => "Select a member to edit role and access.",
            { IsCurrentIdentity: true } => "This is the current local identity.",
            { IsActive: false } => "Inactive members keep history but cannot push future changes.",
            _ => "Active member in the signed membership list.",
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
            ApplySession(
                _coordinatorService.CreateProject(
                    new ProjectCreateRequest(
                        CreateProjectName,
                        CreateProjectCode,
                        CreateVersioningScheme,
                        CreateLocalWorkspaceRoot,
                        CreateSharedWorkspaceRoot)));
            RefreshRecentProjects();
            SetupMessage = $"Created project {CurrentProject.Name}.";
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
            ApplySession(_coordinatorService.OpenProject(OpenLocalWorkspaceRoot, OpenSharedWorkspaceRoot));
            RefreshRecentProjects();
            SetupMessage = $"Opened project {CurrentProject.Name}.";
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

    [RelayCommand]
    private void CreateVersion()
    {
        if (_coordinatorService is null || _currentSession is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewVersionName))
        {
            WorkspaceMessage = "Version name is required.";
            return;
        }

        try
        {
            ApplySession(
                _coordinatorService.SaveVersion(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot,
                    new VersionEditRequest(null, NewVersionName, ReleaseStatus.InProgress, null)));
            WorkspaceMessage = $"Created version {NewVersionName}.";
            NewVersionName = NextSuggestedVersionName();
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void SaveVersionDetails()
    {
        if (_coordinatorService is null || _currentSession is null || SelectedVersion is null)
        {
            WorkspaceMessage = "Select a version first.";
            return;
        }

        try
        {
            var selectedVersionId = SelectedVersion.VersionId;
            ApplySession(
                _coordinatorService.SaveVersion(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot,
                    new VersionEditRequest(
                        selectedVersionId,
                        VersionEditorName,
                        VersionEditorStatus,
                        VersionEditorNotes)));
            ReselectVersion(selectedVersionId);
            WorkspaceMessage = $"Updated version {VersionEditorName}.";
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void ReleaseSelectedVersion()
    {
        if (_coordinatorService is null || _currentSession is null || SelectedVersion is null)
        {
            WorkspaceMessage = "Select a version first.";
            return;
        }

        try
        {
            var selectedVersionId = SelectedVersion.VersionId;
            var selectedVersionName = SelectedVersion.Name;
            ApplySession(
                _coordinatorService.ReleaseVersion(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot,
                    selectedVersionId));
            ReselectVersion(selectedVersionId);
            WorkspaceMessage = $"Released version {selectedVersionName}.";
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void ExportSelectedVersionChangelog()
    {
        if (_coordinatorService is null || _currentSession is null || SelectedVersion is null)
        {
            WorkspaceMessage = "Select a version first.";
            return;
        }

        try
        {
            var export = _coordinatorService.ExportVersionChangelog(
                _currentSession.Paths.LocalWorkspaceRoot,
                _currentSession.Paths.SharedProjectRoot,
                SelectedVersion.VersionId);
            ChangelogPreview = export.Markdown;
            LastChangelogExportPath = export.FilePath;
            WorkspaceMessage = $"Exported changelog for {export.VersionName}.";
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void InviteMember()
    {
        if (_coordinatorService is null || _currentSession is null)
        {
            WorkspaceMessage = "Open a project first.";
            return;
        }

        try
        {
            ApplySession(
                _coordinatorService.InviteMember(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot,
                    new MemberInviteRequest(
                        InviteUserId,
                        InviteDisplayName,
                        InvitePublicKey,
                        InviteRole)));
            WorkspaceMessage = $"Invited member {InviteDisplayName.Trim()}.";
            ClearInviteEditor();
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void SaveMemberDetails()
    {
        if (_coordinatorService is null || _currentSession is null || SelectedMember is null)
        {
            WorkspaceMessage = "Select a member first.";
            return;
        }

        try
        {
            var selectedMemberId = SelectedMember.UserId;
            ApplySession(
                _coordinatorService.UpdateMember(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot,
                    new MemberUpdateRequest(
                        selectedMemberId,
                        MemberEditorDisplayName,
                        MemberEditorRole,
                        MemberEditorIsActive)));
            ReselectMember(selectedMemberId);
            WorkspaceMessage = $"Updated member {MemberEditorDisplayName.Trim()}.";
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void AddItem()
    {
        if (_coordinatorService is null || _currentSession is null || SelectedVersion is null)
        {
            WorkspaceMessage = "Select a version before adding an item.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ItemEditorTitle))
        {
            WorkspaceMessage = "Item title is required.";
            return;
        }

        try
        {
            var versionId = SelectedVersion.VersionId;
            ApplySession(
                _coordinatorService.SaveItem(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot,
                    new ItemEditRequest(
                        versionId,
                        null,
                        SelectedItemTypeId,
                        SelectedCategoryId,
                        ItemEditorTitle,
                        ItemEditorDescription,
                        ItemEditorIsDone)));
            ReselectVersion(versionId);
            WorkspaceMessage = $"Added item {ItemEditorTitle}.";
            ClearItemEditorForNewItem();
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void SaveItemDetails()
    {
        if (_coordinatorService is null || _currentSession is null || SelectedVersion is null || SelectedItem is null)
        {
            WorkspaceMessage = "Select an item first.";
            return;
        }

        try
        {
            var versionId = SelectedVersion.VersionId;
            var itemId = SelectedItem.ItemId;
            ApplySession(
                _coordinatorService.SaveItem(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot,
                    new ItemEditRequest(
                        versionId,
                        itemId,
                        SelectedItemTypeId,
                        SelectedCategoryId,
                        ItemEditorTitle,
                        ItemEditorDescription,
                        ItemEditorIsDone)));
            ReselectVersion(versionId);
            ReselectItem(itemId);
            WorkspaceMessage = $"Updated item {ItemEditorTitle}.";
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    private void ApplySession(LocalWorkspaceSession session)
    {
        _currentSession = session;

        var workspace = session.LoadResult.Workspace;
        var project = workspace.Project;
        var previousSelectedVersionId = SelectedVersion?.VersionId;

        CurrentProject = new ProjectSummary(
            project.Name,
            project.ProjectCode,
            session.LoadResult.TrustReport.State,
            session.Paths.SharedProjectRoot);

        Identity = new IdentitySummary(
            session.Identity.Profile.DisplayName,
            session.Identity.Profile.UserId.ToString(),
            session.Identity.Profile.KeyStorageProvider);
        IdentityPublicKey = session.Identity.Profile.PublicKeyBase64;

        Versions.Clear();
        foreach (var version in workspace.Versions
                     .OrderByDescending(static value => value.Version.CreatedUtc)
                     .Select(MapVersionCard))
        {
            Versions.Add(version);
        }

        AvailableItemTypes.Clear();
        foreach (var itemTypeId in workspace.Project.ItemTypes.Keys.OrderBy(static value => value, StringComparer.Ordinal))
        {
            AvailableItemTypes.Add(itemTypeId);
        }

        AvailableCategories.Clear();
        foreach (var categoryId in workspace.Project.DefaultCategories.Select(static category => category.Id))
        {
            AvailableCategories.Add(categoryId);
        }

        Members.Clear();
        foreach (var member in workspace.Members.Members
                     .Select(member => new WorkspaceMemberCard(
                         member.UserId,
                         member.DisplayName,
                         member.PublicKey,
                         member.Role,
                         member.IsActive,
                         member.UserId == session.Identity.Profile.UserId)))
        {
            Members.Add(member);
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
        WorkspaceMessage = string.Empty;
        ChangelogPreview = string.Empty;
        LastChangelogExportPath = string.Empty;

        if (previousSelectedVersionId is Guid selectedVersionId)
        {
            SelectedVersion = Versions.FirstOrDefault(version => version.VersionId == selectedVersionId) ?? Versions.FirstOrDefault();
        }
        else
        {
            SelectedVersion = Versions.FirstOrDefault();
        }

        if (SelectedVersion is null)
        {
            ClearVersionEditor();
            ClearItemEditorForNewItem();
        }

        SelectedMember = Members.FirstOrDefault(member => member.IsCurrentIdentity) ?? Members.FirstOrDefault();
        if (SelectedMember is null)
        {
            ClearMemberEditor();
        }

        NewVersionName = NextSuggestedVersionName();
        OnPropertyChanged(nameof(TrustBadge));
        OnPropertyChanged(nameof(IdentityId));
        OnPropertyChanged(nameof(IdentityBundle));
        OnPropertyChanged(nameof(CanManageMembers));
        OnPropertyChanged(nameof(CanEditSelectedMember));
        OnPropertyChanged(nameof(SelectedMemberStateSummary));
    }

    private void ApplySetupState(string message)
    {
        _currentSession = null;
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
        AvailableItemTypes.Clear();
        AvailableCategories.Clear();
        Members.Clear();
        SelectedVersion = null;
        SelectedItem = null;
        SelectedMember = null;
        WorkspaceMessage = string.Empty;
        ChangelogPreview = string.Empty;
        LastChangelogExportPath = string.Empty;
        IdentityPublicKey = string.Empty;
        ClearInviteEditor();
        ClearMemberEditor();
        HasActiveSession = false;
        OnPropertyChanged(nameof(TrustBadge));
        OnPropertyChanged(nameof(IdentityId));
        OnPropertyChanged(nameof(IdentityBundle));
        OnPropertyChanged(nameof(CanEditSelectedVersion));
        OnPropertyChanged(nameof(CanEditItems));
        OnPropertyChanged(nameof(CanReleaseSelectedVersion));
        OnPropertyChanged(nameof(SelectedVersionStateSummary));
        OnPropertyChanged(nameof(CanManageMembers));
        OnPropertyChanged(nameof(CanEditSelectedMember));
        OnPropertyChanged(nameof(SelectedMemberStateSummary));
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

    private void PopulateVersionEditor()
    {
        if (SelectedVersion is null)
        {
            ClearVersionEditor();
            return;
        }

        VersionEditorName = SelectedVersion.Name;
        VersionEditorNotes = SelectedVersion.Notes ?? string.Empty;
        VersionEditorStatus = SelectedVersion.Status;
    }

    private void PopulateItemEditor()
    {
        if (SelectedItem is null)
        {
            ClearItemEditorForNewItem();
            return;
        }

        ItemEditorTitle = SelectedItem.Title;
        ItemEditorDescription = SelectedItem.Description ?? string.Empty;
        ItemEditorIsDone = SelectedItem.IsDone;
        SelectedItemTypeId = SelectedItem.ItemTypeId;
        SelectedCategoryId = SelectedItem.CategoryId;
    }

    private void ClearVersionEditor()
    {
        VersionEditorName = string.Empty;
        VersionEditorNotes = string.Empty;
        VersionEditorStatus = ReleaseStatus.InProgress;
    }

    private void ClearItemEditorForNewItem()
    {
        SelectedItem = null;
        ItemEditorTitle = string.Empty;
        ItemEditorDescription = string.Empty;
        ItemEditorIsDone = false;
        SelectedItemTypeId = AvailableItemTypes.FirstOrDefault() ?? "feature";
        SelectedCategoryId = AvailableCategories.FirstOrDefault() ?? "added";
    }

    private void PopulateMemberEditor()
    {
        if (SelectedMember is null)
        {
            ClearMemberEditor();
            return;
        }

        MemberEditorDisplayName = SelectedMember.DisplayName;
        MemberEditorRole = SelectedMember.Role;
        MemberEditorIsActive = SelectedMember.IsActive;
    }

    private void ClearInviteEditor()
    {
        InviteUserId = string.Empty;
        InviteDisplayName = string.Empty;
        InvitePublicKey = string.Empty;
        InviteRole = MemberRole.Editor;
    }

    private void ClearMemberEditor()
    {
        MemberEditorDisplayName = string.Empty;
        MemberEditorRole = MemberRole.Editor;
        MemberEditorIsActive = true;
    }

    private void ReselectVersion(Guid versionId)
    {
        SelectedVersion = Versions.FirstOrDefault(version => version.VersionId == versionId);
    }

    private void ReselectItem(Guid itemId)
    {
        SelectedItem = SelectedVersion?.Items.FirstOrDefault(item => item.ItemId == itemId);
    }

    private void ReselectMember(Guid userId)
    {
        SelectedMember = Members.FirstOrDefault(member => member.UserId == userId);
    }

    private string NextSuggestedVersionName()
    {
        if (!Versions.Any())
        {
            return "1.0.0";
        }

        var latestVersion = Versions
            .Select(version => version.Name)
            .FirstOrDefault(name => Version.TryParse(name, out _));

        if (latestVersion is null || !Version.TryParse(latestVersion, out var parsed))
        {
            return "1.0.0";
        }

        return $"{parsed.Major}.{parsed.Minor + 1}.0";
    }

    private static WorkspaceVersionCard MapVersionCard(VersionWorkspaceSnapshot snapshot) =>
        new(
            snapshot.Version.VersionId,
            snapshot.Version.Name,
            snapshot.Version.Status,
            snapshot.Version.Notes,
            snapshot.Items.Count,
            snapshot.Items.Count(static item => item.IsDone),
            snapshot.Items
                .OrderBy(static item => item.CreatedUtc)
                .Select(static item => new WorkspaceItemCard(
                    item.ItemId,
                    item.ItemKey,
                    item.ItemKeyTypeId,
                    item.CategoryId,
                    item.Title,
                    item.Description,
                    item.IsDone))
                .ToArray());

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
                        },
                        new Dictionary<string, ItemKeyRule>(StringComparer.Ordinal)
                        {
                            ["feature"] = new("BP", ItemKeyScope.Version),
                            ["bug"] = new("BUG", ItemKeyScope.Project),
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
                                "Demo workspace",
                                []),
                            [
                                new ItemDocument(
                                    1,
                                    projectId,
                                    versionId,
                                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                                    "BP-1001",
                                    "feature",
                                    "added",
                                    "Starter item",
                                    "Seeded for design preview.",
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
