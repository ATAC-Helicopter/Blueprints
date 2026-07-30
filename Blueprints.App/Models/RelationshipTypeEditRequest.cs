namespace Blueprints.App.Models;

public sealed record RelationshipTypeEditRequest(
    string TypeId,
    string Name,
    string? Description,
    string ColorHex,
    bool IsDirectional);
