namespace Blueprints.App.Models;

public sealed record SourceDiscoveryResult(
    IReadOnlyList<SourceDiscoveryCandidate> Candidates,
    IReadOnlyList<string> Warnings,
    int ChangelogCount,
    int RoadmapCount,
    int HostedIssueCount,
    int HostedPlanningCount,
    int ChangeRequestCount = 0,
    int ReleaseCount = 0)
{
    public string Summary =>
        $"Found {Candidates.Count} proposals · {ChangelogCount} changelog · {RoadmapCount} roadmap · " +
        $"{HostedIssueCount} issues · {ChangeRequestCount} change requests · {ReleaseCount} releases · " +
        $"{HostedPlanningCount} planning-linked";
}
