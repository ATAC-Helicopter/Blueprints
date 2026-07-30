namespace Blueprints.App.Models;

public sealed record CanvasLayoutEditRequest(
    IReadOnlyList<CanvasNodeLayoutEdit> Nodes);
