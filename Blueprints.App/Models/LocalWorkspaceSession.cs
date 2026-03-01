using Blueprints.Collaboration.Models;
using Blueprints.Security.Models;
using Blueprints.Storage.Models;

namespace Blueprints.App.Models;

public sealed record LocalWorkspaceSession(
    StoredIdentity Identity,
    WorkspacePaths Paths,
    ProjectWorkspaceLoadResult LoadResult,
    SyncSummary Sync,
    IReadOnlyList<string> ConflictPaths);
