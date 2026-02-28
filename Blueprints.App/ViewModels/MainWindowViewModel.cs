using System.Collections.ObjectModel;
using System.Linq;
using Blueprints.App.Models;
using Blueprints.Collaboration.Enums;
using Blueprints.Collaboration.Models;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Models;

namespace Blueprints.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
        : this(CreateDesignSession())
    {
    }

    public MainWindowViewModel(LocalWorkspaceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
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

        Sync = new SyncSummary(
            SyncHealth.Idle,
            0,
            0,
            ConflictCount: 0);

        Versions = new ObservableCollection<VersionSummary>(
            workspace.Versions
                .OrderByDescending(static version => version.Version.CreatedUtc)
                .Select(static version => new VersionSummary(
                    version.Version.Name,
                    version.Version.Status,
                    version.Items.Count,
                    version.Items.Count(static item => item.IsDone))));

        TrustSummary = session.LoadResult.TrustReport.Summary;
        WorkspacePath = session.Paths.LocalWorkspaceRoot;
        SharedSyncPath = session.Paths.SharedProjectRoot;
        VersioningScheme = project.VersioningScheme;
        VersionCount = workspace.Versions.Count;
        ItemCount = workspace.Versions.Sum(static version => version.Items.Count);
        ActiveMemberCount = workspace.Members.Members.Count(static member => member.IsActive);
        MembershipRevision = workspace.Members.MembershipRevision;
        Sync = session.Sync;
    }

    public string Title => $"{CurrentProject.Name} ({CurrentProject.Code})";

    public ProjectSummary CurrentProject { get; }

    public IdentitySummary Identity { get; }

    public string IdentityId => Identity.UserId;

    public SyncSummary Sync { get; private set; }

    public ObservableCollection<VersionSummary> Versions { get; }

    public string TrustSummary { get; }

    public string WorkspacePath { get; }

    public string VersioningScheme { get; }

    public string SharedSyncPath { get; }

    public int VersionCount { get; }

    public int ItemCount { get; }

    public int ActiveMemberCount { get; }

    public int MembershipRevision { get; }

    public string TrustBadge => TrustStatePresenter.ToDisplayText(CurrentProject.TrustState);

    public string SyncStatus =>
        Sync.Health switch
        {
            SyncHealth.Ready => $"{Sync.PendingOutgoingChanges} outgoing, {Sync.PendingIncomingChanges} incoming",
            SyncHealth.NeedsAttention => $"{Sync.ConflictCount} conflicts need attention",
            SyncHealth.Idle => "Sync baseline is current",
            _ => "Sync unavailable",
        };

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
                @"C:\Users\Example\AppData\Local\Blueprints\Workspace\default",
                @"C:\Users\Example\AppData\Local\Blueprints\Shared\default"),
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
