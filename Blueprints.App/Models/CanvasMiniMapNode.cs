namespace Blueprints.App.Models;

public sealed record CanvasMiniMapNode(
    Guid EntityId,
    double X,
    double Y,
    double Width,
    double Height,
    bool IsSelected);
