using System.Text.RegularExpressions;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed partial class MarkdownSourceDiscoveryParser
{
    private const int MaximumCandidatesPerFile = 100;

    public IReadOnlyList<SourceDiscoveryCandidate> Parse(string filePath, SourceArtifactKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (kind is not SourceArtifactKind.Changelog and not SourceArtifactKind.Roadmap)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Only changelog and roadmap Markdown can be parsed.");
        }

        var candidates = new List<SourceDiscoveryCandidate>();
        var heading = string.Empty;
        var lines = File.ReadLines(filePath).Take(5_000);
        var lineNumber = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            var headingMatch = HeadingPattern().Match(line);
            if (headingMatch.Success)
            {
                heading = headingMatch.Groups["heading"].Value.Trim();
                continue;
            }

            var itemMatch = MarkdownItemPattern().Match(line);
            if (!itemMatch.Success)
            {
                continue;
            }

            var title = NormalizeMarkdown(itemMatch.Groups["title"].Value);
            if (title.Length < 3 || IsDecorativeItem(title))
            {
                continue;
            }

            var checkbox = itemMatch.Groups["checkbox"].Value;
            var isDone = checkbox.Equals("x", StringComparison.OrdinalIgnoreCase);
            var category = SuggestCategory(kind, heading, title);
            var itemType = SuggestItemType(heading, title);
            var relativeName = Path.GetFileName(filePath);
            var context = string.IsNullOrWhiteSpace(heading) ? relativeName : heading;

            candidates.Add(
                new SourceDiscoveryCandidate(
                    kind,
                    title,
                    string.IsNullOrWhiteSpace(heading) ? null : $"Imported from section “{heading}”.",
                    itemType,
                    category,
                    isDone || kind == SourceArtifactKind.Changelog,
                    $"{kind.ToString().ToLowerInvariant()}:{relativeName}:{lineNumber}",
                    context,
                    kind == SourceArtifactKind.Roadmap && checkbox.Length > 0 ? 0.94 : 0.82));

            if (candidates.Count >= MaximumCandidatesPerFile)
            {
                break;
            }
        }

        return candidates;
    }

    private static string SuggestCategory(SourceArtifactKind kind, string heading, string title)
    {
        var text = $"{heading} {title}";
        if (ContainsAny(text, "security", "vulnerability", "cve"))
        {
            return "security";
        }

        if (ContainsAny(text, "fix", "fixed", "bug", "repair"))
        {
            return "fixed";
        }

        if (ContainsAny(text, "remove", "removed", "deprecated"))
        {
            return "removed";
        }

        if (kind == SourceArtifactKind.Changelog &&
            ContainsAny(text, "add", "added", "new", "feature"))
        {
            return "added";
        }

        return kind == SourceArtifactKind.Roadmap ? "added" : "changed";
    }

    private static string SuggestItemType(string heading, string title)
    {
        var text = $"{heading} {title}";
        if (ContainsAny(text, "security", "vulnerability", "cve"))
        {
            return "security";
        }

        if (ContainsAny(text, "fix", "bug", "defect", "crash"))
        {
            return "bug";
        }

        if (ContainsAny(text, "issue", "investigate", "research"))
        {
            return "issue";
        }

        return "feature";
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool IsDecorativeItem(string title) =>
        title.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
        title.Equals("TBD", StringComparison.OrdinalIgnoreCase) ||
        title.Equals("None", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeMarkdown(string value)
    {
        var withoutLinks = MarkdownLinkPattern().Replace(value, static match => match.Groups["label"].Value);
        return withoutLinks
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Trim()
            .TrimEnd('.');
    }

    [GeneratedRegex(@"^#{1,6}\s+(?<heading>.+)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^[-*+]\s+(?:\[(?<checkbox>[ xX])\]\s+)?(?<title>.+)$")]
    private static partial Regex MarkdownItemPattern();

    [GeneratedRegex(@"\[(?<label>[^\]]+)\]\([^)]+\)")]
    private static partial Regex MarkdownLinkPattern();
}
