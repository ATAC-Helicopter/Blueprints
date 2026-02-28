using Blueprints.Core.Models;

namespace Blueprints.Storage.Models;

public sealed record VersionWorkspaceSnapshot(
    VersionDocument Version,
    IReadOnlyList<ItemDocument> Items);
