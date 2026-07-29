using System.Security.Cryptography;
using Blueprints.Security.Services;

namespace Blueprints.Tests;

public sealed class LocalFilePrivateKeyProtectorTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "LocalFilePrivateKeyProtector",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ProtectAndUnprotect_RoundTripsPrivateKeyPayload()
    {
        var protector = new LocalFilePrivateKeyProtector(Path.Combine(_rootDirectory, "protector.key"));
        var original = new byte[] { 1, 2, 3, 4, 5, 6 };

        var protectedBytes = protector.Protect(original);
        var unprotectedBytes = protector.Unprotect(protectedBytes);

        Assert.NotEqual(original, protectedBytes);
        Assert.Equal(original, unprotectedBytes);
        Assert.True(File.Exists(Path.Combine(_rootDirectory, "protector.key")));
    }

    [Fact]
    public void Unprotect_DetectsTamperedPayload()
    {
        var protector = new LocalFilePrivateKeyProtector(Path.Combine(_rootDirectory, "tamper.key"));
        var protectedBytes = protector.Protect([1, 2, 3, 4, 5, 6]);
        protectedBytes[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(protectedBytes));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
