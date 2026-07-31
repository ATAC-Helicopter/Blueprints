namespace Blueprints.Security.Models;

public sealed record IdentityBackupFile(
    int SchemaVersion,
    IdentityProfile Identity,
    string KeyDerivation,
    int Iterations,
    string SaltBase64,
    string NonceBase64,
    string CiphertextBase64,
    string TagBase64);
