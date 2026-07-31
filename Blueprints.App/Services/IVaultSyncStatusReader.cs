using Blueprints.App.Models;

namespace Blueprints.App.Services;

public interface IVaultSyncStatusReader
{
    VaultSyncStatusSummary Inspect(string configuredRoot);
}
