namespace Blueprints.App.Models;

public sealed record SourceDiscoveryResult(
    IReadOnlyList<SourceDiscoveryCandidate> Candidates,
    IReadOnlyList<string> Warnings,
    int ChangelogCount,
    int RoadmapCount,
    int GitHubIssueCount,
    int GitHubProjectCount,
    int PullRequestCount = 0,
    int ReleaseCount = 0)
{
    public string Summary =>
        $"Found {Candidates.Count} proposals · {ChangelogCount} changelog · {RoadmapCount} roadmap · " +
        $"{GitHubIssueCount} issues · {PullRequestCount} pull requests · {ReleaseCount} releases · " +
        $"{GitHubProjectCount} project-linked";
}
