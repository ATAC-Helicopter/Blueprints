namespace Blueprints.App.Models;

public sealed record SourceChangeSummary(
    string CommitHash,
    string ShortHash,
    string Subject,
    string AuthorName,
    DateTimeOffset CommittedUtc,
    IReadOnlyList<string> MatchedItemKeys)
{
    public string ItemKeySummary =>
        MatchedItemKeys.Count == 0
            ? "No item key"
            : string.Join(", ", MatchedItemKeys);
}
