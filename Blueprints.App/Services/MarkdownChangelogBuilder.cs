using System.Text;
using Blueprints.App.Models;
using Blueprints.Core.Models;
using Blueprints.Storage.Models;

namespace Blueprints.App.Services;

public static class MarkdownChangelogBuilder
{
    public static string Build(
        ProjectWorkspaceSnapshot workspace,
        VersionWorkspaceSnapshot versionSnapshot,
        IReadOnlyList<SourceChangeSummary>? sourceChanges = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(versionSnapshot);

        var builder = new StringBuilder();
        var project = workspace.Project;
        var rules = project.ChangelogRules;
        var manualOrder = versionSnapshot.Version.ManualOrder
            .Select((itemId, index) => new { itemId, index })
            .ToDictionary(static entry => entry.itemId, static entry => entry.index);
        var categoryOrder = project.DefaultCategories
            .Select((category, index) => new { category.Id, Index = index })
            .ToDictionary(static entry => entry.Id, static entry => entry.Index, StringComparer.Ordinal);
        var categoryLabels = project.DefaultCategories
            .ToDictionary(static category => category.Id, static category => category.Label, StringComparer.Ordinal);

        var includedItems = versionSnapshot.Items
            .Where(item => rules.IncludeIncompleteByDefault || item.IsDone)
            .OrderBy(item => categoryOrder.TryGetValue(item.CategoryId, out var index) ? index : int.MaxValue)
            .ThenBy(item => manualOrder.TryGetValue(item.ItemId, out var index) ? index : int.MaxValue)
            .ThenBy(static item => item.CreatedUtc)
            .ToArray();

        builder.Append("# ")
            .Append(project.Name)
            .Append(' ')
            .Append(versionSnapshot.Version.Name)
            .AppendLine();
        builder.AppendLine();
        builder.Append("Generated: ")
            .AppendLine(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"));
        builder.Append("Status: ")
            .AppendLine(versionSnapshot.Version.Status.ToString());

        if (versionSnapshot.Version.ReleasedUtc is DateTimeOffset releasedUtc)
        {
            builder.Append("Released: ")
                .AppendLine(releasedUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"));
        }

        if (!string.IsNullOrWhiteSpace(versionSnapshot.Version.Notes))
        {
            builder.AppendLine();
            builder.AppendLine("## Notes");
            builder.AppendLine();
            builder.AppendLine(versionSnapshot.Version.Notes.Trim());
        }

        if (includedItems.Length == 0)
        {
            builder.AppendLine();
            builder.AppendLine("No changelog entries matched the current export rules.");
            return builder.ToString().TrimEnd();
        }

        foreach (var categoryGroup in includedItems.GroupBy(static item => item.CategoryId))
        {
            var categoryId = categoryGroup.Key;
            var heading = categoryLabels.TryGetValue(categoryId, out var label) ? label : categoryId;

            builder.AppendLine();
            builder.Append("## ")
                .AppendLine(heading);
            builder.AppendLine();

            foreach (var item in categoryGroup)
            {
                builder.Append("- ");
                if (rules.IncludeItemKeysByDefault && !string.IsNullOrWhiteSpace(item.ItemKey))
                {
                    builder.Append('`')
                        .Append(item.ItemKey)
                        .Append("` ");
                }

                builder.AppendLine(item.Title);

                if (rules.IncludeDescriptionsByDefault && !string.IsNullOrWhiteSpace(item.Description))
                {
                    builder.Append("  ")
                        .AppendLine(item.Description.Trim());
                }
            }
        }

        AppendSourceChanges(builder, includedItems, sourceChanges ?? []);

        return builder.ToString().TrimEnd();
    }

    private static void AppendSourceChanges(
        StringBuilder builder,
        IReadOnlyList<ItemDocument> includedItems,
        IReadOnlyList<SourceChangeSummary> sourceChanges)
    {
        if (sourceChanges.Count == 0)
        {
            return;
        }

        var includedItemKeys = includedItems
            .Select(static item => item.ItemKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);
        var matchedChanges = sourceChanges
            .Where(change => change.MatchedItemKeys.Any(includedItemKeys.Contains))
            .ToArray();
        var unmatchedChanges = sourceChanges
            .Where(change => !change.MatchedItemKeys.Any(includedItemKeys.Contains))
            .ToArray();

        builder.AppendLine();
        builder.AppendLine("## Source Changes");
        builder.AppendLine();

        if (matchedChanges.Length > 0)
        {
            builder.AppendLine("Matched to this version:");
            foreach (var change in matchedChanges)
            {
                builder.Append("- `")
                    .Append(change.ShortHash)
                    .Append("` ")
                    .Append(change.Subject)
                    .Append(" (")
                    .Append(string.Join(", ", change.MatchedItemKeys.Where(includedItemKeys.Contains)))
                    .AppendLine(")");
            }
        }

        if (unmatchedChanges.Length > 0)
        {
            if (matchedChanges.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Unmatched recent changes:");
            foreach (var change in unmatchedChanges)
            {
                builder.Append("- `")
                    .Append(change.ShortHash)
                    .Append("` ")
                    .AppendLine(change.Subject);
            }
        }
    }
}
