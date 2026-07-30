using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class CanvasLayoutHistory
{
    private readonly int _capacity;
    private readonly List<IReadOnlyList<CanvasNodeLayoutEdit>> _undo = [];
    private readonly List<IReadOnlyList<CanvasNodeLayoutEdit>> _redo = [];

    public CanvasLayoutHistory(int capacity = 50)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public bool Record(
        IReadOnlyList<CanvasNodeLayoutEdit> previous,
        IReadOnlyList<CanvasNodeLayoutEdit> current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (AreEquivalent(previous, current))
        {
            return false;
        }

        AddBounded(_undo, Clone(previous));
        _redo.Clear();
        return true;
    }

    public bool TryUndo(
        IReadOnlyList<CanvasNodeLayoutEdit> current,
        out IReadOnlyList<CanvasNodeLayoutEdit> previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_undo.Count == 0)
        {
            previous = [];
            return false;
        }

        AddBounded(_redo, Clone(current));
        previous = TakeLast(_undo);
        return true;
    }

    public bool TryRedo(
        IReadOnlyList<CanvasNodeLayoutEdit> current,
        out IReadOnlyList<CanvasNodeLayoutEdit> next)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_redo.Count == 0)
        {
            next = [];
            return false;
        }

        AddBounded(_undo, Clone(current));
        next = TakeLast(_redo);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private static bool AreEquivalent(
        IReadOnlyList<CanvasNodeLayoutEdit> left,
        IReadOnlyList<CanvasNodeLayoutEdit> right) =>
        left.Count == right.Count
        && left
            .OrderBy(static node => node.NodeType, StringComparer.Ordinal)
            .ThenBy(static node => node.EntityId)
            .SequenceEqual(
                right
                    .OrderBy(static node => node.NodeType, StringComparer.Ordinal)
                    .ThenBy(static node => node.EntityId));

    private static IReadOnlyList<CanvasNodeLayoutEdit> Clone(
        IReadOnlyList<CanvasNodeLayoutEdit> snapshot) =>
        snapshot.ToArray();

    private static IReadOnlyList<CanvasNodeLayoutEdit> TakeLast(
        List<IReadOnlyList<CanvasNodeLayoutEdit>> snapshots)
    {
        var index = snapshots.Count - 1;
        var snapshot = snapshots[index];
        snapshots.RemoveAt(index);
        return snapshot;
    }

    private void AddBounded(
        List<IReadOnlyList<CanvasNodeLayoutEdit>> snapshots,
        IReadOnlyList<CanvasNodeLayoutEdit> snapshot)
    {
        snapshots.Add(snapshot);
        if (snapshots.Count > _capacity)
        {
            snapshots.RemoveAt(0);
        }
    }
}
