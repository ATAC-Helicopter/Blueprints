using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class FileSystemIntegrationSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_ReturnsEmptyWhenSettingsDoNotExist()
    {
        var store = CreateStore();

        var settings = store.Load();

        Assert.Equal(IntegrationSettings.Empty, settings);
    }

    [Fact]
    public void Save_RoundTripsAllMachineLocalLinks()
    {
        var store = CreateStore();
        var expected = new IntegrationSettings("/repo-a", "/backup")
        {
            LocalGitRepositoryPaths = ["/repo-a", "/repo-b"],
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected.LocalGitRepositoryPath, actual.LocalGitRepositoryPath);
        Assert.Equal(expected.VaultSyncMetadataRoot, actual.VaultSyncMetadataRoot);
        Assert.Equal(expected.LocalGitRepositoryPaths, actual.LocalGitRepositoryPaths);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void Load_ReturnsEmptyForMalformedJson()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(SettingsPath, """{"localGitRepositoryPath":""");
        var store = CreateStore();

        var settings = store.Load();

        Assert.Equal(IntegrationSettings.Empty, settings);
    }

    [Fact]
    public void Load_ReturnsEmptyForOversizedInput()
    {
        Directory.CreateDirectory(_root);
        using (var stream = File.Create(SettingsPath))
        {
            stream.SetLength(FileSystemIntegrationSettingsStore.MaximumSettingsBytes + 1);
        }

        var settings = CreateStore().Load();

        Assert.Equal(IntegrationSettings.Empty, settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string SettingsPath => Path.Combine(_root, "integrations.json");

    private FileSystemIntegrationSettingsStore CreateStore() => new(SettingsPath);
}
