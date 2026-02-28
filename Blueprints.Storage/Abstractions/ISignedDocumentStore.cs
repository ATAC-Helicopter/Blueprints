using Blueprints.Security.Models;
using Blueprints.Storage.Models;

namespace Blueprints.Storage.Abstractions;

public interface ISignedDocumentStore
{
    SignedDocumentWriteResult Write<T>(
        string documentPath,
        T document,
        SignatureKeyMaterial signingKey);

    SignedDocumentReadResult<T> Read<T>(
        string documentPath,
        SignaturePublicKey publicKey);
}
