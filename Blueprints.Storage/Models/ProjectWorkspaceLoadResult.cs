using Blueprints.Security.Models;

namespace Blueprints.Storage.Models;

public sealed record ProjectWorkspaceLoadResult(
    ProjectWorkspaceSnapshot Workspace,
    TrustReport TrustReport);
