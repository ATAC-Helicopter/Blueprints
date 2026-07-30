using System.Diagnostics;
using System.Text.RegularExpressions;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed partial class RepositorySourceDiscoveryService : ISourceDiscoveryService
{
    private readonly MarkdownSourceDiscoveryParser _markdownParser;
    private readonly IHostedSourceProviderReader _hostedSourceProviderReader;

    public RepositorySourceDiscoveryService()
        : this(
            new MarkdownSourceDiscoveryParser(),
            new GitHubRestSourceProviderReader())
    {
    }

    public RepositorySourceDiscoveryService(MarkdownSourceDiscoveryParser markdownParser)
        : this(markdownParser, new GitHubRestSourceProviderReader())
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

        var repositoryName = ReadGitHubRepositoryName(root);
        var githubIssueCount = 0;
        var githubProjectCount = 0;
        var pullRequestCount = 0;
        var releaseCount = 0;
        if (string.IsNullOrWhiteSpace(repositoryName))
        {
            warnings.Add("GitHub sources were skipped because the origin remote is not a recognizable GitHub repository.");
        }
        else
        {
            var hosted = _hostedSourceProviderReader.Read(root, repositoryName);
            candidates.AddRange(hosted.Candidates);
            warnings.AddRange(hosted.Warnings);
            githubIssueCount = hosted.IssueCount;
            githubProjectCount = hosted.ProjectCount;
            pullRequestCount = hosted.PullRequestCount;
            releaseCount = hosted.ReleaseCount;
        }

        var deduplicated = candidates
            .GroupBy(
                static candidate => $"{candidate.Kind}:{NormalizeTitle(candidate.Title)}",
                StringComparer.Ordinal)
            .Select(static group => group.First())
            .Take(250)
            .ToArray();

        return new SourceDiscoveryResult(
            deduplicated,
            warnings,
            changelogCount,
            roadmapCount,
            githubIssueCount,
            githubProjectCount,
            pullRequestCount,
            releaseCount);
    }

    private int AddMarkdownCandidates(
        string root,
        SourceArtifactKind kind,
        ICollection<SourceDiscoveryCandidate> candidates)
    {
        var names = kind == SourceArtifactKind.Changelog
            ? new[] { "CHANGELOG.md", "Changelog.md", "changelog.md" }
            : new[] { "Roadmap.md", "ROADMAP.md", "roadmap.md" };
        var paths = names
            .Select(name => Path.Combine(root, name))
            .Concat(names.Select(name => Path.Combine(root, "docs", name)))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
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

    private static string ReadGitHubRepositoryName(string root)
    {
        var remote = RunProcess(root, "git", ["-C", root, "config", "--get", "remote.origin.url"]);
        if (!remote.Success)
        {
            return string.Empty;
        }

        var match = GitHubRemotePattern().Match(remote.Output.Trim());
        if (!match.Success)
        {
            return string.Empty;
        }

        var repository = match.Groups["repo"].Value;
        if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repository = repository[..^4];
        }

        return $"{match.Groups["owner"].Value}/{repository}";
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    private sealed record ProcessResult(bool Success, string Output, string Error);

}
