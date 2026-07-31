using Blueprints.App.Models;

namespace Blueprints.App.Services;

public interface IGitRepositoryService
{
    LocalGitRepositoryStatus Inspect(string repositoryPath);

    Task<GitRepositoryOperationResult> CloneAsync(
        string remote,
        string destinationParent,
        string? folderName,
        CancellationToken cancellationToken = default);

    Task<GitRepositoryOperationResult> PullAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default);

    Task<GitRepositoryOperationResult> CommitAllAsync(
        string repositoryPath,
        string message,
        CancellationToken cancellationToken = default);

    Task<GitRepositoryOperationResult> PushAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default);
}
