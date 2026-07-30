using Blueprints.Core.Models;

namespace Blueprints.App.Models;

public sealed record RelationshipEditRequest(
    Guid? RelationshipId,
    string TypeId,
    RelationshipEndpoint Source,
    RelationshipEndpoint Target,
    string? Label);
