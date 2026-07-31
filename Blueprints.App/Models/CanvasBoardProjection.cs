using Blueprints.Core.Enums;
using Blueprints.Core.Models;

namespace Blueprints.App.Models;

public sealed record CanvasBoardProjection(
    CanvasViewMode ViewMode,
    IReadOnlyList<CanvasVersionFrame> Frames,
    IReadOnlyList<CanvasDependencyNode> DependencyNodes,
    IReadOnlyList<CanvasRelationshipProjection> Relationships);

public sealed record CanvasVersionFrame(
    WorkspaceVersionCard Version,
    IReadOnlyList<CanvasLifecycleColumn> Columns,
    int CompletionPercentage,
    int WarningCount,
    int BlockerCount,
    string ReadinessSummary);

public sealed record CanvasLifecycleColumn(
    WorkItemLifecycle State,
    string DisplayName,
    IReadOnlyList<WorkspaceItemCard> Items);

public sealed record CanvasDependencyNode(
    string NodeType,
    Guid EntityId,
    string Key,
    string Title,
    string Subtitle,
    Guid? VersionId,
    WorkItemLifecycle? WorkflowState);

public sealed record CanvasRelationshipProjection(
    RelationshipEdge Edge,
    RelationshipTypeDefinition Type);

public sealed record CanvasBoardFilter(
    string SearchText = "",
    string Version = "All",
    string Lifecycle = "All",
    string ItemType = "All",
    string Category = "All",
    bool WarningsOnly = false);
