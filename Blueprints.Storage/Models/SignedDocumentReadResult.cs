using Blueprints.Security.Models;

namespace Blueprints.Storage.Models;

public sealed record SignedDocumentReadResult<T>(
    T Document,
    string CanonicalJson,
    DetachedSignature Signature,
    bool IsSignatureValid);
