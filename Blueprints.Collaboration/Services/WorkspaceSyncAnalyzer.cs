using Blueprints.Collaboration.Models;
using Blueprints.Storage.Models;

namespace Blueprints.Collaboration.Services;

public sealed class WorkspaceSyncAnalyzer
{
    private readonly WorkspaceExchangeSnapshotBuilder _snapshotBuilder;

    public WorkspaceSyncAnalyzer(WorkspaceExchangeSnapshotBuilder snapshotBuilder)
    {
        _snapshotBuilder = snapshotBuilder;
    }

    public WorkspaceSyncAnalysis Analyze(WorkspacePaths workspacePaths)
    {
        ArgumentNullException.ThrowIfNull(workspacePaths);

        var localEntries = _snapshotBuilder.Build(workspacePaths.LocalWorkspaceRoot)
            .ToDictionary(static entry => entry.DocumentPath, StringComparer.Ordinal);
        var sharedEntries = _snapshotBuilder.Build(workspacePaths.SharedProjectRoot)
            .ToDictionary(static entry => entry.DocumentPath, StringComparer.Ordinal);
        var baselineEntries = Array.Empty<SyncTrackedEntry>()
            .ToDictionary(static entry => entry.DocumentPath, StringComparer.Ordinal);

        return Analyze(localEntries, sharedEntries, baselineEntries);
    }

    public WorkspaceSyncAnalysis Analyze(
        WorkspacePaths workspacePaths,
        IReadOnlyList<SyncTrackedEntry> baselineEntries)
    {
        ArgumentNullException.ThrowIfNull(workspacePaths);
        ArgumentNullException.ThrowIfNull(baselineEntries);

        var localEntries = _snapshotBuilder.Build(workspacePaths.LocalWorkspaceRoot)
            .ToDictionary(static entry => entry.DocumentPath, StringComparer.Ordinal);
        var sharedEntries = _snapshotBuilder.Build(workspacePaths.SharedProjectRoot)
            .ToDictionary(static entry => entry.DocumentPath, StringComparer.Ordinal);
        var trackedEntries = baselineEntries.ToDictionary(static entry => entry.DocumentPath, StringComparer.Ordinal);

        return Analyze(localEntries, sharedEntries, trackedEntries);
    }

    private static WorkspaceSyncAnalysis Analyze(
        IReadOnlyDictionary<string, SyncManifestEntry> localEntries,
        IReadOnlyDictionary<string, SyncManifestEntry> sharedEntries,
        IReadOnlyDictionary<string, SyncTrackedEntry> baselineEntries)
    {

        var outgoing = new List<string>();
        var incoming = new List<string>();
        var conflicts = new List<string>();

        foreach (var documentPath in localEntries.Keys
                     .Union(sharedEntries.Keys, StringComparer.Ordinal)
                     .Union(baselineEntries.Keys, StringComparer.Ordinal)
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            var hasLocal = localEntries.TryGetValue(documentPath, out var localEntry);
            var hasShared = sharedEntries.TryGetValue(documentPath, out var sharedEntry);
            var hasBaseline = baselineEntries.TryGetValue(documentPath, out var baselineEntry);

            if (!hasBaseline)
            {
                if (hasLocal && !hasShared)
                {
                    outgoing.Add(documentPath);
                    continue;
                }

                if (!hasLocal && hasShared)
                {
                    incoming.Add(documentPath);
                    continue;
                }

                if (hasLocal && hasShared)
                {
                    if (!Matches(localEntry, sharedEntry))
                    {
                        conflicts.Add(documentPath);
                        outgoing.Add(documentPath);
                        incoming.Add(documentPath);
                    }
                }

                continue;
            }

            var localChanged = HasChanged(localEntry, baselineEntry);
            var sharedChanged = HasChanged(sharedEntry, baselineEntry);

            if (localChanged && sharedChanged)
            {
                if (!Matches(localEntry, sharedEntry))
                {
                    conflicts.Add(documentPath);
                }

                continue;
            }

            if (localChanged)
            {
                outgoing.Add(documentPath);
                continue;
            }

            if (sharedChanged)
            {
                incoming.Add(documentPath);
            }
        }

        return new WorkspaceSyncAnalysis(outgoing, incoming, conflicts);
    }

    private static bool HasChanged(SyncManifestEntry? current, SyncTrackedEntry? baseline)
    {
        if (current is null && baseline is null)
        {
            return false;
        }

        if (current is null || baseline is null)
        {
            return true;
        }

        return !string.Equals(current.DocumentHash, baseline.DocumentHash, StringComparison.Ordinal)
            || !string.Equals(current.SignatureHash, baseline.SignatureHash, StringComparison.Ordinal);
    }

    private static bool Matches(SyncManifestEntry? left, SyncManifestEntry? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left.DocumentHash, right.DocumentHash, StringComparison.Ordinal)
            && string.Equals(left.SignatureHash, right.SignatureHash, StringComparison.Ordinal);
    }
}
