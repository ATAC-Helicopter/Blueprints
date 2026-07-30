using Blueprints.Core.Models;

namespace Blueprints.Core.Services;

public static class CanvasLayoutValidator
{
    public const double MaximumCoordinate = 100_000;
    public const int MaximumNodeCount = 10_000;

    private static readonly HashSet<string> SupportedNodeTypes =
        new(StringComparer.Ordinal)
        {
            "project",
            "version",
            "item",
        };

    public static void Validate(CanvasLayoutDocument layout, Guid expectedProjectId)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Canvas layout schema {layout.SchemaVersion} is not supported.");
        }

        if (layout.ProjectId == Guid.Empty || layout.ProjectId != expectedProjectId)
        {
            throw new InvalidOperationException("Canvas layout project identity does not match the workspace.");
        }

        if (layout.Revision < 1)
        {
            throw new InvalidOperationException("Canvas layout revision must be at least 1.");
        }

        if (layout.Nodes.Count > MaximumNodeCount)
        {
            throw new InvalidOperationException($"Canvas layout cannot contain more than {MaximumNodeCount} nodes.");
        }

        var identities = new HashSet<(string NodeType, Guid EntityId)>();
        foreach (var node in layout.Nodes)
        {
            if (!SupportedNodeTypes.Contains(node.NodeType))
            {
                throw new InvalidOperationException($"Canvas node type '{node.NodeType}' is not supported.");
            }

            if (node.EntityId == Guid.Empty)
            {
                throw new InvalidOperationException("Canvas nodes require a non-empty entity ID.");
            }

            if (!identities.Add((node.NodeType, node.EntityId)))
            {
                throw new InvalidOperationException($"Canvas node '{node.NodeType}/{node.EntityId}' is duplicated.");
            }

            ValidateFiniteRange(node.X, 0, MaximumCoordinate, "Canvas node X coordinate");
            ValidateFiniteRange(node.Y, 0, MaximumCoordinate, "Canvas node Y coordinate");
        }
    }

    public static void ValidateEntityReferences(
        CanvasLayoutDocument layout,
        Guid projectId,
        IReadOnlySet<Guid> versionIds,
        IReadOnlySet<Guid> itemIds)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(versionIds);
        ArgumentNullException.ThrowIfNull(itemIds);

        foreach (var node in layout.Nodes)
        {
            var exists = node.NodeType switch
            {
                "project" => node.EntityId == projectId,
                "version" => versionIds.Contains(node.EntityId),
                "item" => itemIds.Contains(node.EntityId),
                _ => false,
            };
            if (!exists)
            {
                throw new InvalidOperationException(
                    $"Canvas node '{node.NodeType}/{node.EntityId}' does not reference an entity in this workspace.");
            }
        }
    }

    private static void ValidateFiniteRange(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"{name} must be between {minimum} and {maximum}.");
        }
    }
}
