namespace Blueprints.App.Models;

public sealed record ProjectTrustDocument(
    int SchemaVersion,
    Guid ProjectId,
    IReadOnlyList<TrustedProjectKey> Keys,
    DateTimeOffset UpdatedUtc);
