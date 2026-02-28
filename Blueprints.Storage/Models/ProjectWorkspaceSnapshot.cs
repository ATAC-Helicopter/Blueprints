using Blueprints.Core.Models;

namespace Blueprints.Storage.Models;

public sealed record ProjectWorkspaceSnapshot(
    ProjectConfigurationDocument Project,
    MemberDocument Members,
    IReadOnlyList<VersionWorkspaceSnapshot> Versions);
