using Blueprints.Core.Enums;

namespace Blueprints.App.Models;

public sealed record WorkspaceVersionCard(
    Guid VersionId,
    string Name,
    ReleaseStatus Status,
    string? Notes,
    int ItemCount,
    int CompletedItemCount,
    IReadOnlyList<WorkspaceItemCard> Items);
