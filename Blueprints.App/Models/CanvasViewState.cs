namespace Blueprints.App.Models;

public sealed record CanvasViewState(
    double Zoom,
    double HorizontalOffset,
    double VerticalOffset)
{
    public static CanvasViewState Default { get; } = new(1, 0, 0);
}
