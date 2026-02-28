namespace Blueprints.App.Models;

public sealed record ItemEditRequest(
    Guid VersionId,
    Guid? ItemId,
    string ItemTypeId,
    string CategoryId,
    string Title,
    string? Description,
    bool IsDone);
