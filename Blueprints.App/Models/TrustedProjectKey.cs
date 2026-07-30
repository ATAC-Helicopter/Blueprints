namespace Blueprints.App.Models;

public sealed record TrustedProjectKey(
    Guid UserId,
    string DisplayName,
    string KeyId,
    string PublicKeyBase64,
    DateTimeOffset FirstTrustedUtc);
