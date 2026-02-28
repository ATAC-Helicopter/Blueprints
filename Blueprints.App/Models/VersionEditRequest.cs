using Blueprints.Core.Enums;

namespace Blueprints.App.Models;

public sealed record VersionEditRequest(
    Guid? VersionId,
    string Name,
    ReleaseStatus Status,
    string? Notes);
