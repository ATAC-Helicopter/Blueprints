namespace Blueprints.App.Models;

public sealed record LocalGitRepositoryStatus(
    bool IsRepository,
    string RepositoryRoot,
    string Branch,
    string RemoteUrl,
    bool IsDirty,
    string LatestTag,
    IReadOnlyList<SourceChangeSummary> RecentChanges,
    string Summary)
{
    public bool HasUpstream { get; init; }

    public int AheadCount { get; init; }

    public int BehindCount { get; init; }
}
