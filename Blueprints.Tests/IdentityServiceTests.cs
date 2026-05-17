using Blueprints.Security.Services;

namespace Blueprints.Tests;

public sealed class IdentityServiceTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "IdentityService",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetOrCreateDefaultIdentity_CreatesThenReloadsSameIdentity()
    {
        Directory.CreateDirectory(_rootDirectory);

        var store = new FileSystemIdentityStore(
            _rootDirectory,
            new Ed25519KeyPairGenerator(),
            new LocalFilePrivateKeyProtector(Path.Combine(_rootDirectory, "protector.key")));

        var service = new IdentityService(_rootDirectory, store);

        var first = service.GetOrCreateDefaultIdentity("Flavio");
        var second = service.GetOrCreateDefaultIdentity("Ignored Second Name");

        Assert.Equal(first.Profile.UserId, second.Profile.UserId);
        Assert.Equal("Flavio", second.Profile.DisplayName);
        Assert.Single(service.ListProfiles());
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
