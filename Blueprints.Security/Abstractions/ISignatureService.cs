using Blueprints.Security.Models;

namespace Blueprints.Security.Abstractions;

public interface ISignatureService
{
    DetachedSignature Sign(ReadOnlySpan<byte> payload, SignatureKeyMaterial keyMaterial);

    bool Verify(ReadOnlySpan<byte> payload, DetachedSignature signature, SignaturePublicKey publicKey);
}
