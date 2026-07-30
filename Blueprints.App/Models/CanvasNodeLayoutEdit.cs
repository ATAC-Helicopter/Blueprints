namespace Blueprints.App.Models;

public sealed record CanvasNodeLayoutEdit(
    string NodeType,
    Guid EntityId,
    double X,
    double Y);
