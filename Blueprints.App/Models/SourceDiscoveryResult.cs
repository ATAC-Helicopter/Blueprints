namespace Blueprints.App.Models;

public sealed record SourceDiscoveryResult(
    IReadOnlyList<SourceDiscoveryCandidate> Candidates,
    IReadOnlyList<string> Warnings,
    int ChangelogCount,
    int RoadmapCount,
    int GitHubIssueCount,
    int GitHubProjectCount)
{
    public string Summary =>
        $"Found {Candidates.Count} proposals · {ChangelogCount} changelog · {RoadmapCount} roadmap · " +
        $"{GitHubIssueCount} issues · {GitHubProjectCount} project-linked";
}
