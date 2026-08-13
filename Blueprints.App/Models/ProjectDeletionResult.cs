namespace Blueprints.App.Models;

public sealed record ProjectDeletionResult(
    string ProjectName,
    bool LocalWorkspaceRootRemoved,
    bool SharedWorkspaceRootRemoved);
