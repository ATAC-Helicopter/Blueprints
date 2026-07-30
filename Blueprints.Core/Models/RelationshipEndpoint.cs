namespace Blueprints.Core.Models;

public sealed record RelationshipEndpoint(
    string NodeType,
    Guid EntityId);
