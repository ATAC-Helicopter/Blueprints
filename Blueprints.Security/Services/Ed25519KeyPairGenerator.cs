using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;
using NSec.Cryptography;

namespace Blueprints.Security.Services;

public sealed class Ed25519KeyPairGenerator : IKeyPairGenerator
{
    private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;

    public SignatureKeyPair Generate(string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        var creationParameters = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport,
        };

        using var key = Key.Create(Algorithm, creationParameters);

        return new SignatureKeyPair(
            keyId,
            key.Export(KeyBlobFormat.PkixPrivateKey),
            key.PublicKey.Export(KeyBlobFormat.PkixPublicKey));
    }
}
