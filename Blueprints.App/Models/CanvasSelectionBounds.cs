namespace Blueprints.App.Models;

public sealed record CanvasSelectionBounds(
    double StartX,
    double StartY,
    double EndX,
    double EndY)
{
    public double Left => Math.Min(StartX, EndX);

    public double Top => Math.Min(StartY, EndY);

    public double Right => Math.Max(StartX, EndX);

    public double Bottom => Math.Max(StartY, EndY);
}
