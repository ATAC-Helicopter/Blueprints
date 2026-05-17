namespace Blueprints.App.Models;

public sealed record LocalGitRepositoryStatus(
    bool IsRepository,
    string RepositoryRoot,
    string Branch,
    string RemoteUrl,
    bool IsDirty,
    string LatestTag,
    IReadOnlyList<SourceChangeSummary> RecentChanges,
    string Summary);
