using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed partial class RepositorySourceDiscoveryService : ISourceDiscoveryService
{
    private const int MaximumGitHubIssues = 100;
    private readonly MarkdownSourceDiscoveryParser _markdownParser;

    public RepositorySourceDiscoveryService()
        : this(new MarkdownSourceDiscoveryParser())
    {
    }

    public RepositorySourceDiscoveryService(MarkdownSourceDiscoveryParser markdownParser)
    {
        _markdownParser = markdownParser;
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
        if (string.IsNullOrWhiteSpace(repositoryName))
        {
            warnings.Add("GitHub issues were skipped because the origin remote is not a recognizable GitHub repository.");
        }
        else
        {
            var github = ReadGitHubIssues(root, repositoryName);
            candidates.AddRange(github.Candidates);
            warnings.AddRange(github.Warnings);
            githubIssueCount = github.Candidates.Count;
            githubProjectCount = github.Candidates.Count(static candidate => candidate.Kind == SourceArtifactKind.GitHubProject);
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
            githubProjectCount);
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

    private static GitHubDiscovery ReadGitHubIssues(string root, string repositoryName)
    {
        var command = RunProcess(
            root,
            "gh",
            [
                "issue",
                "list",
                "--repo",
                repositoryName,
                "--state",
                "all",
                "--limit",
                MaximumGitHubIssues.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--json",
                "number,title,body,state,labels,milestone,url,projectItems",
            ]);

        if (!command.Success)
        {
            return new GitHubDiscovery(
                [],
                [$"GitHub issues were skipped: {command.Error}"]);
        }

        try
        {
            using var document = JsonDocument.Parse(command.Output);
            var candidates = document.RootElement
                .EnumerateArray()
                .Select(issue => ParseGitHubIssue(issue, repositoryName))
                .ToArray();
            return new GitHubDiscovery(candidates, []);
        }
        catch (JsonException exception)
        {
            return new GitHubDiscovery([], [$"GitHub returned unreadable issue data: {exception.Message}"]);
        }
    }

    private static SourceDiscoveryCandidate ParseGitHubIssue(
        JsonElement issue,
        string repositoryName)
    {
        var number = issue.GetProperty("number").GetInt32();
        var title = issue.GetProperty("title").GetString()?.Trim() ?? $"Issue #{number}";
        var body = issue.TryGetProperty("body", out var bodyElement)
            ? Truncate(bodyElement.GetString()?.Trim(), 2_000)
            : null;
        var state = issue.TryGetProperty("state", out var stateElement)
            ? stateElement.GetString()
            : null;
        var url = issue.TryGetProperty("url", out var urlElement)
            ? urlElement.GetString()
            : null;
        var labels = issue.TryGetProperty("labels", out var labelsElement)
            ? labelsElement.EnumerateArray()
                .Select(static label => label.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToArray()
            : [];
        var milestone = issue.TryGetProperty("milestone", out var milestoneElement) &&
                        milestoneElement.ValueKind == JsonValueKind.Object &&
                        milestoneElement.TryGetProperty("title", out var milestoneTitle)
            ? milestoneTitle.GetString()
            : null;
        var projectNames = ReadProjectNames(issue);
        var isProjectLinked = projectNames.Count > 0;
        var contextParts = new[]
            {
                milestone is null ? null : $"Milestone: {milestone}",
                projectNames.Count == 0 ? null : $"Projects: {string.Join(", ", projectNames)}",
                labels.Length == 0 ? null : $"Labels: {string.Join(", ", labels)}",
            }
            .Where(static value => value is not null);

        return new SourceDiscoveryCandidate(
            isProjectLinked ? SourceArtifactKind.GitHubProject : SourceArtifactKind.GitHubIssue,
            title,
            body,
            SuggestGitHubItemType(labels, title),
            SuggestGitHubCategory(labels, state),
            string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase),
            $"github:#{number}",
            string.Join(" · ", contextParts.DefaultIfEmpty($"GitHub issue #{number}")),
            isProjectLinked ? 0.97 : 0.93,
            new ProviderReference(
                SourceProviderKind.GitHub,
                ProviderReferenceKind.Issue,
                repositoryName,
                $"#{number}",
                url));
    }

    private static IReadOnlyList<string> ReadProjectNames(JsonElement issue)
    {
        if (!issue.TryGetProperty("projectItems", out var projects) ||
            projects.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return projects
            .EnumerateArray()
            .Select(static project =>
            {
                if (project.TryGetProperty("title", out var title))
                {
                    return title.GetString();
                }

                return project.TryGetProperty("project", out var nested) &&
                       nested.TryGetProperty("title", out var nestedTitle)
                    ? nestedTitle.GetString()
                    : null;
            })
            .Where(static title => !string.IsNullOrWhiteSpace(title))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string SuggestGitHubItemType(IReadOnlyList<string> labels, string title)
    {
        var text = $"{string.Join(' ', labels)} {title}";
        if (text.Contains("security", StringComparison.OrdinalIgnoreCase))
        {
            return "security";
        }

        if (text.Contains("bug", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("fix", StringComparison.OrdinalIgnoreCase))
        {
            return "bug";
        }

        if (text.Contains("feature", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("enhancement", StringComparison.OrdinalIgnoreCase))
        {
            return "feature";
        }

        return "issue";
    }

    private static string SuggestGitHubCategory(IReadOnlyList<string> labels, string? state)
    {
        var text = string.Join(' ', labels);
        if (text.Contains("security", StringComparison.OrdinalIgnoreCase))
        {
            return "security";
        }

        if (text.Contains("bug", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("fix", StringComparison.OrdinalIgnoreCase))
        {
            return "fixed";
        }

        return string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase)
            ? "changed"
            : "added";
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

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maximumLength
                ? value
                : $"{value[..maximumLength]}…";

    [GeneratedRegex(@"(?:github\.com[:/])(?<owner>[^/\s]+)/(?<repo>[^/\s]+)$")]
    private static partial Regex GitHubRemotePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    private sealed record ProcessResult(bool Success, string Output, string Error);

    private sealed record GitHubDiscovery(
        IReadOnlyList<SourceDiscoveryCandidate> Candidates,
        IReadOnlyList<string> Warnings);
}
