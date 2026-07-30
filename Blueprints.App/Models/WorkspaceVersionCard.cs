using Blueprints.Core.Enums;

namespace Blueprints.App.Models;

public sealed record WorkspaceVersionCard(
    Guid VersionId,
    string Name,
    ReleaseStatus Status,
    string? Notes,
    int ItemCount,
    int CompletedItemCount,
    IReadOnlyList<WorkspaceItemCard> Items)
{
    public IReadOnlyList<WorkspaceItemCategoryGroup> ItemGroups =>
        Items
            .GroupBy(static item => item.CategoryId, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(group => new WorkspaceItemCategoryGroup(
                group.Key,
                group.OrderBy(static item => item.ItemKey, StringComparer.Ordinal).ToArray()))
            .ToArray();
}
