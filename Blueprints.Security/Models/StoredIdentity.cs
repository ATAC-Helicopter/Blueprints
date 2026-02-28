namespace Blueprints.Security.Models;

public sealed record StoredIdentity(
    IdentityProfile Profile,
    SignatureKeyMaterial SigningKey,
    SignaturePublicKey PublicKey);
