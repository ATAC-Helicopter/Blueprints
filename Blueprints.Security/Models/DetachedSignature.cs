namespace Blueprints.Security.Models;

public sealed record DetachedSignature(
    string Algorithm,
    string KeyId,
    string SignatureBase64);
