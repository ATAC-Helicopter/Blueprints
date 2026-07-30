using System.Diagnostics;
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

    [Fact]
    public void Discover_UsesTheProviderNeutralHostedReaderBoundary()
    {
        Directory.CreateDirectory(_root);
        RunGit("init", _root);
        RunGit("-C", _root, "remote", "add", "origin", "https://github.com/example/project.git");
        var hostedReader = new TestHostedSourceProviderReader();
        var service = new RepositorySourceDiscoveryService(
            new MarkdownSourceDiscoveryParser(),
            hostedReader);

        var result = service.Discover(_root);

        Assert.Equal("example/project", hostedReader.RepositoryName);
        Assert.Equal(_root, hostedReader.RepositoryRoot);
        Assert.Equal(1, result.PullRequestCount);
        Assert.Contains(
            result.Candidates,
            static candidate => candidate.Kind == SourceArtifactKind.PullRequest);
    }

    private string Write(string name, string content)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static void RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Git for the test.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestHostedSourceProviderReader : IHostedSourceProviderReader
    {
        public string RepositoryRoot { get; private set; } = string.Empty;

        public string RepositoryName { get; private set; } = string.Empty;

        public HostedSourceDiscoveryResult Read(
            string repositoryRoot,
            string repositoryName)
        {
            RepositoryRoot = repositoryRoot;
            RepositoryName = repositoryName;
            return new HostedSourceDiscoveryResult(
                [
                    new SourceDiscoveryCandidate(
                        SourceArtifactKind.PullRequest,
                        "Provider-neutral reader",
                        null,
                        "feature",
                        "added",
                        false,
                        "test:pr:1",
                        "Test pull request",
                        1,
                        new ProviderReference(
                            SourceProviderKind.GitHub,
                            ProviderReferenceKind.PullRequest,
                            repositoryName,
                            "#1",
                            null)),
                ],
                [],
                0,
                0,
                1,
                0);
        }
    }
}
