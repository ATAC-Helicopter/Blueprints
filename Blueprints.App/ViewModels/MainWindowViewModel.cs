using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
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
    private readonly IntegrationStatusService _integrationStatusService;
    private readonly ISourceDiscoveryService _sourceDiscoveryService;
    private readonly FileSystemCanvasViewStateStore _canvasViewStateStore;
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
    private string _gitChangelogSummary = string.Empty;
    private string _identityPublicKey = string.Empty;
    private WorkspaceMemberCard? _selectedMember;
    private string _inviteUserId = string.Empty;
    private string _inviteDisplayName = string.Empty;
    private string _invitePublicKey = string.Empty;
    private MemberRole _inviteRole = MemberRole.Editor;
    private string _memberEditorDisplayName = string.Empty;
    private MemberRole _memberEditorRole = MemberRole.Editor;
    private bool _memberEditorIsActive = true;
    private string? _selectedConflictPath;
    private SyncDiagnosticCard? _selectedSyncDiagnostic;
    private string _selectedConflictSemanticSummary = string.Empty;
    private string _selectedConflictLocalPreview = string.Empty;
    private string _selectedConflictSharedPreview = string.Empty;
    private string _localGitRepositoryPath = string.Empty;
    private string _integrationMessage = string.Empty;
    private WorkspaceSection _selectedWorkspaceSection = WorkspaceSection.Overview;
    private CanvasLayoutDocument? _canvasLayout;
    private CanvasViewState _canvasViewState = CanvasViewState.Default;
    private SourceImportProposal? _selectedSourceProposal;
    private string _sourceDiscoverySummary = "Connect a repository, then scan its planning sources.";
    private string _sourceDiscoveryWarnings = string.Empty;
    private bool _isDiscoveringSources;

    public MainWindowViewModel()
    {
        Versions = new ObservableCollection<WorkspaceVersionCard>();
        AvailableItemTypes = new ObservableCollection<string>();
        AvailableCategories = new ObservableCollection<string>();
        RecentProjects = new ObservableCollection<RecentProjectReference>();
        Members = new ObservableCollection<WorkspaceMemberCard>();
        Conflicts = new ObservableCollection<string>();
        SyncDiagnostics = new ObservableCollection<SyncDiagnosticCard>();
        TrustDiagnostics = new ObservableCollection<TrustDiagnosticCard>();
        Integrations = new ObservableCollection<IntegrationStatusCard>();
        VersionSourceChangeDiagnostics = new ObservableCollection<VersionSourceChangeDiagnostic>();
        _integrationStatusService = new IntegrationStatusService();
        _sourceDiscoveryService = new RepositorySourceDiscoveryService();
        _canvasViewStateStore = new FileSystemCanvasViewStateStore();
        SourceImportProposals = new ObservableCollection<SourceImportProposal>();
        ApplyDesignSession(CreateDesignSession());
        ApplyDesignSourceProposals();
        RefreshIntegrations();
    }

    public MainWindowViewModel(
        ProjectWorkspaceCoordinatorService coordinatorService,
        IntegrationStatusService integrationStatusService,
        ISourceDiscoveryService? sourceDiscoveryService = null)
    {
        _coordinatorService = coordinatorService;
        _integrationStatusService = integrationStatusService;
        _sourceDiscoveryService = sourceDiscoveryService ?? new RepositorySourceDiscoveryService();
        _canvasViewStateStore = new FileSystemCanvasViewStateStore();
        Versions = new ObservableCollection<WorkspaceVersionCard>();
        AvailableItemTypes = new ObservableCollection<string>();
        AvailableCategories = new ObservableCollection<string>();
        RecentProjects = new ObservableCollection<RecentProjectReference>();
        Members = new ObservableCollection<WorkspaceMemberCard>();
        Conflicts = new ObservableCollection<string>();
        SyncDiagnostics = new ObservableCollection<SyncDiagnosticCard>();
        TrustDiagnostics = new ObservableCollection<TrustDiagnosticCard>();
        Integrations = new ObservableCollection<IntegrationStatusCard>();
        VersionSourceChangeDiagnostics = new ObservableCollection<VersionSourceChangeDiagnostic>();
        SourceImportProposals = new ObservableCollection<SourceImportProposal>();

        RefreshRecentProjects();
        RefreshSuggestedPaths();
        RefreshIntegrations();
        ApplySetupState("Create a new project or open an existing workspace.");
    }

    public ObservableCollection<WorkspaceVersionCard> Versions { get; }

    public ObservableCollection<string> AvailableItemTypes { get; }

    public ObservableCollection<string> AvailableCategories { get; }

    public ObservableCollection<RecentProjectReference> RecentProjects { get; }

    public ObservableCollection<WorkspaceMemberCard> Members { get; }

    public ObservableCollection<string> Conflicts { get; }

    public ObservableCollection<SyncDiagnosticCard> SyncDiagnostics { get; }

    public ObservableCollection<TrustDiagnosticCard> TrustDiagnostics { get; }

    public ObservableCollection<IntegrationStatusCard> Integrations { get; }

    public ObservableCollection<VersionSourceChangeDiagnostic> VersionSourceChangeDiagnostics { get; }

    public ObservableCollection<SourceImportProposal> SourceImportProposals { get; }

    public SourceImportProposal? SelectedSourceProposal
    {
        get => _selectedSourceProposal;
        set
        {
            if (SetProperty(ref _selectedSourceProposal, value))
            {
                OnPropertyChanged(nameof(HasSelectedSourceProposal));
            }
        }
    }

    public bool HasSelectedSourceProposal => SelectedSourceProposal is not null;

    public string SourceDiscoverySummary
    {
        get => _sourceDiscoverySummary;
        private set => SetProperty(ref _sourceDiscoverySummary, value);
    }

    public string SourceDiscoveryWarnings
    {
        get => _sourceDiscoveryWarnings;
        private set
        {
            if (SetProperty(ref _sourceDiscoveryWarnings, value))
            {
                OnPropertyChanged(nameof(HasSourceDiscoveryWarnings));
            }
        }
    }

    public bool HasSourceDiscoveryWarnings => !string.IsNullOrWhiteSpace(SourceDiscoveryWarnings);

    public bool IsDiscoveringSources
    {
        get => _isDiscoveringSources;
        private set
        {
            if (SetProperty(ref _isDiscoveringSources, value))
            {
                OnPropertyChanged(nameof(CanDiscoverSources));
            }
        }
    }

    public bool CanDiscoverSources =>
        !IsDiscoveringSources && !string.IsNullOrWhiteSpace(LocalGitRepositoryPath);

    public bool HasSourceProposals => SourceImportProposals.Count > 0;

    public int ApprovedSourceProposalCount =>
        SourceImportProposals.Count(static proposal => proposal.IsIncluded);

    public string SourceApprovalSummary =>
        HasSourceProposals
            ? $"{ApprovedSourceProposalCount} of {SourceImportProposals.Count} proposals approved"
            : "Nothing is queued for approval.";

    public bool CanApplyApprovedSourceProposals =>
        CanMutateWorkspace &&
        ApprovedSourceProposalCount > 0 &&
        SourceImportProposals
            .Where(static proposal => proposal.IsIncluded)
            .All(static proposal =>
                proposal.TargetVersion is not null &&
                !string.IsNullOrWhiteSpace(proposal.Title));

    public string LocalGitRepositoryPath
    {
        get => _localGitRepositoryPath;
        set
        {
            if (SetProperty(ref _localGitRepositoryPath, value))
            {
                OnPropertyChanged(nameof(CanDiscoverSources));
            }
        }
    }

    public string IntegrationMessage
    {
        get => _integrationMessage;
        private set => SetProperty(ref _integrationMessage, value);
    }

    public CanvasLayoutDocument? CanvasLayout
    {
        get => _canvasLayout;
        private set
        {
            if (SetProperty(ref _canvasLayout, value))
            {
                OnPropertyChanged(nameof(CanvasLayoutRevisionSummary));
            }
        }
    }

    public string CanvasLayoutRevisionSummary =>
        CanvasLayout is null
            ? "Layout has not been saved yet"
            : $"Shared layout revision {CanvasLayout.Revision}";

    public CanvasViewState CanvasViewState
    {
        get => _canvasViewState;
        private set => SetProperty(ref _canvasViewState, value);
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

    public WorkspaceSection SelectedWorkspaceSection
    {
        get => _selectedWorkspaceSection;
        private set
        {
            if (SetProperty(ref _selectedWorkspaceSection, value))
            {
                OnPropertyChanged(nameof(IsOverviewSelected));
                OnPropertyChanged(nameof(IsReleasesSelected));
                OnPropertyChanged(nameof(IsTeamSelected));
                OnPropertyChanged(nameof(IsSyncSelected));
                OnPropertyChanged(nameof(IsTrustSelected));
                OnPropertyChanged(nameof(IsIntegrationsSelected));
                OnPropertyChanged(nameof(IsDetailsWorkspaceSelected));
                OnPropertyChanged(nameof(SelectedWorkspaceSectionTitle));
                OnPropertyChanged(nameof(SelectedWorkspaceSectionDescription));
            }
        }
    }

    public bool IsOverviewSelected => SelectedWorkspaceSection == WorkspaceSection.Overview;

    public bool IsReleasesSelected => SelectedWorkspaceSection == WorkspaceSection.Releases;

    public bool IsTeamSelected => SelectedWorkspaceSection == WorkspaceSection.Team;

    public bool IsSyncSelected => SelectedWorkspaceSection == WorkspaceSection.Sync;

    public bool IsTrustSelected => SelectedWorkspaceSection == WorkspaceSection.Trust;

    public bool IsIntegrationsSelected => SelectedWorkspaceSection == WorkspaceSection.Integrations;

    public bool IsDetailsWorkspaceSelected => !IsOverviewSelected;

    public string SelectedWorkspaceSectionTitle =>
        SelectedWorkspaceSection switch
        {
            WorkspaceSection.Overview => "Project overview",
            WorkspaceSection.Releases => "Release drafting board",
            WorkspaceSection.Team => "Team and signing identities",
            WorkspaceSection.Sync => "Workspace exchange",
            WorkspaceSection.Trust => "Trust and audit",
            WorkspaceSection.Integrations => "Source Lens",
            _ => "Project overview",
        };

    public string SelectedWorkspaceSectionDescription =>
        SelectedWorkspaceSection switch
        {
            WorkspaceSection.Overview => "Read the project map, spot blockers, and choose the next action.",
            WorkspaceSection.Releases => "Plan versions, connect work items, preview notes, and mark milestones complete.",
            WorkspaceSection.Team => "Review signed membership and manage the people allowed to contribute.",
            WorkspaceSection.Sync => "Compare local and shared state before moving signed changes.",
            WorkspaceSection.Trust => "Inspect validation results, conflicts, and the audit boundary.",
            WorkspaceSection.Integrations => "Discover project signals, shape editable proposals, and approve exactly what enters the signed blueprint.",
            _ => string.Empty,
        };

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
                GitChangelogSummary = string.Empty;
                SelectedItem = value?.Items.FirstOrDefault();
                OnPropertyChanged(nameof(CanEditSelectedVersion));
                OnPropertyChanged(nameof(CanEditItems));
                OnPropertyChanged(nameof(CanReleaseSelectedVersion));
                OnPropertyChanged(nameof(SelectedVersionStateSummary));
                OnPropertyChanged(nameof(InspectorSelectionSummary));
                RefreshVersionSourceChangeDiagnostics();
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
                OnPropertyChanged(nameof(HasSelectedItem));
                OnPropertyChanged(nameof(InspectorSelectionSummary));
            }
        }
    }

    public bool HasSelectedItem => SelectedItem is not null;

    public string InspectorSelectionSummary =>
        SelectedItem is not null
            ? $"{SelectedItem.ItemKey} / {SelectedItem.ItemTypeId}"
            : SelectedVersion is not null
                ? $"VERSION / {SelectedVersion.Name}"
                : "SELECT A NODE";

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

    public string GitChangelogSummary
    {
        get => _gitChangelogSummary;
        private set => SetProperty(ref _gitChangelogSummary, value);
    }

    public string? SelectedConflictPath
    {
        get => _selectedConflictPath;
        set
        {
            if (SetProperty(ref _selectedConflictPath, value))
            {
                RefreshSelectedConflictPreview();
                OnPropertyChanged(nameof(CanResolveSelectedConflict));
            }
        }
    }

    public SyncDiagnosticCard? SelectedSyncDiagnostic
    {
        get => _selectedSyncDiagnostic;
        set
        {
            if (SetProperty(ref _selectedSyncDiagnostic, value) && value is not null)
            {
                SelectedConflictPath = value.Path;
            }
        }
    }

    public string SelectedConflictLocalPreview
    {
        get => _selectedConflictLocalPreview;
        private set => SetProperty(ref _selectedConflictLocalPreview, value);
    }

    public string SelectedConflictSemanticSummary
    {
        get => _selectedConflictSemanticSummary;
        private set => SetProperty(ref _selectedConflictSemanticSummary, value);
    }

    public string SelectedConflictSharedPreview
    {
        get => _selectedConflictSharedPreview;
        private set => SetProperty(ref _selectedConflictSharedPreview, value);
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
        CanMutateWorkspace &&
        SelectedVersion is not null &&
        SelectedVersion.Status is not ReleaseStatus.Frozen and not ReleaseStatus.Released;

    public bool CanEditItems =>
        CanMutateWorkspace &&
        SelectedVersion is not null &&
        SelectedVersion.Status is not ReleaseStatus.Frozen and not ReleaseStatus.Released;

    public bool CanReleaseSelectedVersion =>
        CanMutateWorkspace &&
        SelectedVersion is not null &&
        SelectedVersion.Status != ReleaseStatus.Released;

    public string SelectedVersionStateSummary =>
        !IsWorkspaceTrusted
            ? "This workspace is read-only because trust validation failed."
            : HasConflicts
                ? "Resolve sync conflicts before editing this version."
                : SelectedVersion?.Status switch
                {
                    ReleaseStatus.Frozen => "Frozen versions are read-only until they are explicitly released.",
                    ReleaseStatus.Released => "Released versions are immutable.",
                    _ when SelectedVersion is not null => "This version can still be edited.",
                    _ => "Select a version to manage release state.",
                };

    public string VersionSourceChangeSummary
    {
        get
        {
            if (SelectedVersion is null)
            {
                return "Select a version to review source changes.";
            }

            var sourceChanges = GetLocalGitRecentChanges();
            if (sourceChanges.Count == 0)
            {
                return "No Local Git changes are available for this version.";
            }

            var matchedCount = VersionSourceChangeDiagnostics.Count(static diagnostic => diagnostic.MatchesSelectedVersion);
            var unmatchedCount = VersionSourceChangeDiagnostics.Count - matchedCount;

            return $"{matchedCount} matched source changes, {unmatchedCount} unmatched recent changes.";
        }
    }

    public bool CanManageMembers =>
        CanMutateWorkspace &&
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

    public bool IsWorkspaceTrusted => CurrentProject.TrustState == TrustState.Trusted;

    public bool IsWorkspaceReadOnly => !IsWorkspaceTrusted;

    public bool HasConflicts => Conflicts.Count > 0;

    public bool CanMutateWorkspace =>
        IsWorkspaceTrusted && !HasConflicts;

    public bool CanResolveSelectedConflict =>
        HasConflicts && !string.IsNullOrWhiteSpace(SelectedConflictPath);

    public bool HasSyncDiagnostics => SyncDiagnostics.Count > 0;

    public bool HasTrustDiagnostics => TrustDiagnostics.Count > 0;

    public string WorkspaceModeSummary =>
        CurrentProject.TrustState switch
        {
            TrustState.Untrusted => "Workspace is untrusted. Editing is disabled until signed content is trusted again.",
            TrustState.Corrupt => "Workspace is corrupt. Editing is disabled until the workspace is repaired.",
            _ when HasConflicts => "Workspace has unresolved sync conflicts. Resolve them before editing.",
            _ => "Workspace is trusted and editable.",
        };

    public string AdaptiveGuidanceTitle =>
        !IsWorkspaceTrusted
            ? "Review trust before editing"
            : HasConflicts
                ? $"Resolve {Conflicts.Count} sync conflict{(Conflicts.Count == 1 ? string.Empty : "s")}"
                : Versions.Count == 0
                    ? "Create the first release node"
                    : HasSourceProposals
                        ? $"Review {SourceImportProposals.Count} source proposals"
                        : ItemCount == 0
                            ? "Connect work to the selected release"
                            : Sync.PendingOutgoingChanges > 0
                                ? $"Review {Sync.PendingOutgoingChanges} outgoing changes"
                                : "Shape the map around your next milestone";

    public string AdaptiveGuidanceDetail =>
        !IsWorkspaceTrusted
            ? "Blueprints has disabled mutations to protect signed project truth."
            : HasConflicts
                ? "Choose local or shared content for every conflict before continuing."
                : Versions.Count == 0
                    ? "A version gives imported and manual work a deliberate destination."
                    : HasSourceProposals
                        ? "Edit titles, targets, types, and completion state before approving anything."
                        : ItemCount == 0
                            ? "Add work manually or open Source Lens to discover it from project signals."
                            : Sync.PendingOutgoingChanges > 0
                                ? "Your local signed workspace has changes that have not been shared."
                                : "Drag nodes, inspect relationships, and make the release plan reflect reality.";

    [RelayCommand]
    private void NavigateToOverview() =>
        SelectedWorkspaceSection = WorkspaceSection.Overview;

    [RelayCommand]
    private void NavigateToReleases() =>
        SelectedWorkspaceSection = WorkspaceSection.Releases;

    [RelayCommand]
    private void NavigateToTeam() =>
        SelectedWorkspaceSection = WorkspaceSection.Team;

    [RelayCommand]
    private void NavigateToSync() =>
        SelectedWorkspaceSection = WorkspaceSection.Sync;

    [RelayCommand]
    private void NavigateToTrust() =>
        SelectedWorkspaceSection = WorkspaceSection.Trust;

    [RelayCommand]
    private void NavigateToIntegrations() =>
        SelectedWorkspaceSection = WorkspaceSection.Integrations;

    [RelayCommand]
    private void SelectVersionNode(WorkspaceVersionCard? version)
    {
        if (version is null)
        {
            return;
        }

        SelectedVersion = version;
        SelectedItem = null;
        OnPropertyChanged(nameof(InspectorSelectionSummary));
    }

    [RelayCommand]
    private void SelectItemNode(WorkspaceItemCard? item)
    {
        if (item is null)
        {
            return;
        }

        var owningVersion = Versions.FirstOrDefault(version => version.Items.Any(candidate => candidate.ItemId == item.ItemId));
        if (owningVersion is not null && SelectedVersion?.VersionId != owningVersion.VersionId)
        {
            SelectedVersion = owningVersion;
        }

        SelectedItem = item;
        OnPropertyChanged(nameof(InspectorSelectionSummary));
    }

    [RelayCommand]
    private void BeginNewItem()
    {
        if (SelectedVersion is null)
        {
            WorkspaceMessage = "Select a version node before connecting a work item.";
            return;
        }

        ClearItemEditorForNewItem();
        WorkspaceMessage = $"Ready to connect a new item to {SelectedVersion.Name}.";
    }

    [RelayCommand]
    private void SaveCanvasLayout(CanvasLayoutEditRequest? request)
    {
        if (_coordinatorService is null || _currentSession is null || request is null)
        {
            return;
        }

        try
        {
            ApplySession(
                _coordinatorService.SaveCanvasLayout(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot,
                    request));
            WorkspaceMessage = CanvasLayout is null
                ? "Canvas layout saved."
                : $"Canvas layout revision {CanvasLayout.Revision} saved.";
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void SaveCanvasViewState(CanvasViewState? state)
    {
        if (state is null || string.IsNullOrWhiteSpace(WorkspacePath))
        {
            return;
        }

        _canvasViewStateStore.Save(WorkspacePath, state);
        CanvasViewState = state;
    }

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
            var sourceChanges = GetLocalGitRecentChanges();
            var export = _coordinatorService.ExportVersionChangelog(
                _currentSession.Paths.LocalWorkspaceRoot,
                _currentSession.Paths.SharedProjectRoot,
                SelectedVersion.VersionId,
                sourceChanges);
            ChangelogPreview = export.Markdown;
            LastChangelogExportPath = export.FilePath;
            GitChangelogSummary = sourceChanges.Count == 0
                ? "No Local Git changes were available for this changelog."
                : $"{sourceChanges.Count} Local Git changes considered for this changelog.";
            WorkspaceMessage = $"Exported changelog for {export.VersionName}.";
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void RefreshWorkspace()
    {
        if (_coordinatorService is null || _currentSession is null)
        {
            return;
        }

        try
        {
            ApplySession(
                _coordinatorService.RefreshProject(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot));
            WorkspaceMessage = "Workspace refreshed.";
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void SaveLocalGitRepositoryPath()
    {
        try
        {
            var settings = _integrationStatusService.GetSettings();
            _integrationStatusService.SaveSettings(settings with
            {
                LocalGitRepositoryPath = LocalGitRepositoryPath.Trim(),
            });
            RefreshIntegrations();
            IntegrationMessage = string.IsNullOrWhiteSpace(LocalGitRepositoryPath)
                ? "Local Git repository link cleared."
                : "Local Git repository link saved.";
        }
        catch (Exception exception)
        {
            IntegrationMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void RefreshIntegrationStatuses()
    {
        try
        {
            RefreshIntegrations();
            IntegrationMessage = "Integration statuses refreshed.";
        }
        catch (Exception exception)
        {
            IntegrationMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task DiscoverSources()
    {
        if (!CanDiscoverSources)
        {
            IntegrationMessage = "Link a local Git repository before scanning planning sources.";
            return;
        }

        IsDiscoveringSources = true;
        IntegrationMessage = "Reading changelogs, roadmaps, GitHub issues, and project links…";
        try
        {
            var result = await Task.Run(() => _sourceDiscoveryService.Discover(LocalGitRepositoryPath));
            PopulateSourceProposals(result);
            IntegrationMessage = result.Candidates.Count == 0
                ? "The scan completed, but no importable planning entries were found."
                : "Discovery is complete. Review, edit, and approve proposals before applying them.";
        }
        catch (Exception exception)
        {
            SourceDiscoverySummary = "Discovery could not be completed.";
            SourceDiscoveryWarnings = exception.Message;
            IntegrationMessage = exception.Message;
        }
        finally
        {
            IsDiscoveringSources = false;
        }
    }

    [RelayCommand]
    private void ApproveAllSourceProposals()
    {
        foreach (var proposal in SourceImportProposals)
        {
            proposal.IsIncluded = true;
        }

        RefreshSourceApprovalState();
    }

    [RelayCommand]
    private void ClearSourceProposalApprovals()
    {
        foreach (var proposal in SourceImportProposals)
        {
            proposal.IsIncluded = false;
        }

        RefreshSourceApprovalState();
    }

    [RelayCommand]
    private void ApplyApprovedSourceProposals()
    {
        if (_coordinatorService is null || _currentSession is null)
        {
            IntegrationMessage = "Open a project before applying source proposals.";
            return;
        }

        var approved = SourceImportProposals
            .Where(static proposal => proposal.IsIncluded)
            .ToArray();
        if (approved.Length == 0)
        {
            IntegrationMessage = "Approve at least one proposal before applying.";
            return;
        }

        if (approved.Any(static proposal =>
                proposal.TargetVersion is null ||
                string.IsNullOrWhiteSpace(proposal.Title)))
        {
            IntegrationMessage = "Every approved proposal needs a title and target version.";
            return;
        }

        try
        {
            var request = new ApprovedSourceImportRequest(
                approved
                    .Select(static proposal => new ApprovedSourceImportItem(
                        proposal.TargetVersion!.VersionId,
                        proposal.ItemTypeId,
                        proposal.CategoryId,
                        proposal.Title,
                        proposal.Description,
                        proposal.IsDone,
                        proposal.Kind,
                        proposal.SourceReference))
                    .ToArray());
            ApplySession(
                _coordinatorService.ApplyApprovedSourceImport(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot,
                    request));

            SourceImportProposals.Clear();
            SelectedSourceProposal = null;
            SourceDiscoverySummary = $"{approved.Length} approved proposals were added as signed work items.";
            SourceDiscoveryWarnings = string.Empty;
            RefreshSourceApprovalState();
            IntegrationMessage = $"Applied {approved.Length} approved proposals. No source system was modified.";
        }
        catch (Exception exception)
        {
            IntegrationMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void PushWorkspace()
    {
        if (_coordinatorService is null || _currentSession is null)
        {
            return;
        }

        try
        {
            var localRoot = _currentSession.Paths.LocalWorkspaceRoot;
            var sharedRoot = _currentSession.Paths.SharedProjectRoot;
            var result = _coordinatorService.PushWorkspace(localRoot, sharedRoot);
            ApplySession(_coordinatorService.RefreshProject(localRoot, sharedRoot));
            ApplySyncResultDiagnostics(result);
            WorkspaceMessage = result.Summary;
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void PullWorkspace()
    {
        if (_coordinatorService is null || _currentSession is null)
        {
            return;
        }

        try
        {
            var localRoot = _currentSession.Paths.LocalWorkspaceRoot;
            var sharedRoot = _currentSession.Paths.SharedProjectRoot;
            var result = _coordinatorService.PullWorkspace(localRoot, sharedRoot);
            ApplySession(_coordinatorService.RefreshProject(localRoot, sharedRoot));
            ApplySyncResultDiagnostics(result);
            WorkspaceMessage = result.Summary;
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void ResolveConflictKeepLocal() =>
        ResolveSelectedConflict(ConflictResolutionChoice.KeepLocal);

    [RelayCommand]
    private void ResolveConflictAcceptShared() =>
        ResolveSelectedConflict(ConflictResolutionChoice.AcceptShared);

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
        var wasActiveSession = HasActiveSession;
        _currentSession = session;

        var workspace = session.LoadResult.Workspace;
        var project = workspace.Project;
        var previousSelectedVersionId = SelectedVersion?.VersionId;

        CurrentProject = new ProjectSummary(
            project.Name,
            project.ProjectCode,
            session.LoadResult.TrustReport.State,
            session.Paths.SharedProjectRoot,
            project.ProjectId);

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

        Conflicts.Clear();
        foreach (var conflictPath in session.ConflictPaths)
        {
            Conflicts.Add(conflictPath);
        }

        SyncDiagnostics.Clear();
        foreach (var conflictPath in session.ConflictPaths)
        {
            SyncDiagnostics.Add(
                new SyncDiagnosticCard(
                    "Conflict",
                    "Workspace analysis",
                    conflictPath,
                    "Local and shared copies both changed since the last trusted sync baseline."));
        }

        TrustDiagnostics.Clear();
        foreach (var diagnostic in BuildTrustDiagnostics(session))
        {
            TrustDiagnostics.Add(diagnostic);
        }

        VersionSourceChangeDiagnostics.Clear();

        Title = $"{project.Name} ({project.ProjectCode})";
        TrustSummary = session.LoadResult.TrustReport.Summary;
        WorkspacePath = session.Paths.LocalWorkspaceRoot;
        CanvasViewState = _canvasViewStateStore.Load(session.Paths.LocalWorkspaceRoot);
        SharedSyncPath = session.Paths.SharedProjectRoot;
        VersioningScheme = project.VersioningScheme;
        VersionCount = workspace.Versions.Count;
        ItemCount = workspace.Versions.Sum(static version => version.Items.Count);
        ActiveMemberCount = workspace.Members.Members.Count(static member => member.IsActive);
        MembershipRevision = workspace.Members.MembershipRevision;
        CanvasLayout = workspace.CanvasLayout;
        Sync = session.Sync;
        HasActiveSession = true;
        WorkspaceMessage = string.Empty;
        ChangelogPreview = string.Empty;
        LastChangelogExportPath = string.Empty;
        GitChangelogSummary = string.Empty;
        if (!wasActiveSession)
        {
            SelectedWorkspaceSection = WorkspaceSection.Overview;
        }

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

        SelectedConflictPath = Conflicts.FirstOrDefault();
        SelectedSyncDiagnostic = SyncDiagnostics.FirstOrDefault();

        NewVersionName = NextSuggestedVersionName();
        OnPropertyChanged(nameof(TrustBadge));
        OnPropertyChanged(nameof(IdentityId));
        OnPropertyChanged(nameof(IdentityBundle));
        OnPropertyChanged(nameof(CanManageMembers));
        OnPropertyChanged(nameof(CanEditSelectedMember));
        OnPropertyChanged(nameof(SelectedMemberStateSummary));
        OnPropertyChanged(nameof(IsWorkspaceTrusted));
        OnPropertyChanged(nameof(IsWorkspaceReadOnly));
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(CanMutateWorkspace));
        OnPropertyChanged(nameof(WorkspaceModeSummary));
        OnPropertyChanged(nameof(CanResolveSelectedConflict));
        OnPropertyChanged(nameof(HasSyncDiagnostics));
        OnPropertyChanged(nameof(HasTrustDiagnostics));
        OnPropertyChanged(nameof(CanApplyApprovedSourceProposals));
        OnPropertyChanged(nameof(AdaptiveGuidanceTitle));
        OnPropertyChanged(nameof(AdaptiveGuidanceDetail));
        RefreshVersionSourceChangeDiagnostics();
    }

    private void ApplySetupState(string message)
    {
        _currentSession = null;
        Title = "Blueprints Setup";
        CurrentProject = new ProjectSummary(string.Empty, string.Empty, TrustState.Corrupt, string.Empty);
        TrustSummary = message;
        WorkspacePath = string.Empty;
        CanvasViewState = Blueprints.App.Models.CanvasViewState.Default;
        SharedSyncPath = string.Empty;
        VersioningScheme = string.Empty;
        VersionCount = 0;
        ItemCount = 0;
        ActiveMemberCount = 0;
        MembershipRevision = 0;
        CanvasLayout = null;
        Sync = new SyncSummary(SyncHealth.Idle, 0, 0, 0);
        Versions.Clear();
        AvailableItemTypes.Clear();
        AvailableCategories.Clear();
        Members.Clear();
        Conflicts.Clear();
        SyncDiagnostics.Clear();
        TrustDiagnostics.Clear();
        VersionSourceChangeDiagnostics.Clear();
        SourceImportProposals.Clear();
        SelectedSourceProposal = null;
        SourceDiscoverySummary = "Connect a repository, then scan its planning sources.";
        SourceDiscoveryWarnings = string.Empty;
        SelectedVersion = null;
        SelectedItem = null;
        SelectedMember = null;
        SelectedConflictPath = null;
        SelectedSyncDiagnostic = null;
        SelectedConflictSemanticSummary = string.Empty;
        SelectedConflictLocalPreview = string.Empty;
        SelectedConflictSharedPreview = string.Empty;
        WorkspaceMessage = string.Empty;
        ChangelogPreview = string.Empty;
        LastChangelogExportPath = string.Empty;
        GitChangelogSummary = string.Empty;
        IdentityPublicKey = string.Empty;
        SelectedWorkspaceSection = WorkspaceSection.Overview;
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
        OnPropertyChanged(nameof(IsWorkspaceTrusted));
        OnPropertyChanged(nameof(IsWorkspaceReadOnly));
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(CanMutateWorkspace));
        OnPropertyChanged(nameof(WorkspaceModeSummary));
        OnPropertyChanged(nameof(CanResolveSelectedConflict));
        OnPropertyChanged(nameof(HasSyncDiagnostics));
        OnPropertyChanged(nameof(HasTrustDiagnostics));
        OnPropertyChanged(nameof(VersionSourceChangeSummary));
        RefreshSourceApprovalState();
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

    private void RefreshIntegrations()
    {
        var settings = _integrationStatusService.GetSettings();
        LocalGitRepositoryPath = settings.LocalGitRepositoryPath;

        Integrations.Clear();
        foreach (var integration in _integrationStatusService.GetIntegrationStatuses())
        {
            Integrations.Add(integration);
        }

        RefreshVersionSourceChangeDiagnostics();
    }

    private void PopulateSourceProposals(SourceDiscoveryResult result)
    {
        SourceImportProposals.Clear();
        var targetVersion = SelectedVersion is { Status: not ReleaseStatus.Frozen and not ReleaseStatus.Released }
            ? SelectedVersion
            : Versions.FirstOrDefault(static version =>
                version.Status is not ReleaseStatus.Frozen and not ReleaseStatus.Released);
        var existingTitles = Versions
            .SelectMany(static version => version.Items)
            .Select(static item => NormalizeComparableTitle(item.Title))
            .ToHashSet(StringComparer.Ordinal);
        var defaultItemType = AvailableItemTypes.FirstOrDefault() ?? "feature";
        var defaultCategory = AvailableCategories.FirstOrDefault() ?? "added";

        foreach (var candidate in result.Candidates)
        {
            var compatibleCandidate = candidate with
            {
                SuggestedItemTypeId = AvailableItemTypes.Contains(candidate.SuggestedItemTypeId)
                    ? candidate.SuggestedItemTypeId
                    : defaultItemType,
                SuggestedCategoryId = AvailableCategories.Contains(candidate.SuggestedCategoryId)
                    ? candidate.SuggestedCategoryId
                    : defaultCategory,
            };
            var proposal = new SourceImportProposal(
                compatibleCandidate,
                targetVersion,
                existingTitles.Contains(NormalizeComparableTitle(candidate.Title)));
            proposal.PropertyChanged += SourceProposalPropertyChanged;
            SourceImportProposals.Add(proposal);
        }

        SelectedSourceProposal = SourceImportProposals.FirstOrDefault();
        SourceDiscoverySummary = result.Summary;
        SourceDiscoveryWarnings = string.Join(Environment.NewLine, result.Warnings);
        RefreshSourceApprovalState();
    }

    private void SourceProposalPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs) =>
        RefreshSourceApprovalState();

    private void RefreshSourceApprovalState()
    {
        OnPropertyChanged(nameof(HasSourceProposals));
        OnPropertyChanged(nameof(ApprovedSourceProposalCount));
        OnPropertyChanged(nameof(SourceApprovalSummary));
        OnPropertyChanged(nameof(CanApplyApprovedSourceProposals));
        OnPropertyChanged(nameof(AdaptiveGuidanceTitle));
        OnPropertyChanged(nameof(AdaptiveGuidanceDetail));
    }

    private static string NormalizeComparableTitle(string title) =>
        string.Join(
            ' ',
            title.Trim()
                .ToUpperInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private void ApplyDesignSourceProposals()
    {
        PopulateSourceProposals(
            new SourceDiscoveryResult(
                [
                    new SourceDiscoveryCandidate(
                        SourceArtifactKind.GitHubProject,
                        "Interactive dependency map",
                        "Let users connect work visually and reveal release blockers.",
                        "feature",
                        "added",
                        false,
                        "github:#42",
                        "Project: Canvas engine · Milestone: 0.2.0",
                        0.97),
                    new SourceDiscoveryCandidate(
                        SourceArtifactKind.Roadmap,
                        "Add undo and redo history",
                        "Imported from section “Canvas interaction”.",
                        "feature",
                        "added",
                        false,
                        "roadmap:Roadmap.md:88",
                        "Canvas interaction",
                        0.94),
                    new SourceDiscoveryCandidate(
                        SourceArtifactKind.Changelog,
                        "Signed shared canvas layouts",
                        "Imported from section “Unreleased”.",
                        "feature",
                        "changed",
                        true,
                        "changelog:CHANGELOG.md:12",
                        "Unreleased",
                        0.82),
                ],
                [],
                1,
                1,
                1,
                1));
    }

    private IReadOnlyList<SourceChangeSummary> GetLocalGitRecentChanges() =>
        Integrations.FirstOrDefault(static integration => integration.Provider == IntegrationProviderType.LocalGit)
            ?.RecentChanges
        ?? [];

    private void RefreshVersionSourceChangeDiagnostics()
    {
        VersionSourceChangeDiagnostics.Clear();

        foreach (var diagnostic in VersionSourceChangeDiagnosticBuilder.Build(SelectedVersion, GetLocalGitRecentChanges()))
        {
            VersionSourceChangeDiagnostics.Add(diagnostic);
        }

        OnPropertyChanged(nameof(VersionSourceChangeSummary));
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

    private void ResolveSelectedConflict(ConflictResolutionChoice choice)
    {
        if (_coordinatorService is null || _currentSession is null || string.IsNullOrWhiteSpace(SelectedConflictPath))
        {
            WorkspaceMessage = "Select a conflict first.";
            return;
        }

        try
        {
            var result = _coordinatorService.ResolveConflict(
                _currentSession.Paths.LocalWorkspaceRoot,
                _currentSession.Paths.SharedProjectRoot,
                SelectedConflictPath,
                choice);
            ApplySession(
                _coordinatorService.RefreshProject(
                    _currentSession.Paths.LocalWorkspaceRoot,
                    _currentSession.Paths.SharedProjectRoot));
            SelectedConflictPath = Conflicts.FirstOrDefault();
            WorkspaceMessage = result.Summary;
        }
        catch (Exception exception)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    private void ApplySyncResultDiagnostics(WorkspaceSyncResult result)
    {
        SyncDiagnostics.Clear();

        foreach (var path in result.Conflicts)
        {
            SyncDiagnostics.Add(
                new SyncDiagnosticCard(
                    result.Success ? "Info" : "Blocked",
                    result.Operation,
                    path,
                    result.Summary));
        }

        if (SyncDiagnostics.Count == 0)
        {
            foreach (var conflictPath in Conflicts)
            {
                SyncDiagnostics.Add(
                    new SyncDiagnosticCard(
                        "Conflict",
                        "Workspace analysis",
                        conflictPath,
                        "Local and shared copies both changed since the last trusted sync baseline."));
            }
        }

        SelectedSyncDiagnostic = SyncDiagnostics.FirstOrDefault(diagnostic =>
            string.Equals(diagnostic.Path, SelectedConflictPath, StringComparison.Ordinal))
            ?? SyncDiagnostics.FirstOrDefault();
        OnPropertyChanged(nameof(HasSyncDiagnostics));
    }

    private void RefreshSelectedConflictPreview()
    {
        if (_currentSession is null || string.IsNullOrWhiteSpace(SelectedConflictPath))
        {
            SelectedConflictSemanticSummary = "Select a diagnostic path to see the document summary.";
            SelectedConflictLocalPreview = "Select a diagnostic path to preview the local copy.";
            SelectedConflictSharedPreview = "Select a diagnostic path to preview the shared copy.";
            return;
        }

        var localPreview = ReadWorkspacePreview(
            _currentSession.Paths.LocalWorkspaceRoot,
            SelectedConflictPath,
            "Local copy is missing.");
        var sharedPreview = ReadWorkspacePreview(
            _currentSession.Paths.SharedProjectRoot,
            SelectedConflictPath,
            "Shared copy is missing.");

        SelectedConflictLocalPreview = localPreview;
        SelectedConflictSharedPreview = sharedPreview;
        SelectedConflictSemanticSummary = BuildSemanticConflictSummary(
            SelectedConflictPath,
            localPreview,
            sharedPreview);
    }

    private static string ReadWorkspacePreview(
        string root,
        string relativePath,
        string missingMessage)
    {
        var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedPath));
        var fullRoot = Path.GetFullPath(root);
        var relativeToRoot = Path.GetRelativePath(fullRoot, fullPath);
        if (relativeToRoot == ".." || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return "Preview blocked because the path is outside the workspace.";
        }

        if (!File.Exists(fullPath))
        {
            return missingMessage;
        }

        var text = File.ReadAllText(fullPath);
        const int maxPreviewLength = 8000;
        return text.Length <= maxPreviewLength
            ? text
            : text[..maxPreviewLength] + $"{Environment.NewLine}... preview truncated ...";
    }

    private static string BuildSemanticConflictSummary(
        string relativePath,
        string localJson,
        string sharedJson)
    {
        using var localDocument = TryParseJson(localJson);
        using var sharedDocument = TryParseJson(sharedJson);
        if (localDocument is null || sharedDocument is null)
        {
            return "Document summary unavailable because one side is missing or is not valid JSON.";
        }

        var fields = GetSemanticFields(relativePath);
        if (fields.Count == 0)
        {
            return "No semantic presenter exists for this document type yet. Use the raw local/shared previews below.";
        }

        var lines = new List<string>
        {
            $"Document: {GetDocumentKind(relativePath)}",
        };

        foreach (var (label, propertyPath) in fields)
        {
            var localValue = ReadJsonValue(localDocument.RootElement, propertyPath);
            var sharedValue = ReadJsonValue(sharedDocument.RootElement, propertyPath);
            var marker = string.Equals(localValue, sharedValue, StringComparison.Ordinal) ? "=" : "!=";
            lines.Add($"{label}: local {marker} shared");
            lines.Add($"  local: {localValue}");
            lines.Add($"  shared: {sharedValue}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static JsonDocument? TryParseJson(string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<(string Label, string PropertyPath)> GetSemanticFields(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        if (string.Equals(normalizedPath, "project/project.json", StringComparison.Ordinal))
        {
            return
            [
                ("Name", "name"),
                ("Project code", "projectCode"),
                ("Versioning scheme", "versioningScheme"),
                ("Categories", "defaultCategories"),
                ("Item types", "itemTypes"),
                ("Item key rules", "itemKeyRules"),
            ];
        }

        if (string.Equals(normalizedPath, "project/members.json", StringComparison.Ordinal))
        {
            return
            [
                ("Membership revision", "membershipRevision"),
                ("Members", "members"),
            ];
        }

        if (normalizedPath.StartsWith("versions/", StringComparison.Ordinal) &&
            normalizedPath.EndsWith("/version.json", StringComparison.Ordinal))
        {
            return
            [
                ("Name", "name"),
                ("Status", "status"),
                ("Released UTC", "releasedUtc"),
                ("Notes", "notes"),
                ("Manual order", "manualOrder"),
            ];
        }

        if (normalizedPath.StartsWith("versions/", StringComparison.Ordinal) &&
            normalizedPath.Contains("/items/", StringComparison.Ordinal) &&
            normalizedPath.EndsWith(".json", StringComparison.Ordinal))
        {
            return
            [
                ("Key", "itemKey"),
                ("Title", "title"),
                ("Category", "categoryId"),
                ("Type", "itemKeyTypeId"),
                ("Done", "isDone"),
                ("Description", "description"),
                ("Tags", "tags"),
                ("Last modified by", "lastModifiedByName"),
            ];
        }

        if (normalizedPath.StartsWith("log/", StringComparison.Ordinal) &&
            normalizedPath.EndsWith(".json", StringComparison.Ordinal))
        {
            return
            [
                ("Operation", "operation"),
                ("Summary", "summary"),
                ("Timestamp UTC", "timestampUtc"),
                ("Author", "authorDisplayName"),
                ("Membership revision seen", "membershipRevisionSeen"),
                ("Previous entry hash", "previousEntryHash"),
            ];
        }

        return [];
    }

    private static string GetDocumentKind(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        if (string.Equals(normalizedPath, "project/project.json", StringComparison.Ordinal))
        {
            return "Project configuration";
        }

        if (string.Equals(normalizedPath, "project/members.json", StringComparison.Ordinal))
        {
            return "Membership";
        }

        if (normalizedPath.StartsWith("versions/", StringComparison.Ordinal) &&
            normalizedPath.EndsWith("/version.json", StringComparison.Ordinal))
        {
            return "Version";
        }

        if (normalizedPath.StartsWith("versions/", StringComparison.Ordinal) &&
            normalizedPath.Contains("/items/", StringComparison.Ordinal))
        {
            return "Release item";
        }

        if (normalizedPath.StartsWith("log/", StringComparison.Ordinal))
        {
            return "Audit log entry";
        }

        return "Signed document";
    }

    private static string ReadJsonValue(JsonElement root, string propertyPath)
    {
        var current = root;
        foreach (var part in propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
            {
                return "(missing)";
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.Null => "(null)",
            JsonValueKind.String => current.GetString() ?? string.Empty,
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => $"{current.GetArrayLength()} entries",
            JsonValueKind.Object => $"{current.EnumerateObject().Count()} fields",
            _ => current.GetRawText(),
        };
    }

    private static IReadOnlyList<TrustDiagnosticCard> BuildTrustDiagnostics(LocalWorkspaceSession session)
    {
        var diagnostics = new List<TrustDiagnosticCard>
        {
            new(
                session.LoadResult.TrustReport.State == TrustState.Trusted ? "Ok" : "Blocked",
                "Workspace trust",
                session.LoadResult.TrustReport.Summary,
                session.LoadResult.TrustReport.State == TrustState.Trusted
                    ? "Workspace mutations are allowed while signatures, audit continuity, and safety checks remain valid."
                    : "Treat this workspace as read-only. Recheck trust after restoring signed files from a known-good local or shared copy."),
        };

        diagnostics.Add(
            new TrustDiagnosticCard(
                session.AuditLogValidation.IsValid ? "Ok" : "Blocked",
                "Audit chain",
                session.AuditLogValidation.Summary,
                session.AuditLogValidation.IsValid
                    ? $"Audit chain contains {session.AuditLogValidation.EntryCount} signed entries."
                    : BuildAuditRecoveryGuidance(session.AuditLogValidation.InvalidEntryPaths)));

        if (session.SharedFolderSafety.Findings.Count == 0)
        {
            diagnostics.Add(
                new TrustDiagnosticCard(
                    "Ok",
                    "Shared folder",
                    "No shared-folder safety findings.",
                    "Keep the shared folder separate from the local workspace and limit write access to trusted collaborators."));
        }
        else
        {
            diagnostics.AddRange(
                session.SharedFolderSafety.Findings.Select(finding =>
                    new TrustDiagnosticCard(
                        finding.Severity,
                        $"Shared folder: {finding.Code}",
                        finding.Message,
                        BuildSharedFolderGuidance(finding))));
        }

        if (session.ConflictPaths.Count > 0)
        {
            diagnostics.Add(
                new TrustDiagnosticCard(
                    "Blocked",
                    "Sync conflicts",
                    $"{session.ConflictPaths.Count} sync conflicts require resolution.",
                    "Open the Sync tab, inspect the semantic preview, then keep local or accept shared for each conflict."));
        }

        return diagnostics;
    }

    private static string BuildAuditRecoveryGuidance(IReadOnlyList<string> invalidEntryPaths)
    {
        if (invalidEntryPaths.Count == 0)
        {
            return "Audit validation failed without a specific path. Restore the log folder from a known-good copy before editing.";
        }

        return "Invalid entries: "
            + string.Join(", ", invalidEntryPaths)
            + ". Restore these entries and their signatures from a known-good copy, then recheck trust.";
    }

    private static string BuildSharedFolderGuidance(SharedFolderSafetyFinding finding) =>
        finding.Code switch
        {
            "path-overlap" => "Choose a shared sync folder outside the local workspace. The shared folder is an exchange area, not the live editing workspace.",
            "missing-folder" => "Create the shared folder or choose an existing sync location before relying on collaboration state.",
            "acl-check-unavailable" => "On Linux/macOS this warning is expected. Keep filesystem permissions narrow and avoid public writable folders.",
            "broad-write-acl" => "Restrict write permissions to trusted collaborators before using this folder for signed exchange.",
            "acl-check-failed" => "Inspect folder permissions manually and rerun trust checks after access issues are fixed.",
            _ => "Review this finding before pushing or pulling shared changes.",
        };

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
                    "Local key protector",
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
            new SyncSummary(SyncHealth.Ready, 3, 0, 0),
            [],
            new AuditLogValidationResult(true, 4, "design-hash", [], "Validated 4 audit entries."),
            new SharedFolderSafetyReport(
                true,
                [
                    new SharedFolderSafetyFinding(
                        "acl-check-unavailable",
                        "Warning",
                        "Windows ACL safety checks are unavailable on this operating system."),
                ]));
    }
}
