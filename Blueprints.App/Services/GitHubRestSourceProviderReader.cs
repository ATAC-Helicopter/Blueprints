using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class GitHubRestSourceProviderReader : IHostedSourceProviderReader
{
    private const string ApiVersion = "2022-11-28";
    private const string ProjectDraftQuery =
        "query($owner:String!,$name:String!){repository(owner:$owner,name:$name){" +
        "projectsV2(first:10){nodes{number title url items(first:100){nodes{id type content{" +
        "__typename ... on DraftIssue{title body} ... on Issue{" +
        "number title body state url labels(first:20){nodes{name}}} ... on PullRequest{number}" +
        "}}}}}}}";
    private readonly HttpClient _httpClient;
    private readonly IProviderCredentialSource _credentialSource;

    public GitHubRestSourceProviderReader()
        : this(
            new HttpClient
            {
                BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(15),
            },
            new EnvironmentProviderCredentialSource())
    {
    }

    public GitHubRestSourceProviderReader(
        HttpClient httpClient,
        IProviderCredentialSource credentialSource)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(credentialSource);
        _httpClient = httpClient;
        _credentialSource = credentialSource;
    }

    public HostedSourceDiscoveryResult Read(
        string repositoryRoot,
        HostedRepositoryDescriptor repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(repository);
        if (repository.Provider != SourceProviderKind.GitHub)
        {
            throw new InvalidOperationException(
                "The GitHub reader only accepts GitHub repositories.");
        }
        var repositoryName = repository.RepositoryName;
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ValidateRepositoryName(repositoryName);

        var token = _credentialSource.GetGitHubToken();
        var issuesTask = ReadRestAsync(
            $"repos/{repositoryName}/issues?state=all&per_page=100",
            repositoryName,
            "issues",
            GitHubSourceJsonParser.ParseIssues,
            token);
        var pullRequestsTask = ReadRestAsync(
            $"repos/{repositoryName}/pulls?state=all&per_page=100",
            repositoryName,
            "pull requests",
            GitHubSourceJsonParser.ParsePullRequests,
            token);
        var releasesTask = ReadRestAsync(
            $"repos/{repositoryName}/releases?per_page=50",
            repositoryName,
            "releases",
            GitHubSourceJsonParser.ParseReleases,
            token);
        var projectItemsTask = string.IsNullOrWhiteSpace(token)
            ? Task.FromResult(new ProviderReadResult(
                [],
                ["GitHub Projects require BLUEPRINTS_GITHUB_TOKEN and were skipped."]))
            : ReadProjectItemsAsync(repositoryName, token);
        Task.WhenAll(
                issuesTask,
                pullRequestsTask,
                releasesTask,
                projectItemsTask)
            .GetAwaiter()
            .GetResult();
        var issues = issuesTask.Result;
        var pullRequests = pullRequestsTask.Result;
        var releases = releasesTask.Result;
        var projectItems = projectItemsTask.Result;
        var projectIssueIds = projectItems.Candidates
            .Where(static candidate =>
                candidate.ProviderReference?.Kind == ProviderReferenceKind.Issue)
            .Select(static candidate => candidate.ProviderReference!.Identifier)
            .ToHashSet(StringComparer.Ordinal);
        var unlinkedIssues = issues.Candidates
            .Where(candidate =>
                candidate.ProviderReference is null ||
                !projectIssueIds.Contains(candidate.ProviderReference.Identifier))
            .ToArray();

        return new HostedSourceDiscoveryResult(
            [
                .. unlinkedIssues,
                .. pullRequests.Candidates,
                .. releases.Candidates,
                .. projectItems.Candidates,
            ],
            [
                .. issues.Warnings,
                .. pullRequests.Warnings,
                .. releases.Warnings,
                .. projectItems.Warnings,
            ],
            issues.Candidates.Count,
            projectItems.Candidates.Count,
            pullRequests.Candidates.Count,
            releases.Candidates.Count);
    }

    private async Task<ProviderReadResult> ReadRestAsync(
        string relativeUri,
        string repositoryName,
        string sourceName,
        Func<string, string, IReadOnlyList<SourceDiscoveryCandidate>> parser,
        string? token)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, relativeUri, token);
            using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var body = await ReadBoundedBodyAsync(response).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failure(sourceName, response.StatusCode);
            }

            return new ProviderReadResult(parser(body, repositoryName), []);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
            TaskCanceledException or
            JsonException or
            InvalidOperationException)
        {
            return new ProviderReadResult(
                [],
                [$"GitHub {sourceName} were skipped: {SafeMessage(exception.Message)}"]);
        }
    }

    private async Task<ProviderReadResult> ReadProjectItemsAsync(
        string repositoryName,
        string token)
    {
        var separator = repositoryName.IndexOf('/');
        var payload = JsonSerializer.Serialize(
            new
            {
                query = ProjectDraftQuery,
                variables = new
                {
                    owner = repositoryName[..separator],
                    name = repositoryName[(separator + 1)..],
                },
            });
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "graphql", token);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var body = await ReadBoundedBodyAsync(response).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failure("Projects", response.StatusCode);
            }

            using (var document = JsonDocument.Parse(body))
            {
                if (document.RootElement.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Array &&
                    errors.GetArrayLength() > 0)
                {
                    return new ProviderReadResult(
                        [],
                        ["GitHub Projects were skipped: GraphQL returned an error."]);
                }
            }

            return new ProviderReadResult(
                GitHubSourceJsonParser.ParseProjectItems(body, repositoryName),
                []);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
            TaskCanceledException or
            JsonException or
            InvalidOperationException)
        {
            return new ProviderReadResult(
                [],
                [$"GitHub Projects were skipped: {SafeMessage(exception.Message)}"]);
        }
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativeUri,
        string? token)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.UserAgent.ParseAdd("Blueprints/0.4");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static async Task<string> ReadBoundedBodyAsync(
        HttpResponseMessage response)
    {
        const int maximumResponseBytes = 4 * 1024 * 1024;
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > maximumResponseBytes)
        {
            throw new InvalidOperationException(
                "GitHub response exceeded the 4 MiB safety limit.");
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync()
            .ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await stream
                .ReadAsync(chunk.AsMemory())
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > maximumResponseBytes)
            {
                throw new InvalidOperationException(
                    "GitHub response exceeded the 4 MiB safety limit.");
            }
            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static ProviderReadResult Failure(
        string sourceName,
        HttpStatusCode statusCode) =>
        new(
            [],
            [
                $"GitHub {sourceName} were skipped: API returned " +
                $"{(int)statusCode} {statusCode}.",
            ]);

    private static void ValidateRepositoryName(string repositoryName)
    {
        var parts = repositoryName.Split('/');
        if (parts.Length != 2 ||
            parts.Any(static part =>
                string.IsNullOrWhiteSpace(part) ||
                part is "." or ".." ||
                part.Any(static character =>
                    !(char.IsAsciiLetterOrDigit(character) ||
                      character is '-' or '_' or '.'))))
        {
            throw new InvalidOperationException(
                "GitHub repository identity must use owner/name syntax.");
        }
    }

    private static string SafeMessage(string message)
    {
        const int maximumLength = 300;
        var normalized = message.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength] + "…";
    }

    private sealed record ProviderReadResult(
        IReadOnlyList<SourceDiscoveryCandidate> Candidates,
        IReadOnlyList<string> Warnings);
}
