using System.Runtime.Versioning;
using Blueprints.Security.Services;

namespace Blueprints.Tests;

[SupportedOSPlatform("windows")]
public sealed class FileSystemIdentityStoreTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "IdentityStore",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateAndLoad_RoundTripsIdentityThroughProtectedFilesystemStorage()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_rootDirectory);

        var store = new FileSystemIdentityStore(
            _rootDirectory,
            new Ed25519KeyPairGenerator(),
            new DpapiPrivateKeyProtector());

        var createdIdentity = store.Create("Flavio");
        var loadedIdentity = store.Load(createdIdentity.Profile.UserId);

        Assert.Equal("Flavio", loadedIdentity.Profile.DisplayName);
        Assert.Equal("DPAPI", loadedIdentity.Profile.KeyStorageProvider);
        Assert.Equal(createdIdentity.Profile.KeyId, loadedIdentity.Profile.KeyId);
        Assert.Equal(createdIdentity.SigningKey.PrivateKeyBytes, loadedIdentity.SigningKey.PrivateKeyBytes);
        Assert.Equal(createdIdentity.PublicKey.PublicKeyBytes, loadedIdentity.PublicKey.PublicKeyBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
