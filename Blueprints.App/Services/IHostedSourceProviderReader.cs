using Blueprints.App.Models;

namespace Blueprints.App.Services;

public interface IHostedSourceProviderReader
{
    int ContractVersion { get; }

    SourceProviderKind Provider { get; }

    SourceProviderCapabilities Capabilities { get; }

    HostedSourceDiscoveryResult Read(
        string repositoryRoot,
        HostedRepositoryDescriptor repository);
}
