using Blueprints.Security.Abstractions;
using Blueprints.Security.Services;

namespace Blueprints.Tests;

public sealed class IdentityInvitationServiceTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "IdentityInvitations",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriteAndRead_RoundTripsAProofOfKeyPossession()
    {
        var identity = CreateIdentity("Bob");
        var service = new IdentityInvitationService(new Ed25519SignatureService());
        var path = Path.Combine(_rootDirectory, "bob.blueprints-identity.json");

        service.Write(path, identity);
        var invitation = service.Read(path);

        Assert.Equal(identity.Profile.UserId, invitation.UserId);
        Assert.Equal(identity.Profile.KeyId, invitation.KeyId);
        Assert.Equal(identity.Profile.PublicKeyBase64, invitation.PublicKeyBase64);
    }

    [Fact]
    public void Read_RejectsTamperedIdentityFields()
    {
        var identity = CreateIdentity("Bob");
        var service = new IdentityInvitationService(new Ed25519SignatureService());
        var path = Path.Combine(_rootDirectory, "tampered.blueprints-identity.json");
        service.Write(path, identity);
        File.WriteAllText(
            path,
            File.ReadAllText(path).Replace("\"Bob\"", "\"Mallory\"", StringComparison.Ordinal));

        var exception = Assert.Throws<InvalidOperationException>(() => service.Read(path));

        Assert.Contains("proof", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private Blueprints.Security.Models.StoredIdentity CreateIdentity(string displayName)
    {
        var identityRoot = Path.Combine(_rootDirectory, "identities");
        return new FileSystemIdentityStore(
            identityRoot,
            new Ed25519KeyPairGenerator(),
            new TestPrivateKeyProtector()).Create(displayName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private sealed class TestPrivateKeyProtector : IPrivateKeyProtector
    {
        public string ProviderName => "Test";

        public byte[] Protect(ReadOnlySpan<byte> privateKeyBytes) =>
            privateKeyBytes.ToArray();

        public byte[] Unprotect(ReadOnlySpan<byte> protectedBytes) =>
            protectedBytes.ToArray();
    }
}
