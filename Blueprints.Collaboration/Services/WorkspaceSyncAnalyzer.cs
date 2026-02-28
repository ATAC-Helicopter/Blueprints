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

        var outgoing = new List<string>();
        var incoming = new List<string>();
        var conflicts = new List<string>();

        foreach (var documentPath in localEntries.Keys.Union(sharedEntries.Keys, StringComparer.Ordinal).OrderBy(static path => path, StringComparer.Ordinal))
        {
            var hasLocal = localEntries.TryGetValue(documentPath, out var localEntry);
            var hasShared = sharedEntries.TryGetValue(documentPath, out var sharedEntry);

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

            if (localEntry is null || sharedEntry is null)
            {
                continue;
            }

            var documentChanged = !string.Equals(localEntry.DocumentHash, sharedEntry.DocumentHash, StringComparison.Ordinal);
            var signatureChanged = !string.Equals(localEntry.SignatureHash, sharedEntry.SignatureHash, StringComparison.Ordinal);

            if (documentChanged || signatureChanged)
            {
                outgoing.Add(documentPath);
                incoming.Add(documentPath);
                conflicts.Add(documentPath);
            }
        }

        return new WorkspaceSyncAnalysis(outgoing, incoming, conflicts);
    }
}
