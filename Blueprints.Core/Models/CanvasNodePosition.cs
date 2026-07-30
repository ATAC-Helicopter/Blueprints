namespace Blueprints.Core.Models;

public sealed record CanvasNodePosition(
    string NodeType,
    Guid EntityId,
    double X,
    double Y);
