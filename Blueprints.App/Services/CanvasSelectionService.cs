using Blueprints.App.Models;

namespace Blueprints.App.Services;

public static class CanvasSelectionService
{
    public static IReadOnlySet<Guid> SelectIntersecting(
        IReadOnlyList<CanvasNodeBounds> nodes,
        CanvasSelectionBounds selection)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(selection);

        return nodes
            .Where(node =>
                node.X <= selection.Right &&
                node.X + node.Width >= selection.Left &&
                node.Y <= selection.Bottom &&
                node.Y + node.Height >= selection.Top)
            .Select(static node => node.EntityId)
            .ToHashSet();
    }

    public static (double DeltaX, double DeltaY) ConstrainMove(
        IReadOnlyList<CanvasNodeBounds> selectedNodes,
        double requestedDeltaX,
        double requestedDeltaY,
        double surfaceWidth,
        double surfaceHeight)
    {
        ArgumentNullException.ThrowIfNull(selectedNodes);
        if (selectedNodes.Count == 0)
        {
            return (0, 0);
        }

        var minimumDeltaX = selectedNodes.Max(static node => -node.X);
        var maximumDeltaX = selectedNodes.Min(node => surfaceWidth - node.X - node.Width);
        var minimumDeltaY = selectedNodes.Max(static node => -node.Y);
        var maximumDeltaY = selectedNodes.Min(node => surfaceHeight - node.Y - node.Height);

        return (
            Math.Clamp(requestedDeltaX, minimumDeltaX, maximumDeltaX),
            Math.Clamp(requestedDeltaY, minimumDeltaY, maximumDeltaY));
    }

    public static CanvasAlignmentGuides FindAlignmentGuides(
        IReadOnlyList<CanvasNodeBounds> movingNodes,
        IReadOnlyList<CanvasNodeBounds> stationaryNodes,
        double tolerance = 5)
    {
        ArgumentNullException.ThrowIfNull(movingNodes);
        ArgumentNullException.ThrowIfNull(stationaryNodes);
        if (tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }

        if (movingNodes.Count == 0 || stationaryNodes.Count == 0)
        {
            return CanvasAlignmentGuides.Empty;
        }

        var vertical = new HashSet<double>();
        var horizontal = new HashSet<double>();
        foreach (var moving in movingNodes)
        {
            foreach (var stationary in stationaryNodes)
            {
                AddMatches(
                    [moving.X, moving.X + moving.Width / 2, moving.X + moving.Width],
                    [stationary.X, stationary.X + stationary.Width / 2, stationary.X + stationary.Width],
                    tolerance,
                    vertical);
                AddMatches(
                    [moving.Y, moving.Y + moving.Height / 2, moving.Y + moving.Height],
                    [stationary.Y, stationary.Y + stationary.Height / 2, stationary.Y + stationary.Height],
                    tolerance,
                    horizontal);
            }
        }

        return new CanvasAlignmentGuides(
            vertical.Order().ToArray(),
            horizontal.Order().ToArray());
    }

    private static void AddMatches(
        IReadOnlyList<double> movingCoordinates,
        IReadOnlyList<double> stationaryCoordinates,
        double tolerance,
        ISet<double> matches)
    {
        foreach (var moving in movingCoordinates)
        {
            foreach (var stationary in stationaryCoordinates)
            {
                if (Math.Abs(moving - stationary) <= tolerance)
                {
                    matches.Add(stationary);
                }
            }
        }
    }
}
