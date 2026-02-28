using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;
using NSec.Cryptography;

namespace Blueprints.Security.Services;

public sealed class Ed25519SignatureService : ISignatureService
{
    private const string AlgorithmName = "Ed25519";
    private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;

    public DetachedSignature Sign(ReadOnlySpan<byte> payload, SignatureKeyMaterial keyMaterial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyMaterial.KeyId);

        var creationParameters = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.None,
        };

        using var key = Key.Import(Algorithm, keyMaterial.PrivateKeyBytes, KeyBlobFormat.PkixPrivateKey, creationParameters);
        var signatureBytes = Algorithm.Sign(key, payload);

        return new DetachedSignature(
            AlgorithmName,
            keyMaterial.KeyId,
            Convert.ToBase64String(signatureBytes));
    }

    public bool Verify(ReadOnlySpan<byte> payload, DetachedSignature signature, SignaturePublicKey publicKey)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(publicKey);

        if (!string.Equals(signature.Algorithm, AlgorithmName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(signature.KeyId, publicKey.KeyId, StringComparison.Ordinal))
        {
            return false;
        }

        var signatureBytes = Convert.FromBase64String(signature.SignatureBase64);
        var importedPublicKey = PublicKey.Import(Algorithm, publicKey.PublicKeyBytes, KeyBlobFormat.PkixPublicKey);

        return Algorithm.Verify(importedPublicKey, payload, signatureBytes);
    }
}
