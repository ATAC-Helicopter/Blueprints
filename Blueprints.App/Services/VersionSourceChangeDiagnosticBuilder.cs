using Blueprints.App.Models;

namespace Blueprints.App.Services;

public static class VersionSourceChangeDiagnosticBuilder
{
    public static IReadOnlyList<VersionSourceChangeDiagnostic> Build(
        WorkspaceVersionCard? selectedVersion,
        IReadOnlyList<SourceChangeSummary> sourceChanges)
    {
        ArgumentNullException.ThrowIfNull(sourceChanges);

        if (selectedVersion is null || sourceChanges.Count == 0)
        {
            return [];
        }

        var exportItemKeys = selectedVersion.Items
            .Where(static item => item.IsDone)
            .Select(static item => item.ItemKey)
            .Where(static itemKey => !string.IsNullOrWhiteSpace(itemKey))
            .ToHashSet(StringComparer.Ordinal);

        return sourceChanges
            .Select(change =>
            {
                var matchingKeys = change.MatchedItemKeys
                    .Where(exportItemKeys.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                return new VersionSourceChangeDiagnostic(
                    change,
                    matchingKeys,
                    matchingKeys.Length > 0);
            })
            .ToArray();
    }
}
