using System.Collections.ObjectModel;
using Blueprints.Collaboration.Enums;
using Blueprints.Collaboration.Models;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Security.Models;
using Blueprints.Security.Services;

namespace Blueprints.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        CurrentProject = new ProjectSummary(
            "VaultSync",
            "VS",
            TrustState.Trusted,
            @"\\NAS\Blueprints\VaultSync");

        Identity = new IdentitySummary(
            "Flavio",
            "b2d53ce4-f2e2-4ec5-b0ed-31fd2049d7f8",
            "DPAPI");

        Sync = new SyncSummary(
            SyncHealth.Ready,
            PendingOutgoingChanges: 3,
            PendingIncomingChanges: 1,
            ConflictCount: 0);

        Versions = new ObservableCollection<VersionSummary>
        {
            new("1.6.0", ReleaseStatus.InProgress, 14, 9),
            new("1.5.0", ReleaseStatus.Frozen, 22, 22),
            new("1.4.3", ReleaseStatus.Released, 8, 8),
        };
    }

    public string Title => $"{CurrentProject.Name} ({CurrentProject.Code})";

    public ProjectSummary CurrentProject { get; }

    public IdentitySummary Identity { get; }

    public SyncSummary Sync { get; }

    public ObservableCollection<VersionSummary> Versions { get; }

    public string TrustBadge => TrustStatePresenter.ToDisplayText(CurrentProject.TrustState);

    public string SyncStatus =>
        Sync.Health switch
        {
            SyncHealth.Ready => $"{Sync.PendingOutgoingChanges} outgoing, {Sync.PendingIncomingChanges} incoming",
            SyncHealth.NeedsAttention => $"{Sync.ConflictCount} conflicts need attention",
            _ => "Idle",
        };
}
