using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class HostedSourceProviderRouter : IHostedSourceProviderReader
{
    private readonly IReadOnlyDictionary<SourceProviderKind, IHostedSourceProviderReader> _readers;

    public HostedSourceProviderRouter()
        : this(
            new GitHubRestSourceProviderReader(),
            new GitLabRestSourceProviderReader())
    {
    }

    public HostedSourceProviderRouter(
        GitHubRestSourceProviderReader gitHubReader,
        GitLabRestSourceProviderReader gitLabReader)
    {
        ArgumentNullException.ThrowIfNull(gitHubReader);
        ArgumentNullException.ThrowIfNull(gitLabReader);
        _readers = new Dictionary<SourceProviderKind, IHostedSourceProviderReader>
        {
            [SourceProviderKind.GitHub] = gitHubReader,
            [SourceProviderKind.GitLab] = gitLabReader,
        };
    }

    public HostedSourceDiscoveryResult Read(
        string repositoryRoot,
        HostedRepositoryDescriptor repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!_readers.TryGetValue(repository.Provider, out var reader))
        {
            throw new InvalidOperationException(
                $"No hosted-source reader supports {repository.Provider}.");
        }

        return reader.Read(repositoryRoot, repository);
    }
}
