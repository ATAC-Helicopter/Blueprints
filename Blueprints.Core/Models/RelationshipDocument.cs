namespace Blueprints.Core.Models;

public sealed record RelationshipDocument(
    int SchemaVersion,
    Guid ProjectId,
    int Revision,
    IReadOnlyList<RelationshipTypeDefinition> Types,
    IReadOnlyList<RelationshipEdge> Relationships,
    DateTimeOffset UpdatedUtc,
    Guid LastModifiedByUserId,
    string LastModifiedByName);
