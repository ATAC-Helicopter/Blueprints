using Blueprints.App.Models;

namespace Blueprints.App.Services;

public interface ISourceDiscoveryService
{
    SourceDiscoveryResult Discover(string repositoryPath);
}
