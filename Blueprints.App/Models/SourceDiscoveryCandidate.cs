namespace Blueprints.App.Models;

public sealed record SourceDiscoveryCandidate(
    SourceArtifactKind Kind,
    string Title,
    string? Description,
    string SuggestedItemTypeId,
    string SuggestedCategoryId,
    bool IsDone,
    string SourceReference,
    string SourceContext,
    double Confidence);
