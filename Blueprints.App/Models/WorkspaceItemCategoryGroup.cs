namespace Blueprints.App.Models;

public sealed record WorkspaceItemCategoryGroup(
    string CategoryId,
    IReadOnlyList<WorkspaceItemCard> Items);
