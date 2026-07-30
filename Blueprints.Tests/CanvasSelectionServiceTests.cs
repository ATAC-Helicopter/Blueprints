using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class CanvasSelectionServiceTests
{
    [Fact]
    public void SelectIntersecting_NormalizesDragDirectionAndIncludesPartialIntersections()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var outside = Guid.NewGuid();
        CanvasNodeBounds[] nodes =
        [
            new(first, 20, 20, 100, 80),
            new(second, 180, 100, 100, 80),
            new(outside, 400, 400, 100, 80),
        ];

        var selected = CanvasSelectionService.SelectIntersecting(
            nodes,
            new CanvasSelectionBounds(250, 150, 80, 60));

        Assert.Equal(2, selected.Count);
        Assert.Contains(first, selected);
        Assert.Contains(second, selected);
        Assert.DoesNotContain(outside, selected);
    }

    [Fact]
    public void ConstrainMove_KeepsTheWholeSelectionInsideTheSurface()
    {
        CanvasNodeBounds[] nodes =
        [
            new(Guid.NewGuid(), 10, 20, 100, 80),
            new(Guid.NewGuid(), 250, 220, 120, 90),
        ];

        var towardTopLeft = CanvasSelectionService.ConstrainMove(nodes, -50, -50, 400, 340);
        var towardBottomRight = CanvasSelectionService.ConstrainMove(nodes, 100, 100, 400, 340);

        Assert.Equal((-10d, -20d), towardTopLeft);
        Assert.Equal((30d, 30d), towardBottomRight);
    }

    [Fact]
    public void FindAlignmentGuides_MatchesEdgesAndCentersWithinTolerance()
    {
        CanvasNodeBounds[] moving =
        [
            new(Guid.NewGuid(), 98, 48, 100, 80),
        ];
        CanvasNodeBounds[] stationary =
        [
            new(Guid.NewGuid(), 100, 50, 100, 80),
            new(Guid.NewGuid(), 500, 500, 100, 80),
        ];

        var guides = CanvasSelectionService.FindAlignmentGuides(moving, stationary);

        Assert.Equal([100d, 150d, 200d], guides.Vertical);
        Assert.Equal([50d, 90d, 130d], guides.Horizontal);
    }
}
