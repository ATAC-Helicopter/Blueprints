namespace Blueprints.Security.Models;

public sealed record SignatureKeyPair(
    string KeyId,
    byte[] PrivateKeyBytes,
    byte[] PublicKeyBytes);
