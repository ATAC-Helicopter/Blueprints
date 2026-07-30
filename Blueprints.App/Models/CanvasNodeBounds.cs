namespace Blueprints.App.Models;

public sealed record CanvasNodeBounds(
    Guid EntityId,
    double X,
    double Y,
    double Width,
    double Height);
