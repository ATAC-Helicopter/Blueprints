using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class GitHubSourceJsonParserTests
{
    [Fact]
    public void ParseProjectDrafts_ReturnsOnlyStandaloneDraftItems()
    {
        var candidates = GitHubSourceJsonParser.ParseProjectDrafts(
            """
            {
              "data": {
                "repository": {
                  "projectsV2": {
                    "nodes": [
                      {
                        "number": 8,
                        "title": "Blueprints Roadmap",
                        "url": "https://github.com/users/example/projects/8",
                        "items": {
                          "nodes": [
                            {
                              "id": "draft-1",
                              "type": "DRAFT_ISSUE",
                              "content": {
                                "__typename": "DraftIssue",
                                "title": "Design offline provider cache",
                                "body": "Keep the source boundary explicit."
                              }
                            },
                            {
                              "id": "issue-1",
                              "type": "ISSUE",
                              "content": {
                                "__typename": "Issue",
                                "number": 42
                              }
                            }
                          ]
                        }
                      }
                    ]
                  }
                }
              }
            }
            """,
            "example/project");

        var candidate = Assert.Single(candidates);
        Assert.Equal(SourceArtifactKind.GitHubProject, candidate.Kind);
        Assert.Equal("Design offline provider cache", candidate.Title);
        Assert.False(candidate.IsDone);
        var reference = Assert.IsType<ProviderReference>(candidate.ProviderReference);
        Assert.Equal(ProviderReferenceKind.Project, reference.Kind);
        Assert.Equal("8/draft/draft-1", reference.Identifier);
    }

    [Fact]
    public void ParsePullRequests_ProducesProviderNeutralReferencesAndCompletion()
    {
        var candidates = GitHubSourceJsonParser.ParsePullRequests(
            """
            [
              {
                "number": 42,
                "title": "Fix release check",
                "body": "Make readiness explicit.",
                "state": "MERGED",
                "labels": [{ "name": "bug" }],
                "url": "https://github.com/example/project/pull/42",
                "mergedAt": "2026-07-30T10:00:00Z"
              }
            ]
            """,
            "example/project");

        var candidate = Assert.Single(candidates);
        Assert.Equal(SourceArtifactKind.PullRequest, candidate.Kind);
        Assert.True(candidate.IsDone);
        Assert.Equal("bug", candidate.SuggestedItemTypeId);
        Assert.Equal("fixed", candidate.SuggestedCategoryId);
        var reference = Assert.IsType<ProviderReference>(candidate.ProviderReference);
        Assert.Equal(SourceProviderKind.GitHub, reference.Provider);
        Assert.Equal(ProviderReferenceKind.PullRequest, reference.Kind);
        Assert.Equal("example/project", reference.Repository);
        Assert.Equal("#42", reference.Identifier);
    }

    [Fact]
    public void ParseReleases_DistinguishesDraftAndPublishedRecords()
    {
        var candidates = GitHubSourceJsonParser.ParseReleases(
            """
            [
              {
                "tagName": "v0.4.0-alpha.4",
                "name": "Alpha 4",
                "isDraft": true,
                "isPrerelease": true,
                "publishedAt": null,
                "url": "https://github.com/example/project/releases/tag/v0.4.0-alpha.4"
              },
              {
                "tagName": "v0.3.0",
                "name": "",
                "isDraft": false,
                "isPrerelease": true,
                "publishedAt": "2026-07-30T10:00:00Z"
              }
            ]
            """,
            "example/project");

        Assert.Collection(
            candidates,
            draft =>
            {
                Assert.Equal(SourceArtifactKind.Release, draft.Kind);
                Assert.False(draft.IsDone);
                Assert.Equal("Alpha 4", draft.Title);
            },
            published =>
            {
                Assert.True(published.IsDone);
                Assert.Equal("v0.3.0", published.Title);
                var reference = Assert.IsType<ProviderReference>(published.ProviderReference);
                Assert.Equal(ProviderReferenceKind.Release, reference.Kind);
                Assert.Equal("v0.3.0", reference.Identifier);
                Assert.Equal(
                    "https://github.com/example/project/releases/tag/v0.3.0",
                    reference.WebUrl);
            });
    }
}
