using Blueprints.App.Models;

namespace Blueprints.App.Services;

public interface IVaultSyncExchangeRootAdapter
{
    VaultSyncExchangeRootIntent PrepareIntent(
        string configuredMetadataRoot,
        Guid projectId);

    VaultSyncExchangeRootApproval Approve(
        VaultSyncExchangeRootIntent intent,
        DateTimeOffset nowUtc);

    VaultSyncExchangeRootRegistration Register(
        VaultSyncExchangeRootIntent intent,
        VaultSyncExchangeRootApproval approval,
        DateTimeOffset nowUtc);
}
