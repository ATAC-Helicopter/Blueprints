namespace Blueprints.App.Models;

public sealed record VaultSyncExchangeRootRegistration(
    string ExchangeRoot,
    string RegistrationMarkerPath,
    bool AlreadyRegistered);
