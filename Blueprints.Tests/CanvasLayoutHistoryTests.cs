using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class CanvasLayoutHistoryTests
{
    [Fact]
    public void RecordUndoAndRedo_RoundTripLayoutSnapshots()
    {
        var nodeId = Guid.NewGuid();
        CanvasNodeLayoutEdit[] original = [new("version", nodeId, 100, 200)];
        CanvasNodeLayoutEdit[] moved = [new("version", nodeId, 300, 400)];
        var history = new CanvasLayoutHistory();

        Assert.True(history.Record(original, moved));
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        Assert.True(history.TryUndo(moved, out var undone));
        Assert.Equal(original, undone);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);

        Assert.True(history.TryRedo(undone, out var redone));
        Assert.Equal(moved, redone);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Record_IgnoresEquivalentLayoutsAndClearsRedoAfterANewEdit()
    {
        var firstNodeId = Guid.NewGuid();
        var secondNodeId = Guid.NewGuid();
        CanvasNodeLayoutEdit[] original =
        [
            new("item", firstNodeId, 10, 20),
            new("version", secondNodeId, 30, 40),
        ];
        CanvasNodeLayoutEdit[] sameInAnotherOrder =
        [
            new("version", secondNodeId, 30, 40),
            new("item", firstNodeId, 10, 20),
        ];
        CanvasNodeLayoutEdit[] moved =
        [
            new("item", firstNodeId, 30, 40),
            new("version", secondNodeId, 30, 40),
        ];
        CanvasNodeLayoutEdit[] movedAgain =
        [
            new("item", firstNodeId, 50, 60),
            new("version", secondNodeId, 30, 40),
        ];
        var history = new CanvasLayoutHistory();

        Assert.False(history.Record(original, sameInAnotherOrder));
        Assert.False(history.CanUndo);

        history.Record(original, moved);
        history.TryUndo(moved, out _);
        Assert.True(history.CanRedo);

        history.Record(original, movedAgain);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Capacity_DiscardsTheOldestUndoSnapshot()
    {
        var nodeId = Guid.NewGuid();
        var history = new CanvasLayoutHistory(capacity: 2);
        var first = LayoutAt(nodeId, 10);
        var second = LayoutAt(nodeId, 20);
        var third = LayoutAt(nodeId, 30);
        var fourth = LayoutAt(nodeId, 40);

        history.Record(first, second);
        history.Record(second, third);
        history.Record(third, fourth);

        Assert.True(history.TryUndo(fourth, out var undoThird));
        Assert.Equal(third, undoThird);
        Assert.True(history.TryUndo(undoThird, out var undoSecond));
        Assert.Equal(second, undoSecond);
        Assert.False(history.TryUndo(undoSecond, out _));
    }

    private static CanvasNodeLayoutEdit[] LayoutAt(Guid nodeId, double coordinate) =>
        [new("project", nodeId, coordinate, coordinate)];
}
