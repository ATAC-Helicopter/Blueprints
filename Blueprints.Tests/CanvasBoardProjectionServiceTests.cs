using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;

namespace Blueprints.Tests;

public sealed class CanvasBoardProjectionServiceTests
{
    [Fact]
    public void Plan_ProjectsVersionFramesAndLifecycleColumnsWithoutOwnershipEdges()
    {
        var versionId = Guid.NewGuid();
        var items = Enum.GetValues<WorkItemLifecycle>()
            .Select((state, index) => new WorkspaceItemCard(
                Guid.NewGuid(),
                $"BP-{index + 1}",
                "feature",
                index % 2 == 0 ? "added" : "fixed",
                $"{state} work",
                null,
                state == WorkItemLifecycle.Complete,
                state))
            .ToArray();
        var version = new WorkspaceVersionCard(
            versionId,
            "1.0.0",
            ReleaseStatus.InProgress,
            null,
            items.Length,
            1,
            items);

        var projection = CanvasBoardProjectionService.Build(
            CanvasViewMode.Plan,
            [version],
            relationshipDocument: null);

        var frame = Assert.Single(projection.Frames);
        Assert.Equal(4, frame.Columns.Count);
        Assert.All(frame.Columns, static column => Assert.Single(column.Items));
        Assert.Empty(projection.Relationships);
        Assert.Equal(25, frame.CompletionPercentage);
    }

    [Fact]
    public void Dependencies_ProjectsOnlyTypedRelationshipsWithDirectionAndLabel()
    {
        var source = CreateItem("BP-1", WorkItemLifecycle.InProgress);
        var target = CreateItem("BP-2", WorkItemLifecycle.Review);
        var version = new WorkspaceVersionCard(
            Guid.NewGuid(),
            "1.0.0",
            ReleaseStatus.InProgress,
            null,
            2,
            0,
            [source, target]);
        var type = new RelationshipTypeDefinition(
            "blocks",
            "Blocks",
            "Must finish first",
            "#C64D42",
            true);
        var edge = new RelationshipEdge(
            Guid.NewGuid(),
            type.TypeId,
            new RelationshipEndpoint("item", source.ItemId),
            new RelationshipEndpoint("item", target.ItemId),
            "Release gate");
        var relationships = new RelationshipDocument(
            1,
            Guid.NewGuid(),
            1,
            [type],
            [edge],
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "Tester");

        var projection = CanvasBoardProjectionService.Build(
            CanvasViewMode.Dependencies,
            [version],
            relationships);

        var projected = Assert.Single(projection.Relationships);
        Assert.True(projected.Type.IsDirectional);
        Assert.Equal("Release gate", projected.Edge.Label);
        Assert.Equal("#C64D42", projected.Type.ColorHex);
        Assert.Equal(3, projection.DependencyNodes.Count);
    }

    [Fact]
    public void Filters_AreViewOnlyAndRetainOriginalCards()
    {
        var planned = CreateItem("BP-1", WorkItemLifecycle.Planned);
        var review = CreateItem("BP-2", WorkItemLifecycle.Review);
        var version = new WorkspaceVersionCard(
            Guid.NewGuid(),
            "2.0.0",
            ReleaseStatus.Planned,
            null,
            2,
            0,
            [planned, review]);

        var projection = CanvasBoardProjectionService.Build(
            CanvasViewMode.Plan,
            [version],
            null,
            new CanvasBoardFilter(Lifecycle: "Review"));

        Assert.Single(projection.Frames.Single().Columns.Single(column =>
            column.State == WorkItemLifecycle.Review).Items);
        Assert.Equal(2, version.Items.Count);
        Assert.Equal(WorkItemLifecycle.Planned, planned.WorkflowState);
    }

    [Fact]
    public void Dependencies_ProjectsLargeBoardsWithinConfiguredDomainLimits()
    {
        var items = Enumerable.Range(1, 600)
            .Select(index => CreateItem($"BP-{index}", (WorkItemLifecycle)(index % 4)))
            .ToArray();
        var version = new WorkspaceVersionCard(
            Guid.NewGuid(),
            "3.0.0",
            ReleaseStatus.InProgress,
            null,
            items.Length,
            items.Count(static item => item.IsDone),
            items);
        var type = new RelationshipTypeDefinition("relates", "Relates", null, "#6254D9", false);
        var edges = Enumerable.Range(0, 1_200)
            .Select(index => new RelationshipEdge(
                Guid.NewGuid(),
                type.TypeId,
                new RelationshipEndpoint("item", items[index % items.Length].ItemId),
                new RelationshipEndpoint("item", items[(index + index / items.Length + 1) % items.Length].ItemId),
                null))
            .ToArray();
        var document = new RelationshipDocument(
            1,
            Guid.NewGuid(),
            1,
            [type],
            edges,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "Tester");

        var projection = CanvasBoardProjectionService.Build(
            CanvasViewMode.Dependencies,
            [version],
            document);

        Assert.Equal(601, projection.DependencyNodes.Count);
        Assert.Equal(1_200, projection.Relationships.Count);
    }

    private static WorkspaceItemCard CreateItem(string key, WorkItemLifecycle state) =>
        new(
            Guid.NewGuid(),
            key,
            "feature",
            "added",
            $"{key} title",
            null,
            state == WorkItemLifecycle.Complete,
            state);
}
