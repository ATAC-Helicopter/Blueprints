namespace Blueprints.App.Models;

public sealed record GitRepositoryOperationResult(
    bool Success,
    string Summary,
    string RepositoryRoot,
    LocalGitRepositoryStatus? Status = null);
