namespace Blueprints.Security.Models;

public sealed record SignatureKeyMaterial(
    string KeyId,
    byte[] PrivateKeyBytes);
