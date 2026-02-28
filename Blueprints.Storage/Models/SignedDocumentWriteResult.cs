using Blueprints.Security.Models;

namespace Blueprints.Storage.Models;

public sealed record SignedDocumentWriteResult(
    string DocumentPath,
    string SignaturePath,
    string CanonicalJson,
    DetachedSignature Signature);
