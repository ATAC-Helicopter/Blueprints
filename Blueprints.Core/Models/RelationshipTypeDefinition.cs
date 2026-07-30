namespace Blueprints.Core.Models;

public sealed record RelationshipTypeDefinition(
    string TypeId,
    string Name,
    string? Description,
    string ColorHex,
    bool IsDirectional);
