using Blueprints.Security.Models;
using Blueprints.Storage.Models;

namespace Blueprints.App.Models;

public sealed record LocalWorkspaceSession(
    StoredIdentity Identity,
    string WorkspaceRoot,
    ProjectWorkspaceLoadResult LoadResult);
