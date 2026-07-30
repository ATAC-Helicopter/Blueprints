namespace Blueprints.App.Models;

public sealed record CanvasAlignmentGuides(
    IReadOnlyList<double> Vertical,
    IReadOnlyList<double> Horizontal)
{
    public static CanvasAlignmentGuides Empty { get; } = new([], []);
}
