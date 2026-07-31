using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class CanvasMiniMapProjectionServiceTests
{
    [Fact]
    public void Project_ScalesFramesNodesAndSelectionIntoMapBounds()
    {
        var selected = Guid.NewGuid();
        var nodes = new[]
        {
            new CanvasNodeBounds(selected, 100, 50, 400, 200),
            new CanvasNodeBounds(Guid.NewGuid(), 900, 700, 80, 60),
        };

        var result = CanvasMiniMapProjectionService.Project(
            nodes,
            new HashSet<Guid> { selected },
            1000,
            800,
            200,
            100);

        Assert.Equal(2, result.Count);
        Assert.True(result.Single(node => node.EntityId == selected).IsSelected);
        Assert.All(result, node =>
        {
            Assert.InRange(node.X, 0, 200);
            Assert.InRange(node.Y, 0, 100);
        });
    }
}
