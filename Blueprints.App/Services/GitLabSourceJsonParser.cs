using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public static class GitLabSourceJsonParser
{
    public static IReadOnlyList<SourceDiscoveryCandidate> ParseIssues(
        string json,
        string repositoryName)
    {
        using var document = Parse(json, repositoryName);
        return document.RootElement
            .EnumerateArray()
            .Take(100)
            .Select(issue =>
            {
                var iid = ReadInt(issue, "iid");
                var title = ReadString(issue, "title") ?? $"Issue #{iid}";
                var labels = ReadLabels(issue);
                var closed = string.Equals(
                    ReadString(issue, "state"),
                    "closed",
                    StringComparison.OrdinalIgnoreCase);
                var milestone = ReadNestedString(issue, "milestone", "title");
                return new SourceDiscoveryCandidate(
                    SourceArtifactKind.Issue,
                    title,
                    Truncate(ReadString(issue, "description"), 2_000),
                    SuggestItemType(labels, title),
                    SuggestCategory(labels, closed),
                    closed,
                    $"gitlab:#{iid}",
                    milestone is null
                        ? $"GitLab issue #{iid}"
                        : $"GitLab issue #{iid} · Milestone: {milestone}",
                    0.93,
                    new ProviderReference(
                        SourceProviderKind.GitLab,
                        ProviderReferenceKind.Issue,
                        repositoryName,
                        $"#{iid}",
                        ReadString(issue, "web_url")));
            })
            .ToArray();
    }

    public static IReadOnlyList<SourceDiscoveryCandidate> ParseMergeRequests(
        string json,
        string repositoryName)
    {
        using var document = Parse(json, repositoryName);
        return document.RootElement
            .EnumerateArray()
            .Take(100)
            .Select(mergeRequest =>
            {
                var iid = ReadInt(mergeRequest, "iid");
                var title = ReadString(mergeRequest, "title")
                    ?? $"Merge request !{iid}";
                var labels = ReadLabels(mergeRequest);
                var state = ReadString(mergeRequest, "state");
                var mergedAt = ReadString(mergeRequest, "merged_at");
                var completed =
                    string.Equals(state, "merged", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase);
                return new SourceDiscoveryCandidate(
                    SourceArtifactKind.ChangeRequest,
                    title,
                    Truncate(ReadString(mergeRequest, "description"), 2_000),
                    SuggestItemType(labels, title),
                    SuggestCategory(labels, completed),
                    completed,
                    $"gitlab:mr:!{iid}",
                    mergedAt is null
                        ? $"GitLab merge request !{iid} · {state ?? "unknown state"}"
                        : $"GitLab merge request !{iid} · merged {mergedAt}",
                    0.96,
                    new ProviderReference(
                        SourceProviderKind.GitLab,
                        ProviderReferenceKind.ChangeRequest,
                        repositoryName,
                        $"!{iid}",
                        ReadString(mergeRequest, "web_url")));
            })
            .ToArray();
    }

    public static IReadOnlyList<SourceDiscoveryCandidate> ParseReleases(
        string json,
        string repositoryName)
    {
        using var document = Parse(json, repositoryName);
        return document.RootElement
            .EnumerateArray()
            .Take(50)
            .Select(release =>
            {
                var tag = ReadString(release, "tag_name") ?? "(untagged)";
                var name = ReadString(release, "name");
                var releasedAt = ReadString(release, "released_at");
                var upcoming = ReadBoolean(release, "upcoming_release");
                return new SourceDiscoveryCandidate(
                    SourceArtifactKind.Release,
                    string.IsNullOrWhiteSpace(name) ? tag : name,
                    Truncate(ReadString(release, "description"), 2_000),
                    "feature",
                    "changed",
                    !upcoming && releasedAt is not null,
                    $"gitlab:release:{tag}",
                    upcoming
                        ? $"GitLab upcoming release · {tag}"
                        : $"GitLab release · {tag} · {releasedAt ?? "unpublished"}",
                    0.98,
                    new ProviderReference(
                        SourceProviderKind.GitLab,
                        ProviderReferenceKind.Release,
                        repositoryName,
                        tag,
                        ReadNestedString(release, "_links", "self")));
            })
            .ToArray();
    }

    public static IReadOnlyList<SourceDiscoveryCandidate> ParseMilestones(
        string json,
        string repositoryName)
    {
        using var document = Parse(json, repositoryName);
        return document.RootElement
            .EnumerateArray()
            .Take(100)
            .Select(milestone =>
            {
                var iid = ReadInt(milestone, "iid");
                var title = ReadString(milestone, "title")
                    ?? $"Milestone {iid}";
                var closed = string.Equals(
                    ReadString(milestone, "state"),
                    "closed",
                    StringComparison.OrdinalIgnoreCase);
                return new SourceDiscoveryCandidate(
                    SourceArtifactKind.Project,
                    title,
                    Truncate(ReadString(milestone, "description"), 2_000),
                    "feature",
                    closed ? "changed" : "added",
                    closed,
                    $"gitlab:milestone:{iid}",
                    $"GitLab milestone {iid} · {(closed ? "closed" : "active")}",
                    0.9,
                    new ProviderReference(
                        SourceProviderKind.GitLab,
                        ProviderReferenceKind.Project,
                        repositoryName,
                        iid.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        $"https://gitlab.com/{repositoryName}/-/milestones/{iid}"));
            })
            .ToArray();
    }

    private static JsonDocument Parse(string json, string repositoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        return JsonDocument.Parse(json);
    }

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.TryGetInt32(out var result)
            ? result
            : 0;

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

    private static string? ReadNestedString(
        JsonElement element,
        string objectProperty,
        string valueProperty) =>
        element.TryGetProperty(objectProperty, out var nested) &&
        nested.ValueKind == JsonValueKind.Object
            ? ReadString(nested, valueProperty)
            : null;

    private static bool ReadBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private static IReadOnlyList<string> ReadLabels(JsonElement element) =>
        element.TryGetProperty("labels", out var labels) &&
        labels.ValueKind == JsonValueKind.Array
            ? labels
                .EnumerateArray()
                .Select(static label => label.ValueKind == JsonValueKind.String
                    ? label.GetString()
                    : null)
                .Where(static label => !string.IsNullOrWhiteSpace(label))
                .Cast<string>()
                .ToArray()
            : [];

    private static string SuggestItemType(
        IReadOnlyList<string> labels,
        string title)
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
        return "feature";
    }

    private static string SuggestCategory(
        IReadOnlyList<string> labels,
        bool completed)
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
        return completed ? "changed" : "added";
    }

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maximumLength
                ? value
                : value[..maximumLength] + "…";
}
