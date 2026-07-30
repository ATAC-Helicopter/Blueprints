using Blueprints.App.Models;

namespace Blueprints.App.Services;

public interface IHostedSourceProviderReader
{
    HostedSourceDiscoveryResult Read(
        string repositoryRoot,
        HostedRepositoryDescriptor repository);
}
