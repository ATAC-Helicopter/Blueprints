namespace Blueprints.Security.Models;

public sealed record IdentityInvitationPayload(
    int SchemaVersion,
    Guid UserId,
    string DisplayName,
    string KeyId,
    string PublicKeyBase64,
    DateTimeOffset CreatedUtc);
