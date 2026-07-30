using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public static class GitHubSourceJsonParser
{
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
        var mergedAt = ReadString(pullRequest, "mergedAt");
        var url = ReadString(pullRequest, "url");
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

    private static SourceDiscoveryCandidate ParseRelease(
        JsonElement release,
        string repositoryName)
    {
        var tag = ReadString(release, "tagName") ?? "(untagged)";
        var name = ReadString(release, "name");
        var isDraft = ReadBoolean(release, "isDraft");
        var isPrerelease = ReadBoolean(release, "isPrerelease");
        var publishedAt = ReadString(release, "publishedAt");
        var url = ReadString(release, "url")
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

    private static IReadOnlyList<string> ReadLabels(JsonElement element) =>
        element.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array
            ? labels
                .EnumerateArray()
                .Select(static label => label.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToArray()
            : [];

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

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

    private static bool ReadBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
        && value.GetBoolean();

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maximumLength
                ? value
                : $"{value[..maximumLength]}…";
}
