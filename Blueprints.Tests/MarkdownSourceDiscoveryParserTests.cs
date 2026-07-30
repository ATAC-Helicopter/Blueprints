using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class MarkdownSourceDiscoveryParserTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "SourceDiscovery",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ParseRoadmap_MapsCheckboxStateAndContext()
    {
        var path = Write(
            "Roadmap.md",
            """
            # Roadmap
            ## Canvas interaction
            - [ ] Add undo and redo
            - [x] Fix selection crash
            """);

        var candidates = new MarkdownSourceDiscoveryParser().Parse(path, SourceArtifactKind.Roadmap);

        Assert.Collection(
            candidates,
            first =>
            {
                Assert.Equal("Add undo and redo", first.Title);
                Assert.False(first.IsDone);
                Assert.Equal("feature", first.SuggestedItemTypeId);
                Assert.Equal("Canvas interaction", first.SourceContext);
                var reference = Assert.IsType<ProviderReference>(first.ProviderReference);
                Assert.Equal(SourceProviderKind.Local, reference.Provider);
                Assert.Equal(ProviderReferenceKind.PlanningDocument, reference.Kind);
                Assert.Equal("Roadmap.md:3", reference.Identifier);
            },
            second =>
            {
                Assert.True(second.IsDone);
                Assert.Equal("bug", second.SuggestedItemTypeId);
                Assert.Equal("fixed", second.SuggestedCategoryId);
            });
    }

    [Fact]
    public void ParseChangelog_MapsSecurityAndCompletedState()
    {
        var path = Write(
            "CHANGELOG.md",
            """
            # Changelog
            ## Security
            - Harden signature validation
            """);

        var candidate = Assert.Single(
            new MarkdownSourceDiscoveryParser().Parse(path, SourceArtifactKind.Changelog));

        Assert.True(candidate.IsDone);
        Assert.Equal("security", candidate.SuggestedItemTypeId);
        Assert.Equal("security", candidate.SuggestedCategoryId);
        Assert.StartsWith("changelog:", candidate.SourceReference, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_NormalizesMarkdownLinksAndCode()
    {
        var path = Write(
            "Roadmap.md",
            """
            ## Sources
            - [ ] Add [`GitHub`](https://github.com) ingestion.
            """);

        var candidate = Assert.Single(
            new MarkdownSourceDiscoveryParser().Parse(path, SourceArtifactKind.Roadmap));

        Assert.Equal("Add GitHub ingestion", candidate.Title);
    }

    [Fact]
    public void Discover_ReturnsLocalSourcesWhenGitHubIsUnavailable()
    {
        Write(
            "CHANGELOG.md",
            """
            ## Added
            - Signed import review
            """);
        Write(
            "Roadmap.md",
            """
            ## Next
            - [ ] Add provider-neutral references
            """);

        var result = new RepositorySourceDiscoveryService().Discover(_root);

        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(1, result.ChangelogCount);
        Assert.Equal(1, result.RoadmapCount);
        Assert.Equal(0, result.GitHubIssueCount);
        Assert.Contains(
            result.Warnings,
            static warning => warning.Contains("GitHub", StringComparison.OrdinalIgnoreCase));
    }

    private string Write(string name, string content)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
