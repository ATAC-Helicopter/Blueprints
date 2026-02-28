namespace Blueprints.Security.Models;

public sealed record SignaturePublicKey(
    string KeyId,
    byte[] PublicKeyBytes);
