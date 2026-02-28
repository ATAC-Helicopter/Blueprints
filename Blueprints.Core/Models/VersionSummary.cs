using Blueprints.Core.Enums;

namespace Blueprints.Core.Models;

public sealed record VersionSummary(
    string Name,
    ReleaseStatus Status,
    int ItemCount,
    int CompletedItemCount);
