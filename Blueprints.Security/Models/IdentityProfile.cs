namespace Blueprints.Security.Models;

public sealed record IdentityProfile(
    Guid UserId,
    string DisplayName,
    string KeyId,
    string PublicKeyBase64,
    string KeyStorageProvider,
    DateTimeOffset CreatedUtc);
