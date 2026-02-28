namespace Blueprints.Core.Models;

public sealed record ItemDocument(
    int SchemaVersion,
    Guid ProjectId,
    Guid VersionId,
    Guid ItemId,
    string ItemKey,
    string ItemKeyTypeId,
    string CategoryId,
    string Title,
    string? Description,
    bool IsDone,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    Guid LastModifiedByUserId,
    string LastModifiedByName);
