namespace Blueprints.App.Models;

public sealed record HostedSourceDiscoveryResult(
    IReadOnlyList<SourceDiscoveryCandidate> Candidates,
    IReadOnlyList<string> Warnings,
    int IssueCount,
    int ProjectCount,
    int ChangeRequestCount,
    int ReleaseCount);
