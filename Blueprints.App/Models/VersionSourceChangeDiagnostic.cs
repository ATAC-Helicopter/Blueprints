namespace Blueprints.App.Models;

public sealed record VersionSourceChangeDiagnostic(
    SourceChangeSummary Change,
    IReadOnlyList<string> MatchingItemKeys,
    bool MatchesSelectedVersion)
{
    public string MatchSummary =>
        MatchesSelectedVersion
            ? string.Join(", ", MatchingItemKeys)
            : "No selected-version item";
}
