namespace Blueprints.Security.Models;

public sealed record StoredIdentityRecord(
    IdentityProfile Profile,
    string ProtectedPrivateKeyFileName);
