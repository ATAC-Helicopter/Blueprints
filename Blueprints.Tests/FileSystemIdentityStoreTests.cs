using Blueprints.Security.Services;

namespace Blueprints.Tests;

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
        Directory.CreateDirectory(_rootDirectory);

        var store = new FileSystemIdentityStore(
            _rootDirectory,
            new Ed25519KeyPairGenerator(),
            new LocalFilePrivateKeyProtector(Path.Combine(_rootDirectory, "protector.key")));

        var createdIdentity = store.Create("Flavio");
        var loadedIdentity = store.Load(createdIdentity.Profile.UserId);

        Assert.Equal("Flavio", loadedIdentity.Profile.DisplayName);
        Assert.Equal("Local AES-GCM", loadedIdentity.Profile.KeyStorageProvider);
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
