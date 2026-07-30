using System.Text.RegularExpressions;
using Blueprints.Core.Models;

namespace Blueprints.Core.Services;

public static partial class RelationshipDocumentValidator
{
    public const int MaximumTypeCount = 100;
    public const int MaximumRelationshipCount = 5_000;

    private static readonly HashSet<string> SupportedNodeTypes =
        new(StringComparer.Ordinal)
        {
            "project",
            "version",
            "item",
        };

    public static void Validate(
        RelationshipDocument document,
        Guid expectedProjectId)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Relationship schema {document.SchemaVersion} is not supported.");
        }

        if (document.ProjectId == Guid.Empty || document.ProjectId != expectedProjectId)
        {
            throw new InvalidOperationException(
                "Relationship project identity does not match the workspace.");
        }

        if (document.Revision < 1)
        {
            throw new InvalidOperationException("Relationship revision must be at least 1.");
        }

        if (document.Types is null || document.Relationships is null)
        {
            throw new InvalidOperationException(
                "Relationship type and relationship collections are required.");
        }

        if (document.Types.Count > MaximumTypeCount)
        {
            throw new InvalidOperationException(
                $"Relationship documents cannot define more than {MaximumTypeCount} types.");
        }

        if (document.Relationships.Count > MaximumRelationshipCount)
        {
            throw new InvalidOperationException(
                $"Relationship documents cannot contain more than {MaximumRelationshipCount} relationships.");
        }

        var types = new Dictionary<string, RelationshipTypeDefinition>(StringComparer.Ordinal);
        foreach (var type in document.Types)
        {
            if (type is null)
            {
                throw new InvalidOperationException("Relationship type entries cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(type.TypeId) ||
                !TypeIdPattern().IsMatch(type.TypeId))
            {
                throw new InvalidOperationException(
                    $"Relationship type ID '{type.TypeId}' must be a lowercase slug of at most 32 characters.");
            }

            ValidateText(type.Name, 80, "Relationship type name", required: true);
            ValidateText(type.Description, 500, "Relationship type description", required: false);
            if (string.IsNullOrWhiteSpace(type.ColorHex) ||
                !ColorPattern().IsMatch(type.ColorHex))
            {
                throw new InvalidOperationException(
                    $"Relationship type '{type.TypeId}' requires a #RRGGBB color.");
            }

            if (!types.TryAdd(type.TypeId, type))
            {
                throw new InvalidOperationException(
                    $"Relationship type '{type.TypeId}' is duplicated.");
            }
        }

        var relationshipIds = new HashSet<Guid>();
        var logicalRelationships = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relationship in document.Relationships)
        {
            if (relationship is null)
            {
                throw new InvalidOperationException("Relationship entries cannot be null.");
            }

            if (relationship.RelationshipId == Guid.Empty
                || !relationshipIds.Add(relationship.RelationshipId))
            {
                throw new InvalidOperationException(
                    "Relationship IDs must be non-empty and unique.");
            }

            if (string.IsNullOrWhiteSpace(relationship.TypeId) ||
                !types.TryGetValue(relationship.TypeId, out var type))
            {
                throw new InvalidOperationException(
                    $"Relationship '{relationship.RelationshipId}' references unknown type '{relationship.TypeId}'.");
            }

            ValidateEndpoint(relationship.Source, "source");
            ValidateEndpoint(relationship.Target, "target");
            if (relationship.Source == relationship.Target)
            {
                throw new InvalidOperationException("A relationship cannot connect a node to itself.");
            }

            ValidateText(relationship.Label, 120, "Relationship label", required: false);
            var logicalKey = LogicalKey(relationship, type.IsDirectional);
            if (!logicalRelationships.Add(logicalKey))
            {
                throw new InvalidOperationException(
                    "Duplicate relationships with the same type and endpoints are not allowed.");
            }
        }

        ValidateText(
            document.LastModifiedByName,
            200,
            "Relationship modifier name",
            required: true);
        if (document.LastModifiedByUserId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Relationship documents require a last-modifier identity.");
        }

        if (document.UpdatedUtc == default)
        {
            throw new InvalidOperationException(
                "Relationship documents require an update timestamp.");
        }
    }

    public static void ValidateEntityReferences(
        RelationshipDocument document,
        Guid projectId,
        IReadOnlySet<Guid> versionIds,
        IReadOnlySet<Guid> itemIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(versionIds);
        ArgumentNullException.ThrowIfNull(itemIds);

        foreach (var relationship in document.Relationships)
        {
            ValidateEntityReference(relationship.Source, projectId, versionIds, itemIds);
            ValidateEntityReference(relationship.Target, projectId, versionIds, itemIds);
        }
    }

    private static void ValidateEndpoint(RelationshipEndpoint endpoint, string role)
    {
        if (endpoint is null)
        {
            throw new InvalidOperationException($"Relationship {role} is required.");
        }
        if (string.IsNullOrWhiteSpace(endpoint.NodeType) ||
            !SupportedNodeTypes.Contains(endpoint.NodeType))
        {
            throw new InvalidOperationException(
                $"Relationship {role} node type '{endpoint.NodeType}' is not supported.");
        }

        if (endpoint.EntityId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Relationship {role} requires a non-empty entity ID.");
        }
    }

    private static void ValidateEntityReference(
        RelationshipEndpoint endpoint,
        Guid projectId,
        IReadOnlySet<Guid> versionIds,
        IReadOnlySet<Guid> itemIds)
    {
        var exists = endpoint.NodeType switch
        {
            "project" => endpoint.EntityId == projectId,
            "version" => versionIds.Contains(endpoint.EntityId),
            "item" => itemIds.Contains(endpoint.EntityId),
            _ => false,
        };
        if (!exists)
        {
            throw new InvalidOperationException(
                $"Relationship endpoint '{endpoint.NodeType}/{endpoint.EntityId}' does not exist in this workspace.");
        }
    }

    private static string LogicalKey(
        RelationshipEdge relationship,
        bool directional)
    {
        var source = $"{relationship.Source.NodeType}/{relationship.Source.EntityId:N}";
        var target = $"{relationship.Target.NodeType}/{relationship.Target.EntityId:N}";
        if (!directional && string.CompareOrdinal(source, target) > 0)
        {
            (source, target) = (target, source);
        }

        return $"{relationship.TypeId}:{source}:{target}";
    }

    private static void ValidateText(
        string? value,
        int maximumLength,
        string fieldName,
        bool required)
    {
        if (required && string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        if (value?.Length > maximumLength)
        {
            throw new InvalidOperationException(
                $"{fieldName} cannot exceed {maximumLength} characters.");
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex TypeIdPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
