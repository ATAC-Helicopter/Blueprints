using Blueprints.App.Models;

namespace Blueprints.App.Services;

public static class CanvasMiniMapProjectionService
{
    public static IReadOnlyList<CanvasMiniMapNode> Project(
        IReadOnlyList<CanvasNodeBounds> nodes,
        IReadOnlySet<Guid> selectedIds,
        double surfaceWidth,
        double surfaceHeight,
        double mapWidth,
        double mapHeight)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(selectedIds);
        if (surfaceWidth <= 0 || surfaceHeight <= 0 || mapWidth <= 0 || mapHeight <= 0)
        {
            return [];
        }

        var scale = Math.Min(mapWidth / surfaceWidth, mapHeight / surfaceHeight);
        return nodes.Select(node => new CanvasMiniMapNode(
            node.EntityId,
            node.X * scale,
            node.Y * scale,
            Math.Max(3, node.Width * scale),
            Math.Max(2, node.Height * scale),
            selectedIds.Contains(node.EntityId))).ToArray();
    }
}
