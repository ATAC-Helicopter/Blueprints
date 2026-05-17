using Blueprints.App.Models;

namespace Blueprints.App.Services;

public interface IIntegrationSettingsStore
{
    IntegrationSettings Load();

    void Save(IntegrationSettings settings);
}
