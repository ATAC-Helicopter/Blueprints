using Blueprints.Core.Enums;

namespace Blueprints.App.Models;

public sealed record WorkspaceItemCard(
    Guid ItemId,
    string ItemKey,
    string ItemTypeId,
    string CategoryId,
    string Title,
    string? Description,
    bool IsDone,
    WorkItemLifecycle WorkflowState = WorkItemLifecycle.Planned,
    IReadOnlyList<string>? Tags = null)
{
    public string? SourceReference =>
        Tags?.FirstOrDefault(static tag =>
            tag.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            tag.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        ?? Tags?.FirstOrDefault(static tag =>
            tag.StartsWith("source:", StringComparison.OrdinalIgnoreCase));
}
