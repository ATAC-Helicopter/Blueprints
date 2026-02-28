namespace Blueprints.App.Models;

public sealed record WorkspaceItemCard(
    Guid ItemId,
    string ItemKey,
    string ItemTypeId,
    string CategoryId,
    string Title,
    string? Description,
    bool IsDone);
