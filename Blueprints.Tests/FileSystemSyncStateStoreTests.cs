using Blueprints.Collaboration.Models;
using Blueprints.Collaboration.Services;

namespace Blueprints.Tests;

public sealed class FileSystemSyncStateStoreTests : IDisposable
{
    private readonly string _workspaceRoot = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "SyncState",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_ReturnsDefault_WhenStateDoesNotExist()
    {
        var store = new FileSystemSyncStateStore();

        var state = store.Load(_workspaceRoot);

        Assert.Equal(0, state.LastPulledManifestVersion);
        Assert.Equal(0, state.LastPushedManifestVersion);
        Assert.Empty(state.KnownRemoteBatchIds);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsState()
    {
        var store = new FileSystemSyncStateStore();
        var expected = new SyncStateDocument(
            1,
            4,
            3,
            DateTimeOffset.UtcNow,
            ["batch-0003", "batch-0004"],
            ["versions/123/version.json"],
            [new SyncTrackedEntry("project/project.json", "ABC", "DEF")]);

        store.Save(_workspaceRoot, expected);

        var actual = store.Load(_workspaceRoot);

        Assert.Equal(expected.LastPulledManifestVersion, actual.LastPulledManifestVersion);
        Assert.Equal(expected.LastPushedManifestVersion, actual.LastPushedManifestVersion);
        Assert.Equal(expected.KnownRemoteBatchIds, actual.KnownRemoteBatchIds);
        Assert.Equal(expected.UnresolvedConflicts, actual.UnresolvedConflicts);
        Assert.Equal(expected.TrackedEntries, actual.TrackedEntries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }
}
