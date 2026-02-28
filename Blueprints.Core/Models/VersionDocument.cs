using Blueprints.Core.Enums;

namespace Blueprints.Core.Models;

public sealed record VersionDocument(
    int SchemaVersion,
    Guid ProjectId,
    Guid VersionId,
    string Name,
    ReleaseStatus Status,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ReleasedUtc,
    string? Notes,
    IReadOnlyList<Guid> ManualOrder);
