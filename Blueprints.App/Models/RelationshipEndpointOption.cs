using Blueprints.Core.Models;

namespace Blueprints.App.Models;

public sealed record RelationshipEndpointOption(
    RelationshipEndpoint Endpoint,
    string DisplayName);
