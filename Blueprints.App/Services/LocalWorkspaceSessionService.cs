using Blueprints.App.Models;
using Blueprints.Collaboration.Enums;
using Blueprints.Collaboration.Models;
using Blueprints.Collaboration.Services;
using Blueprints.Security.Models;
using Blueprints.Storage.Services;

namespace Blueprints.App.Services;

public sealed class LocalWorkspaceSessionService
{
    private readonly LocalWorkspaceService _workspaceService;
    private readonly FileSystemSyncStateStore _syncStateStore;
    private readonly WorkspaceSyncAnalyzer _syncAnalyzer;

    public LocalWorkspaceSessionService(
        LocalWorkspaceService workspaceService,
        FileSystemSyncStateStore syncStateStore,
        WorkspaceSyncAnalyzer syncAnalyzer)
    {
        _workspaceService = workspaceService;
        _syncStateStore = syncStateStore;
        _syncAnalyzer = syncAnalyzer;
    }

    public LocalWorkspaceSession GetOrCreateDefaultSession(
        StoredIdentity identity,
        string sharedWorkspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWorkspaceRoot);

        Directory.CreateDirectory(sharedWorkspaceRoot);

        var workspaceSession = _workspaceService.GetOrCreateDefaultWorkspace(identity);
        var paths = WorkspacePathResolver.Create(
            workspaceSession.Paths.LocalWorkspaceRoot,
            sharedWorkspaceRoot);

        var syncState = _syncStateStore.Load(paths.LocalWorkspaceRoot);
        var analysis = _syncAnalyzer.Analyze(paths, syncState.TrackedEntries);
        var sync = new SyncSummary(
            DetermineHealth(analysis),
            analysis.OutgoingDocumentPaths.Count,
            analysis.IncomingDocumentPaths.Count,
            analysis.PotentialConflictDocumentPaths.Count);

        return workspaceSession with
        {
            Paths = paths,
            Sync = sync,
        };
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
}
