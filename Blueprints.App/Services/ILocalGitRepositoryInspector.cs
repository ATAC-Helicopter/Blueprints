using Blueprints.App.Models;

namespace Blueprints.App.Services;

public interface ILocalGitRepositoryInspector
{
    LocalGitRepositoryStatus Inspect(string repositoryPath);
}
