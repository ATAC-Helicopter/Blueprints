using System.Text;
using Blueprints.Storage.Models;

namespace Blueprints.App.Services;

public static class MarkdownChangelogBuilder
{
    public static string Build(
        ProjectWorkspaceSnapshot workspace,
        VersionWorkspaceSnapshot versionSnapshot)
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

        return builder.ToString().TrimEnd();
    }
}
