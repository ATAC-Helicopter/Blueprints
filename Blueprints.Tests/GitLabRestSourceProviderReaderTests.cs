using System.Net;
using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class GitLabRestSourceProviderReaderTests
{
    [Fact]
    public void Read_MapsBoundedGitLabSourcesAndUsesEncodedProjectPath()
    {
        var handler = new GitLabHandler();
        var reader = CreateReader(handler, "gitlab-secret");

        var result = reader.Read(
            "/repository",
            new HostedRepositoryDescriptor(
                SourceProviderKind.GitLab,
                "example/platform/project"));

        Assert.Equal(1, result.IssueCount);
        Assert.Equal(1, result.ChangeRequestCount);
        Assert.Equal(1, result.ReleaseCount);
        Assert.Equal(1, result.ProjectCount);
        Assert.Empty(result.Warnings);
        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests, static request =>
            Assert.Equal("gitlab-secret", request.Token));
        Assert.All(handler.Requests, static request =>
            Assert.Contains(
                "example%2Fplatform%2Fproject",
                request.Path,
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            result.Candidates,
            static candidate =>
                candidate.Kind == SourceArtifactKind.ChangeRequest &&
                candidate.ProviderReference?.Provider == SourceProviderKind.GitLab);
    }

    [Fact]
    public void Read_AllowsAnonymousPublicDiscovery()
    {
        var handler = new GitLabHandler();
        var reader = CreateReader(handler, null);

        var result = reader.Read(
            "/repository",
            new HostedRepositoryDescriptor(
                SourceProviderKind.GitLab,
                "example/project"));

        Assert.Empty(result.Warnings);
        Assert.All(handler.Requests, static request =>
            Assert.Null(request.Token));
    }

    [Fact]
    public void Read_ReportsFailuresWithoutExposingCredential()
    {
        var reader = CreateReader(
            new GitLabHandler(HttpStatusCode.Unauthorized),
            "gitlab-secret");

        var result = reader.Read(
            "/repository",
            new HostedRepositoryDescriptor(
                SourceProviderKind.GitLab,
                "example/project"));

        Assert.Empty(result.Candidates);
        Assert.Equal(4, result.Warnings.Count);
        Assert.All(result.Warnings, static warning =>
        {
            Assert.Contains("401", warning, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "gitlab-secret",
                warning,
                StringComparison.Ordinal);
        });
    }

    private static GitLabRestSourceProviderReader CreateReader(
        GitLabHandler handler,
        string? token) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri(
                    "https://gitlab.example/api/v4/",
                    UriKind.Absolute),
            },
            new TestCredentialSource(token));

    private sealed class TestCredentialSource(string? token)
        : IProviderCredentialSource
    {
        public string? GetGitHubToken() => null;

        public string? GetGitLabToken() => token;
    }

    private sealed class GitLabHandler : HttpMessageHandler
    {
        private readonly Lock _lock = new();
        private readonly HttpStatusCode _statusCode;

        public GitLabHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _statusCode = statusCode;
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                Requests.Add(
                    new CapturedRequest(
                        request.RequestUri!.PathAndQuery,
                        request.Headers.TryGetValues(
                            "PRIVATE-TOKEN",
                            out var values)
                            ? values.Single()
                            : null));
            }
            if (_statusCode != HttpStatusCode.OK)
            {
                return Task.FromResult(
                    new HttpResponseMessage(_statusCode)
                    {
                        Content = new StringContent("provider error"),
                    });
            }

            var json = request.RequestUri!.AbsolutePath switch
            {
                var path when path.EndsWith("/issues", StringComparison.Ordinal) =>
                    """
                    [{
                      "iid": 4,
                      "title": "Fix GitLab discovery",
                      "description": "Provider parity.",
                      "state": "closed",
                      "web_url": "https://gitlab.example/example/project/-/issues/4",
                      "labels": ["bug"],
                      "milestone": { "title": "0.4.0" }
                    }]
                    """,
                var path when path.EndsWith("/merge_requests", StringComparison.Ordinal) =>
                    """
                    [{
                      "iid": 5,
                      "title": "Add merge request discovery",
                      "description": "Provider parity.",
                      "state": "merged",
                      "merged_at": "2026-07-30T00:00:00Z",
                      "web_url": "https://gitlab.example/example/project/-/merge_requests/5",
                      "labels": ["feature"]
                    }]
                    """,
                var path when path.EndsWith("/releases", StringComparison.Ordinal) =>
                    """
                    [{
                      "tag_name": "v0.4.0",
                      "name": "Source awareness",
                      "description": "GitLab parity.",
                      "released_at": "2026-07-30T00:00:00Z",
                      "upcoming_release": false,
                      "_links": {
                        "self": "https://gitlab.example/example/project/-/releases/v0.4.0"
                      }
                    }]
                    """,
                var path when path.EndsWith("/milestones", StringComparison.Ordinal) =>
                    """
                    [{
                      "iid": 6,
                      "title": "0.4.0",
                      "description": "Source awareness.",
                      "state": "active"
                    }]
                    """,
                _ => "[]",
            };
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json),
                });
        }
    }

    private sealed record CapturedRequest(string Path, string? Token);
}
