using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class FileSystemCanvasViewStateStoreTests : IDisposable
{
    private readonly string _workspaceRoot = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "CanvasViewState",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_ReturnsDefault_WhenLocalStateDoesNotExist()
    {
        var store = new FileSystemCanvasViewStateStore();

        var state = store.Load(_workspaceRoot);

        Assert.Equal(CanvasViewState.Default, state);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsMachineLocalViewport()
    {
        var store = new FileSystemCanvasViewStateStore();
        var collapsed = Guid.NewGuid();
        var expected = new CanvasViewState(
            0.8,
            125,
            240,
            CanvasViewMode.Dependencies,
            "security",
            "Review",
            "1.0.0",
            false,
            collapsed.ToString("D"));

        store.Save(_workspaceRoot, expected);
        var actual = store.Load(_workspaceRoot);

        Assert.Equal(expected, actual);
        Assert.True(File.Exists(Path.Combine(_workspaceRoot, ".blueprints", "canvas-view.json")));
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(2.5)]
    public void SaveAndLoad_AcceptsTheVisibleZoomRange(double zoom)
    {
        var store = new FileSystemCanvasViewStateStore();
        var expected = new CanvasViewState(zoom, 0, 0);

        store.Save(_workspaceRoot, expected);

        Assert.Equal(expected, store.Load(_workspaceRoot));
    }

    [Fact]
    public void Load_FallsBackToDefault_WhenLocalStateIsMalformed()
    {
        var path = Path.Combine(_workspaceRoot, ".blueprints", "canvas-view.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"zoom":999,"horizontalOffset":0,"verticalOffset":0}""");

        var state = new FileSystemCanvasViewStateStore().Load(_workspaceRoot);

        Assert.Equal(CanvasViewState.Default, state);
    }

    [Fact]
    public void Save_RejectsNonFiniteViewportValues()
    {
        var store = new FileSystemCanvasViewStateStore();

        Assert.Throws<InvalidOperationException>(
            () => store.Save(_workspaceRoot, new CanvasViewState(double.NaN, 0, 0)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }
}
