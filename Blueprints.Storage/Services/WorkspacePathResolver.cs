using Blueprints.Storage.Models;

namespace Blueprints.Storage.Services;

public static class WorkspacePathResolver
{
    public static WorkspacePaths Create(string localWorkspaceRoot, string sharedProjectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedProjectRoot);

        return new WorkspacePaths(localWorkspaceRoot, sharedProjectRoot);
    }
}
