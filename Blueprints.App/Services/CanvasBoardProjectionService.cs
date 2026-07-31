using Blueprints.App.Models;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;

namespace Blueprints.App.Services;

public static class CanvasBoardProjectionService
{
    private static readonly WorkItemLifecycle[] LifecycleOrder =
    [
        WorkItemLifecycle.Planned,
        WorkItemLifecycle.InProgress,
        WorkItemLifecycle.Review,
        WorkItemLifecycle.Complete,
    ];

    public static CanvasBoardProjection Build(
        CanvasViewMode viewMode,
        IReadOnlyList<WorkspaceVersionCard> versions,
        RelationshipDocument? relationshipDocument,
        CanvasBoardFilter? filter = null,
        ProjectSummary? project = null)
    {
        ArgumentNullException.ThrowIfNull(versions);
        filter ??= new CanvasBoardFilter();

        var types = (relationshipDocument?.Types ?? [])
            .ToDictionary(static type => type.TypeId, StringComparer.Ordinal);
        var relationships = (relationshipDocument?.Relationships ?? [])
            .Where(edge => types.ContainsKey(edge.TypeId))
            .Select(edge => new CanvasRelationshipProjection(edge, types[edge.TypeId]))
            .ToArray();
        var blockerTargets = relationships
            .Where(static relationship =>
                relationship.Type.TypeId.Contains("block", StringComparison.OrdinalIgnoreCase) ||
                relationship.Type.Name.Contains("block", StringComparison.OrdinalIgnoreCase))
            .Select(static relationship => relationship.Edge.Target.EntityId)
            .ToHashSet();

        var frames = versions
            .Where(version => MatchesVersion(version, filter))
            .Select(version => BuildFrame(version, filter, blockerTargets))
            .ToArray();

        var visibleIds = frames
            .SelectMany(frame => frame.Columns.SelectMany(static column => column.Items))
            .Select(static item => item.ItemId)
            .Concat(frames.Select(static frame => frame.Version.VersionId))
            .ToHashSet();
        if (viewMode == CanvasViewMode.Dependencies &&
            project is { ProjectId: var projectId } &&
            projectId != Guid.Empty)
        {
            visibleIds.Add(projectId);
        }

        var dependencyNodes = (viewMode == CanvasViewMode.Dependencies &&
                               project is { ProjectId: var dependencyProjectId } &&
                               dependencyProjectId != Guid.Empty
                ? new[]
                {
                    new CanvasDependencyNode(
                        "project",
                        dependencyProjectId,
                        project.Code,
                        project.Name,
                        "Project root",
                        null,
                        null),
                }
                : [])
            .Concat(frames
            .SelectMany(frame =>
                new[]
                {
                    new CanvasDependencyNode(
                        "version",
                        frame.Version.VersionId,
                        "VERSION",
                        frame.Version.Name,
                        $"{frame.Version.Status} · {frame.CompletionPercentage}% complete",
                        frame.Version.VersionId,
                        null),
                }
                .Concat(frame.Columns.SelectMany(column => column.Items.Select(item =>
                    new CanvasDependencyNode(
                        "item",
                        item.ItemId,
                        item.ItemKey,
                        item.Title,
                        $"{Format(column.State)} · {item.ItemTypeId} · {item.CategoryId}",
                        frame.Version.VersionId,
                        column.State))))))
            .ToArray();

        return new CanvasBoardProjection(
            viewMode,
            frames,
            dependencyNodes,
            relationships
                .Where(relationship =>
                    visibleIds.Contains(relationship.Edge.Source.EntityId) &&
                    visibleIds.Contains(relationship.Edge.Target.EntityId))
                .ToArray());
    }

    private static CanvasVersionFrame BuildFrame(
        WorkspaceVersionCard version,
        CanvasBoardFilter filter,
        IReadOnlySet<Guid> blockerTargets)
    {
        var items = version.Items
            .Where(item => MatchesItem(item, filter, blockerTargets))
            .ToArray();
        var columns = LifecycleOrder
            .Select(state => new CanvasLifecycleColumn(
                state,
                Format(state),
                items.Where(item => item.WorkflowState == state)
                    .OrderBy(static item => item.ItemKey, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();
        var completion = version.ItemCount == 0
            ? 0
            : (int)Math.Round(version.CompletedItemCount * 100d / version.ItemCount);
        var warnings = version.Items.Count(static item =>
            string.IsNullOrWhiteSpace(item.Title) || !item.IsDone);
        var blockers = version.Items.Count(item => blockerTargets.Contains(item.ItemId));
        var readiness = blockers > 0
            ? $"{blockers} blocker{(blockers == 1 ? string.Empty : "s")}"
            : warnings > 0
                ? $"{warnings} item{(warnings == 1 ? string.Empty : "s")} need attention"
                : version.ItemCount == 0
                    ? "No work items"
                    : "Ready for review";
        return new CanvasVersionFrame(version, columns, completion, warnings, blockers, readiness);
    }

    private static bool MatchesVersion(WorkspaceVersionCard version, CanvasBoardFilter filter) =>
        (filter.Version == "All" ||
         string.Equals(version.Name, filter.Version, StringComparison.OrdinalIgnoreCase))
        && (string.IsNullOrWhiteSpace(filter.SearchText) ||
            version.Name.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase) ||
            version.Items.Any(item => MatchesSearch(item, filter.SearchText)));

    private static bool MatchesItem(
        WorkspaceItemCard item,
        CanvasBoardFilter filter,
        IReadOnlySet<Guid> blockerTargets) =>
        (string.IsNullOrWhiteSpace(filter.SearchText) || MatchesSearch(item, filter.SearchText))
        && (filter.Lifecycle == "All" ||
            string.Equals(Format(item.WorkflowState), filter.Lifecycle, StringComparison.OrdinalIgnoreCase))
        && (filter.ItemType == "All" ||
            string.Equals(item.ItemTypeId, filter.ItemType, StringComparison.OrdinalIgnoreCase))
        && (filter.Category == "All" ||
            string.Equals(item.CategoryId, filter.Category, StringComparison.OrdinalIgnoreCase))
        && (!filter.WarningsOnly || !item.IsDone || blockerTargets.Contains(item.ItemId));

    private static bool MatchesSearch(WorkspaceItemCard item, string search) =>
        item.ItemKey.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.ItemTypeId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.CategoryId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
        (item.Tags?.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase)) ?? false);

    public static string Format(WorkItemLifecycle state) =>
        state == WorkItemLifecycle.InProgress ? "In Progress" : state.ToString();
}
