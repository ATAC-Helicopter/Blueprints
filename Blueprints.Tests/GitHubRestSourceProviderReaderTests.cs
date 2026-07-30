using System.Net;
using System.Net.Http.Headers;
using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class GitHubRestSourceProviderReaderTests
{
    [Fact]
    public void Read_UsesBoundedAnonymousRestDiscoveryWithoutProjectAccess()
    {
        var handler = new GitHubHandler();
        var reader = CreateReader(handler, null);

        var result = reader.Read("/repository", "example/project");

        Assert.Equal(1, result.IssueCount);
        Assert.Equal(1, result.PullRequestCount);
        Assert.Equal(1, result.ReleaseCount);
        Assert.Equal(0, result.ProjectCount);
        Assert.Contains(
            result.Warnings,
            static warning => warning.Contains(
                "BLUEPRINTS_GITHUB_TOKEN",
                StringComparison.Ordinal));
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, static request =>
            Assert.Null(request.Authorization));
        Assert.All(handler.Requests, static request =>
            Assert.Equal("2022-11-28", request.ApiVersion));
        Assert.DoesNotContain(
            result.Candidates,
            static candidate => candidate.Title == "Pull duplicated by issues API");
    }

    [Fact]
    public void Read_UsesBearerCredentialForRestAndGraphQlWithoutPersistingIt()
    {
        var handler = new GitHubHandler();
        var reader = CreateReader(handler, "test-secret");

        var result = reader.Read("/repository", "example/project");

        Assert.Equal(2, result.ProjectCount);
        Assert.Empty(result.Warnings);
        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests, static request =>
        {
            Assert.Equal("Bearer", request.Authorization?.Scheme);
            Assert.Equal("test-secret", request.Authorization?.Parameter);
        });
        Assert.Contains(
            result.Candidates,
            static candidate =>
                candidate.Kind == SourceArtifactKind.GitHubProject &&
                candidate.Title == "Standalone draft");
        Assert.Single(
            result.Candidates,
            static candidate => candidate.Title == "Fix direct discovery");
        Assert.Contains(
            result.Candidates,
            static candidate =>
                candidate.Kind == SourceArtifactKind.GitHubProject &&
                candidate.Title == "Fix direct discovery");
    }

    [Fact]
    public void Read_ReportsHttpFailuresWithoutExposingTheCredential()
    {
        var handler = new GitHubHandler(HttpStatusCode.Unauthorized);
        var reader = CreateReader(handler, "test-secret");

        var result = reader.Read("/repository", "example/project");

        Assert.Empty(result.Candidates);
        Assert.Equal(4, result.Warnings.Count);
        Assert.All(
            result.Warnings,
            static warning =>
            {
                Assert.Contains("401", warning, StringComparison.Ordinal);
                Assert.DoesNotContain(
                    "test-secret",
                    warning,
                    StringComparison.Ordinal);
            });
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("../repo")]
    [InlineData("owner/repo/extra")]
    [InlineData("owner/repo?query")]
    public void Read_RejectsMalformedRepositoryNames(string repositoryName)
    {
        var reader = CreateReader(new GitHubHandler(), null);

        Assert.Throws<InvalidOperationException>(
            () => reader.Read("/repository", repositoryName));
    }

    private static GitHubRestSourceProviderReader CreateReader(
        GitHubHandler handler,
        string? token) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.github.test/", UriKind.Absolute),
            },
            new TestCredentialSource(token));

    private sealed class TestCredentialSource(string? token) : IProviderCredentialSource
    {
        public string? GetGitHubToken() => token;
    }

    private sealed class GitHubHandler : HttpMessageHandler
    {
        private readonly Lock _lock = new();
        private readonly HttpStatusCode _statusCode;

        public List<CapturedRequest> Requests { get; } = [];

        public GitHubHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _statusCode = statusCode;
        }

        protected override HttpResponseMessage Send(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Handle(request);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Handle(request));

        private HttpResponseMessage Handle(HttpRequestMessage request)
        {
            lock (_lock)
            {
                Requests.Add(
                    new CapturedRequest(
                        request.RequestUri!.PathAndQuery,
                        request.Headers.Authorization,
                        request.Headers.TryGetValues(
                            "X-GitHub-Api-Version",
                            out var versions)
                            ? versions.Single()
                            : null));
            }
            if (_statusCode != HttpStatusCode.OK)
            {
                return new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent("provider error"),
                };
            }

            var json = request.RequestUri.AbsolutePath switch
            {
                "/repos/example/project/issues" =>
                    """
                    [
                      {
                        "number": 7,
                        "title": "Fix direct discovery",
                        "body": "Use the REST API.",
                        "state": "closed",
                        "html_url": "https://github.test/example/project/issues/7",
                        "labels": [{ "name": "bug" }],
                        "milestone": { "title": "0.4.0" }
                      },
                      {
                        "number": 8,
                        "title": "Pull duplicated by issues API",
                        "state": "open",
                        "pull_request": { "url": "https://api.github.test/pulls/8" }
                      }
                    ]
                    """,
                "/repos/example/project/pulls" =>
                    """
                    [{
                      "number": 8,
                      "title": "Direct provider adapter",
                      "body": "Remove the CLI dependency.",
                      "state": "closed",
                      "merged_at": "2026-07-30T00:00:00Z",
                      "html_url": "https://github.test/example/project/pull/8",
                      "labels": [{ "name": "feature" }]
                    }]
                    """,
                "/repos/example/project/releases" =>
                    """
                    [{
                      "tag_name": "v0.4.0",
                      "name": "Source awareness",
                      "draft": false,
                      "prerelease": true,
                      "published_at": "2026-07-30T00:00:00Z",
                      "html_url": "https://github.test/example/project/releases/tag/v0.4.0"
                    }]
                    """,
                "/graphql" =>
                    """
                    {
                      "data": {
                        "repository": {
                          "projectsV2": {
                            "nodes": [{
                              "number": 2,
                              "title": "Roadmap",
                              "url": "https://github.test/orgs/example/projects/2",
                              "items": {
                                "nodes": [{
                                  "id": "PVTI_1",
                                  "type": "REDACTED",
                                  "content": {
                                    "__typename": "DraftIssue",
                                    "title": "Standalone draft",
                                    "body": "Draft detail"
                                  }
                                }, {
                                  "id": "PVTI_2",
                                  "type": "ISSUE",
                                  "content": {
                                    "__typename": "Issue",
                                    "number": 7,
                                    "title": "Fix direct discovery",
                                    "body": "Use the REST API.",
                                    "state": "CLOSED",
                                    "url": "https://github.test/example/project/issues/7",
                                    "labels": { "nodes": [{ "name": "bug" }] }
                                  }
                                }]
                              }
                            }]
                          }
                        }
                      }
                    }
                    """,
                _ => "[]",
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json),
            };
        }
    }

    private sealed record CapturedRequest(
        string Path,
        AuthenticationHeaderValue? Authorization,
        string? ApiVersion);
}
