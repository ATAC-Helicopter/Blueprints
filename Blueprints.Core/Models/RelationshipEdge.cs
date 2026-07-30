namespace Blueprints.Core.Models;

public sealed record RelationshipEdge(
    Guid RelationshipId,
    string TypeId,
    RelationshipEndpoint Source,
    RelationshipEndpoint Target,
    string? Label);
