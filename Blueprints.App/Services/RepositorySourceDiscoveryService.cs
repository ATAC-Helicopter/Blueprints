using System.Diagnostics;
using System.Text.RegularExpressions;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed partial class RepositorySourceDiscoveryService : ISourceDiscoveryService
{
    public const int MaximumCandidatesPerRepository = 5_000;
    private readonly MarkdownSourceDiscoveryParser _markdownParser;
    private readonly IHostedSourceProviderReader _hostedSourceProviderReader;

    public RepositorySourceDiscoveryService()
        : this(
            new MarkdownSourceDiscoveryParser(),
            new HostedSourceProviderRouter())
    {
    }

    public RepositorySourceDiscoveryService(MarkdownSourceDiscoveryParser markdownParser)
        : this(markdownParser, new HostedSourceProviderRouter())
    {
    }

    public RepositorySourceDiscoveryService(
        MarkdownSourceDiscoveryParser markdownParser,
        IHostedSourceProviderReader hostedSourceProviderReader)
    {
        ArgumentNullException.ThrowIfNull(markdownParser);
        ArgumentNullException.ThrowIfNull(hostedSourceProviderReader);
        _markdownParser = markdownParser;
        _hostedSourceProviderReader = hostedSourceProviderReader;
    }

    public SourceDiscoveryResult Discover(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var root = Path.GetFullPath(repositoryPath.Trim());
        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException("The linked repository path does not exist.");
        }

        var candidates = new List<SourceDiscoveryCandidate>();
        var warnings = new List<string>();
        var changelogCount = AddMarkdownCandidates(root, SourceArtifactKind.Changelog, candidates);
        var roadmapCount = AddMarkdownCandidates(root, SourceArtifactKind.Roadmap, candidates);

        var hostedRepository = ReadHostedRepository(root);
        var hostedIssueCount = 0;
        var hostedPlanningCount = 0;
        var changeRequestCount = 0;
        var releaseCount = 0;
        if (hostedRepository is null)
        {
            warnings.Add(
                "Hosted issue and release metadata is available for GitHub and GitLab. Local Git and planning files were still scanned for this repository.");
        }
        else
        {
            var hosted = _hostedSourceProviderReader.Read(root, hostedRepository);
            candidates.AddRange(hosted.Candidates);
            warnings.AddRange(hosted.Warnings);
            hostedIssueCount = hosted.IssueCount;
            hostedPlanningCount = hosted.ProjectCount;
            changeRequestCount = hosted.ChangeRequestCount;
            releaseCount = hosted.ReleaseCount;
        }

        var deduplicated = candidates
            .GroupBy(
                static candidate => $"{candidate.Kind}:{NormalizeTitle(candidate.Title)}",
                StringComparer.Ordinal)
            .Select(static group => group.First())
            .Take(MaximumCandidatesPerRepository)
            .ToArray();

        return new SourceDiscoveryResult(
            deduplicated,
            warnings,
            changelogCount,
            roadmapCount,
            hostedIssueCount,
            hostedPlanningCount,
            changeRequestCount,
            releaseCount);
    }

    private int AddMarkdownCandidates(
        string root,
        SourceArtifactKind kind,
        ICollection<SourceDiscoveryCandidate> candidates)
    {
        var names = kind == SourceArtifactKind.Changelog
            ? new[] { "CHANGELOG.md", "Changelog.md", "changelog.md", "RELEASES.md", "ReleaseNotes.md", "RELEASE_NOTES.md" }
            : new[] { "Roadmap.md", "ROADMAP.md", "roadmap.md", "PLAN.md", "Plan.md", "TODO.md", "BACKLOG.md" };
        var paths = names
            .Select(name => Path.Combine(root, name))
            .Concat(names.Select(name => Path.Combine(root, "docs", name)))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        var countBefore = candidates.Count;
        foreach (var path in paths)
        {
            foreach (var candidate in _markdownParser.Parse(path, kind))
            {
                candidates.Add(candidate);
            }
        }

        return candidates.Count - countBefore;
    }

    private static HostedRepositoryDescriptor? ReadHostedRepository(string root)
    {
        var remote = RunProcess(root, "git", ["-C", root, "config", "--get", "remote.origin.url"]);
        if (!remote.Success)
        {
            return null;
        }

        var remoteUrl = remote.Output.Trim();
        var gitHubMatch = GitHubRemotePattern().Match(remoteUrl);
        if (gitHubMatch.Success)
        {
            var repository = RemoveGitSuffix(gitHubMatch.Groups["repo"].Value);
            return new HostedRepositoryDescriptor(
                SourceProviderKind.GitHub,
                $"{gitHubMatch.Groups["owner"].Value}/{repository}");
        }

        var gitLabMatch = GitLabRemotePattern().Match(remoteUrl);
        return gitLabMatch.Success
            ? new HostedRepositoryDescriptor(
                SourceProviderKind.GitLab,
                RemoveGitSuffix(gitLabMatch.Groups["repo"].Value))
            : null;
    }

    private static string RemoveGitSuffix(string repository)
    {
        if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repository = repository[..^4];
        }

        return repository;
    }

    private static ProcessResult RunProcess(
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start {fileName}.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(15_000))
            {
                process.Kill(entireProcessTree: true);
                return new ProcessResult(false, string.Empty, $"{fileName} timed out after 15 seconds.");
            }

            return process.ExitCode == 0
                ? new ProcessResult(true, output.Trim(), string.Empty)
                : new ProcessResult(false, string.Empty, string.IsNullOrWhiteSpace(error) ? $"{fileName} exited with code {process.ExitCode}." : error.Trim());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ProcessResult(false, string.Empty, $"{fileName} is unavailable: {exception.Message}");
        }
    }

    private static string NormalizeTitle(string title) =>
        WhitespacePattern().Replace(title.Trim().ToUpperInvariant(), " ");

    [GeneratedRegex(@"(?:github\.com[:/])(?<owner>[^/\s]+)/(?<repo>[^/\s]+)$")]
    private static partial Regex GitHubRemotePattern();

    [GeneratedRegex(@"(?:gitlab\.com[:/])(?<repo>[^\s]+)$")]
    private static partial Regex GitLabRemotePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    private sealed record ProcessResult(bool Success, string Output, string Error);

}
