namespace Blueprints.App.Models;

public sealed record VaultSyncExchangeRootIntent(
    Guid ProjectId,
    string DestinationRoot,
    string ExchangeRoot);
