namespace Blueprints.App.Models;

public sealed record CanvasViewState(
    double Zoom,
    double HorizontalOffset,
    double VerticalOffset,
    CanvasViewMode ViewMode = CanvasViewMode.Plan,
    string SearchText = "",
    string LifecycleFilter = "All",
    string VersionFilter = "All",
    bool MinimapVisible = true,
    string CollapsedVersionIds = "",
    string ItemTypeFilter = "All",
    string CategoryFilter = "All",
    bool WarningsOnly = false)
{
    public static CanvasViewState Default { get; } = new(1, 0, 0);

    public IReadOnlyList<Guid> ParseCollapsedVersionIds() =>
        CollapsedVersionIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(static id => id != Guid.Empty)
            .Distinct()
            .ToArray();
}
