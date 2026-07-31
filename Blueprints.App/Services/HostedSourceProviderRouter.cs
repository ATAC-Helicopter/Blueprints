using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class HostedSourceProviderRouter : IHostedSourceProviderReader
{
    private readonly IReadOnlyDictionary<SourceProviderKind, IHostedSourceProviderReader> _readers;

    public int ContractVersion => HostedSourceProviderContract.CurrentVersion;

    public SourceProviderKind Provider => SourceProviderKind.Local;

    public SourceProviderCapabilities Capabilities =>
        _readers.Values.Aggregate(
            SourceProviderCapabilities.None,
            static (capabilities, reader) => capabilities | reader.Capabilities);

    public HostedSourceProviderRouter()
        : this(
            new GitHubRestSourceProviderReader(),
            new GitLabRestSourceProviderReader())
    {
    }

    public HostedSourceProviderRouter(
        GitHubRestSourceProviderReader gitHubReader,
        GitLabRestSourceProviderReader gitLabReader)
        : this([gitHubReader, gitLabReader])
    {
    }

    public HostedSourceProviderRouter(
        IEnumerable<IHostedSourceProviderReader> readers)
    {
        ArgumentNullException.ThrowIfNull(readers);
        var registeredReaders =
            readers.ToArray();
        foreach (var reader in registeredReaders)
        {
            ArgumentNullException.ThrowIfNull(reader);
            if (reader.ContractVersion != HostedSourceProviderContract.CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"Provider {reader.Provider} uses contract {reader.ContractVersion}; Blueprints requires contract {HostedSourceProviderContract.CurrentVersion}.");
            }

            if (reader.Provider == SourceProviderKind.Local
                || reader.Capabilities == SourceProviderCapabilities.None)
            {
                throw new InvalidOperationException(
                    "Hosted source providers must declare a hosted provider and at least one capability.");
            }
        }

        _readers = registeredReaders.ToDictionary(static reader => reader.Provider);
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

        var result = reader.Read(repositoryRoot, repository);
        if (result.Candidates.Count > HostedSourceProviderContract.MaximumCandidatesPerDiscovery
            || result.Warnings.Count > HostedSourceProviderContract.MaximumWarningsPerDiscovery)
        {
            throw new InvalidOperationException(
                $"Provider {reader.Provider} exceeded the bounded contract response limits.");
        }

        return result;
    }
}
