using System.Text;
using Blueprints.Security.Models;
using Blueprints.Security.Services;

namespace Blueprints.Tests;

public sealed class Ed25519SignatureServiceTests
{
    [Fact]
    public void SignAndVerify_RoundTripsWithGeneratedKeyPair()
    {
        var generator = new Ed25519KeyPairGenerator();
        var keyPair = generator.Generate("primary");
        var service = new Ed25519SignatureService();
        var payload = Encoding.UTF8.GetBytes("Blueprints signed payload");

        var signature = service.Sign(
            payload,
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var isValid = service.Verify(
            payload,
            signature,
            new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes));

        Assert.True(isValid);
    }
}
