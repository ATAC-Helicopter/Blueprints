using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using Blueprints.Security.Abstractions;

namespace Blueprints.Security.Services;

[SupportedOSPlatform("windows")]
public sealed class DpapiPrivateKeyProtector : IPrivateKeyProtector
{
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("Blueprints.PrivateKey.v1");

    public string ProviderName => "DPAPI";

    public byte[] Protect(ReadOnlySpan<byte> privateKeyBytes)
    {
        if (privateKeyBytes.IsEmpty)
        {
            throw new ArgumentException("Private key payload must not be empty.", nameof(privateKeyBytes));
        }

        return ProtectedData.Protect(
            privateKeyBytes.ToArray(),
            OptionalEntropy,
            DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(ReadOnlySpan<byte> protectedPrivateKeyBytes)
    {
        if (protectedPrivateKeyBytes.IsEmpty)
        {
            throw new ArgumentException("Protected private key payload must not be empty.", nameof(protectedPrivateKeyBytes));
        }

        return ProtectedData.Unprotect(
            protectedPrivateKeyBytes.ToArray(),
            OptionalEntropy,
            DataProtectionScope.CurrentUser);
    }
}
