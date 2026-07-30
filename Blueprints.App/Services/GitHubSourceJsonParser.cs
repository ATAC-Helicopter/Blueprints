using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public static class GitHubSourceJsonParser
{
    public static IReadOnlyList<SourceDiscoveryCandidate> ParseIssues(
        string json,
        string repositoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .EnumerateArray()
            .Take(100)
            .Where(static issue => !issue.TryGetProperty("pull_request", out _))
            .Select(issue => ParseIssue(issue, repositoryName))
            .ToArray();
    }

    public static IReadOnlyList<SourceDiscoveryCandidate> ParseProjectDrafts(
        string json,
        string repositoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("repository", out var repository)
            || repository.ValueKind != JsonValueKind.Object
            || !repository.TryGetProperty("projectsV2", out var projects)
            || !projects.TryGetProperty("nodes", out var projectNodes)
            || projectNodes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return projectNodes
            .EnumerateArray()
            .Take(10)
            .SelectMany(project => ParseProjectDrafts(project, repositoryName))
            .Take(100)
            .ToArray();
    }

    public static IReadOnlyList<SourceDiscoveryCandidate> ParseProjectItems(
        string json,
        string repositoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        using var document = JsonDocument.Parse(json);
        if (!TryReadProjectNodes(document.RootElement, out var projectNodes))
        {
            return [];
        }

        var projects = projectNodes
            .EnumerateArray()
            .Take(10)
            .ToArray();
        var drafts = projects
            .SelectMany(project => ParseProjectDrafts(project, repositoryName))
            .Take(100);
        var linkedIssues = projects
            .SelectMany(project => ParseProjectLinkedIssues(project, repositoryName))
            .Take(100);
        return [.. drafts, .. linkedIssues];
    }

    public static IReadOnlyList<SourceDiscoveryCandidate> ParsePullRequests(
        string json,
        string repositoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .EnumerateArray()
            .Take(100)
            .Select(pullRequest => ParsePullRequest(pullRequest, repositoryName))
            .ToArray();
    }

    public static IReadOnlyList<SourceDiscoveryCandidate> ParseReleases(
        string json,
        string repositoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .EnumerateArray()
            .Take(50)
            .Select(release => ParseRelease(release, repositoryName))
            .ToArray();
    }

    private static SourceDiscoveryCandidate ParsePullRequest(
        JsonElement pullRequest,
        string repositoryName)
    {
        var number = pullRequest.GetProperty("number").GetInt32();
        var title = pullRequest.GetProperty("title").GetString()?.Trim() ?? $"Pull request #{number}";
        var body = ReadString(pullRequest, "body");
        var state = ReadString(pullRequest, "state");
        var mergedAt = ReadString(pullRequest, "mergedAt", "merged_at");
        var url = ReadString(pullRequest, "html_url", "url");
        var labels = ReadLabels(pullRequest);
        var completed = !string.Equals(state, "OPEN", StringComparison.OrdinalIgnoreCase);
        var context = string.IsNullOrWhiteSpace(mergedAt)
            ? $"GitHub pull request #{number} · {state ?? "unknown state"}"
            : $"GitHub pull request #{number} · merged {mergedAt}";

        return new SourceDiscoveryCandidate(
            SourceArtifactKind.PullRequest,
            title,
            Truncate(body, 2_000),
            SuggestItemType(labels, title),
            SuggestCategory(labels, completed),
            completed,
            $"github:pr:#{number}",
            context,
            0.96,
            new ProviderReference(
                SourceProviderKind.GitHub,
                ProviderReferenceKind.PullRequest,
                repositoryName,
                $"#{number}",
                url));
    }

    private static IReadOnlyList<SourceDiscoveryCandidate> ParseProjectDrafts(
        JsonElement project,
        string repositoryName)
    {
        var projectNumber = project.GetProperty("number").GetInt32();
        var projectTitle = ReadString(project, "title") ?? $"Project {projectNumber}";
        var projectUrl = ReadString(project, "url");
        if (!project.TryGetProperty("items", out var items)
            || !items.TryGetProperty("nodes", out var itemNodes)
            || itemNodes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return itemNodes
            .EnumerateArray()
            .Take(100)
            .Where(static item =>
                item.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Object
                && ReadString(content, "__typename") == "DraftIssue")
            .Select(item =>
            {
                var itemId = ReadString(item, "id") ?? "(unknown)";
                var content = item.GetProperty("content");
                var title = ReadString(content, "title") ?? "Untitled project draft";
                var body = ReadString(content, "body");
                return new SourceDiscoveryCandidate(
                    SourceArtifactKind.GitHubProject,
                    title,
                    Truncate(body, 2_000),
                    SuggestItemType([], title),
                    "added",
                    false,
                    $"github:project:{projectNumber}:draft:{itemId}",
                    $"{projectTitle} · standalone draft item",
                    0.9,
                    new ProviderReference(
                        SourceProviderKind.GitHub,
                        ProviderReferenceKind.Project,
                        repositoryName,
                        $"{projectNumber}/draft/{itemId}",
                        projectUrl));
            })
            .ToArray();
    }

    private static IReadOnlyList<SourceDiscoveryCandidate> ParseProjectLinkedIssues(
        JsonElement project,
        string repositoryName)
    {
        var projectNumber = project.GetProperty("number").GetInt32();
        var projectTitle = ReadString(project, "title") ?? $"Project {projectNumber}";
        var projectUrl = ReadString(project, "url");
        if (!project.TryGetProperty("items", out var items) ||
            !items.TryGetProperty("nodes", out var itemNodes) ||
            itemNodes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var linkedIssues = itemNodes
            .EnumerateArray()
            .Take(100)
            .Where(static item =>
                item.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Object &&
                ReadString(content, "__typename") == "Issue")
            .Select(item =>
            {
                var content = item.GetProperty("content");
                var number = content.GetProperty("number").GetInt32();
                var title = ReadString(content, "title") ?? $"Issue #{number}";
                var state = ReadString(content, "state");
                var labels = ReadLabels(content);
                return new SourceDiscoveryCandidate(
                    SourceArtifactKind.GitHubProject,
                    title,
                    Truncate(ReadString(content, "body"), 2_000),
                    SuggestItemType(labels, title),
                    SuggestCategory(
                        labels,
                        string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase)),
                    string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase),
                    $"github:#{number}",
                    $"{projectTitle} · linked issue #{number}",
                    0.97,
                    new ProviderReference(
                        SourceProviderKind.GitHub,
                        ProviderReferenceKind.Issue,
                        repositoryName,
                        $"#{number}",
                        ReadString(content, "url") ?? projectUrl));
            });

        return linkedIssues.ToArray();
    }

    private static bool TryReadProjectNodes(
        JsonElement root,
        out JsonElement projectNodes)
    {
        projectNodes = default;
        return root.TryGetProperty("data", out var data) &&
               data.TryGetProperty("repository", out var repository) &&
               repository.ValueKind == JsonValueKind.Object &&
               repository.TryGetProperty("projectsV2", out var projects) &&
               projects.TryGetProperty("nodes", out projectNodes) &&
               projectNodes.ValueKind == JsonValueKind.Array;
    }

    private static SourceDiscoveryCandidate ParseRelease(
        JsonElement release,
        string repositoryName)
    {
        var tag = ReadString(release, "tagName", "tag_name") ?? "(untagged)";
        var name = ReadString(release, "name");
        var isDraft = ReadBoolean(release, "isDraft", "draft");
        var isPrerelease = ReadBoolean(release, "isPrerelease", "prerelease");
        var publishedAt = ReadString(release, "publishedAt", "published_at");
        var url = ReadString(release, "html_url", "url")
            ?? $"https://github.com/{repositoryName}/releases/tag/{Uri.EscapeDataString(tag)}";
        var title = string.IsNullOrWhiteSpace(name) ? tag : name;
        var states = new[]
            {
                isDraft ? "draft" : null,
                isPrerelease ? "prerelease" : "release",
                publishedAt is null ? null : $"published {publishedAt}",
            }
            .Where(static value => value is not null);

        return new SourceDiscoveryCandidate(
            SourceArtifactKind.Release,
            title,
            null,
            "feature",
            "changed",
            !isDraft && publishedAt is not null,
            $"github:release:{tag}",
            string.Join(" · ", states),
            0.98,
            new ProviderReference(
                SourceProviderKind.GitHub,
                ProviderReferenceKind.Release,
                repositoryName,
                tag,
                url));
    }

    private static SourceDiscoveryCandidate ParseIssue(
        JsonElement issue,
        string repositoryName)
    {
        var number = issue.GetProperty("number").GetInt32();
        var title = ReadString(issue, "title") ?? $"Issue #{number}";
        var body = ReadString(issue, "body");
        var state = ReadString(issue, "state");
        var url = ReadString(issue, "html_url", "url");
        var labels = ReadLabels(issue);
        var milestone = issue.TryGetProperty("milestone", out var milestoneElement) &&
                        milestoneElement.ValueKind == JsonValueKind.Object
            ? ReadString(milestoneElement, "title")
            : null;
        var projects = ReadProjectNames(issue);
        var context = new[]
            {
                milestone is null ? null : $"Milestone: {milestone}",
                projects.Count == 0 ? null : $"Projects: {string.Join(", ", projects)}",
                labels.Count == 0 ? null : $"Labels: {string.Join(", ", labels)}",
            }
            .Where(static value => value is not null)
            .DefaultIfEmpty($"GitHub issue #{number}");

        return new SourceDiscoveryCandidate(
            projects.Count > 0
                ? SourceArtifactKind.GitHubProject
                : SourceArtifactKind.GitHubIssue,
            title,
            Truncate(body, 2_000),
            SuggestItemType(labels, title),
            SuggestCategory(
                labels,
                string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase)),
            string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase),
            $"github:#{number}",
            string.Join(" · ", context),
            projects.Count > 0 ? 0.97 : 0.93,
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
                ReadString(project, "title") ??
                (project.TryGetProperty("project", out var nested)
                    ? ReadString(nested, "title")
                    : null))
            .Where(static title => !string.IsNullOrWhiteSpace(title))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadLabels(JsonElement element)
    {
        if (!element.TryGetProperty("labels", out var labels))
        {
            return [];
        }

        if (labels.ValueKind == JsonValueKind.Object &&
            labels.TryGetProperty("nodes", out var nodes))
        {
            labels = nodes;
        }

        return labels.ValueKind == JsonValueKind.Array
            ? labels
                .EnumerateArray()
                .Select(static label =>
                    label.TryGetProperty("name", out var name)
                        ? name.GetString()
                        : null)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToArray()
            : [];
    }

    private static string SuggestItemType(IReadOnlyList<string> labels, string title)
    {
        var text = $"{string.Join(' ', labels)} {title}";
        if (text.Contains("security", StringComparison.OrdinalIgnoreCase))
        {
            return "security";
        }

        if (text.Contains("bug", StringComparison.OrdinalIgnoreCase)
            || text.Contains("fix", StringComparison.OrdinalIgnoreCase))
        {
            return "bug";
        }

        return "feature";
    }

    private static string SuggestCategory(IReadOnlyList<string> labels, bool completed)
    {
        var text = string.Join(' ', labels);
        if (text.Contains("security", StringComparison.OrdinalIgnoreCase))
        {
            return "security";
        }

        if (text.Contains("bug", StringComparison.OrdinalIgnoreCase)
            || text.Contains("fix", StringComparison.OrdinalIgnoreCase))
        {
            return "fixed";
        }

        return completed ? "changed" : "added";
    }

    private static string? ReadString(
        JsonElement element,
        string propertyName,
        string? alternativePropertyName = null)
    {
        if (!element.TryGetProperty(propertyName, out var value) &&
            (alternativePropertyName is null ||
             !element.TryGetProperty(alternativePropertyName, out value)))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }

    private static bool ReadBoolean(
        JsonElement element,
        string propertyName,
        string? alternativePropertyName = null)
    {
        if (!element.TryGetProperty(propertyName, out var value) &&
            (alternativePropertyName is null ||
             !element.TryGetProperty(alternativePropertyName, out value)))
        {
            return false;
        }

        return value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               value.GetBoolean();
    }

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maximumLength
                ? value
                : $"{value[..maximumLength]}…";
}
