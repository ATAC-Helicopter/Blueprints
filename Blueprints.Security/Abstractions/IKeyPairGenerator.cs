using Blueprints.Security.Models;

namespace Blueprints.Security.Abstractions;

public interface IKeyPairGenerator
{
    SignatureKeyPair Generate(string keyId);
}
