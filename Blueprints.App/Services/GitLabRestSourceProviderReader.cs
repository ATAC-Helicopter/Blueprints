using System.Net;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class GitLabRestSourceProviderReader : IHostedSourceProviderReader
{
    private readonly HttpClient _httpClient;
    private readonly IProviderCredentialSource _credentialSource;

    public int ContractVersion => HostedSourceProviderContract.CurrentVersion;

    public SourceProviderKind Provider => SourceProviderKind.GitLab;

    public SourceProviderCapabilities Capabilities =>
        SourceProviderCapabilities.Issues
        | SourceProviderCapabilities.ChangeRequests
        | SourceProviderCapabilities.Releases
        | SourceProviderCapabilities.PlanningBoards;

    public GitLabRestSourceProviderReader()
        : this(
            new HttpClient
            {
                BaseAddress = new Uri(
                    "https://gitlab.com/api/v4/",
                    UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(15),
            },
            new EnvironmentProviderCredentialSource())
    {
    }

    public GitLabRestSourceProviderReader(
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
        if (repository.Provider != SourceProviderKind.GitLab)
        {
            throw new InvalidOperationException(
                "The GitLab reader only accepts GitLab repositories.");
        }
        ValidateRepositoryName(repository.RepositoryName);

        var project = Uri.EscapeDataString(repository.RepositoryName);
        var token = _credentialSource.GetGitLabToken();
        var issuesTask = ReadPagesAsync(
            $"projects/{project}/issues?scope=all&per_page=100",
            100,
            repository.RepositoryName,
            "issues",
            GitLabSourceJsonParser.ParseIssues,
            token);
        var mergeRequestsTask = ReadPagesAsync(
            $"projects/{project}/merge_requests?scope=all&state=all&per_page=100",
            100,
            repository.RepositoryName,
            "merge requests",
            GitLabSourceJsonParser.ParseMergeRequests,
            token);
        var releasesTask = ReadPagesAsync(
            $"projects/{project}/releases?per_page=50",
            50,
            repository.RepositoryName,
            "releases",
            GitLabSourceJsonParser.ParseReleases,
            token);
        var milestonesTask = ReadPagesAsync(
            $"projects/{project}/milestones?per_page=100",
            100,
            repository.RepositoryName,
            "milestones",
            GitLabSourceJsonParser.ParseMilestones,
            token);
        Task.WhenAll(
                issuesTask,
                mergeRequestsTask,
                releasesTask,
                milestonesTask)
            .GetAwaiter()
            .GetResult();
        var issues = issuesTask.Result;
        var mergeRequests = mergeRequestsTask.Result;
        var releases = releasesTask.Result;
        var milestones = milestonesTask.Result;
        SourceDiscoveryCandidate[] candidates =
            [
                .. issues.Candidates,
                .. mergeRequests.Candidates,
                .. releases.Candidates,
                .. milestones.Candidates,
            ];
        var boundedCandidates = candidates
            .Take(HostedSourceProviderContract.MaximumCandidatesPerDiscovery)
            .ToArray();
        var limitWarnings = candidates.Length > boundedCandidates.Length
            ? new[] { $"GitLab discovery was limited to {HostedSourceProviderContract.MaximumCandidatesPerDiscovery} items." }
            : [];
        return new HostedSourceDiscoveryResult(
            boundedCandidates,
            [
                .. issues.Warnings,
                .. mergeRequests.Warnings,
                .. releases.Warnings,
                .. milestones.Warnings,
                .. limitWarnings,
            ],
            issues.Candidates.Count,
            milestones.Candidates.Count,
            mergeRequests.Candidates.Count,
            releases.Candidates.Count);
    }

    private async Task<ProviderReadResult> ReadPagesAsync(
        string relativeUri,
        int pageSize,
        string repositoryName,
        string sourceName,
        Func<string, string, IReadOnlyList<SourceDiscoveryCandidate>> parser,
        string? token)
    {
        var candidates = new List<SourceDiscoveryCandidate>();
        var warnings = new List<string>();
        var maximumPages = (int)Math.Ceiling(
            HostedSourceProviderContract.MaximumCandidatesPerDiscovery / (double)pageSize);
        for (var page = 1; page <= maximumPages; page++)
        {
            var separator = relativeUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            var result = await ReadPageAsync(
                    $"{relativeUri}{separator}page={page}",
                    repositoryName,
                    sourceName,
                    parser,
                    token)
                .ConfigureAwait(false);
            candidates.AddRange(result.Candidates);
            warnings.AddRange(result.Warnings);
            if (result.RawItemCount < pageSize || result.Warnings.Count > 0)
            {
                break;
            }
        }
        return new ProviderReadResult(candidates, warnings, candidates.Count);
    }

    private async Task<ProviderReadResult> ReadPageAsync(
        string relativeUri,
        string repositoryName,
        string sourceName,
        Func<string, string, IReadOnlyList<SourceDiscoveryCandidate>> parser,
        string? token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
            request.Headers.UserAgent.ParseAdd("Blueprints/0.4");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Add("PRIVATE-TOKEN", token);
            }
            using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var body = await ReadBoundedBodyAsync(response).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failure(sourceName, response.StatusCode);
            }
            using var document = System.Text.Json.JsonDocument.Parse(body);
            var rawItemCount = document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                ? document.RootElement.GetArrayLength()
                : 0;
            return new ProviderReadResult(parser(body, repositoryName), [], rawItemCount);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
            TaskCanceledException or
            System.Text.Json.JsonException or
            InvalidOperationException)
        {
            return new ProviderReadResult(
                [],
                [$"GitLab {sourceName} were skipped: {SafeMessage(exception.Message)}"]);
        }
    }

    private static async Task<string> ReadBoundedBodyAsync(
        HttpResponseMessage response)
    {
        const int maximumResponseBytes = 4 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > maximumResponseBytes)
        {
            throw new InvalidOperationException(
                "GitLab response exceeded the 4 MiB safety limit.");
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync()
            .ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory()).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            if (buffer.Length + read > maximumResponseBytes)
            {
                throw new InvalidOperationException(
                    "GitLab response exceeded the 4 MiB safety limit.");
            }
            buffer.Write(chunk, 0, read);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static ProviderReadResult Failure(
        string sourceName,
        HttpStatusCode statusCode) =>
        new(
            [],
            [
                $"GitLab {sourceName} were skipped: API returned " +
                $"{(int)statusCode} {statusCode}.",
            ]);

    private static void ValidateRepositoryName(string repositoryName)
    {
        var parts = repositoryName.Split('/');
        if (parts.Length < 2 ||
            parts.Any(static part =>
                string.IsNullOrWhiteSpace(part) ||
                part is "." or ".." ||
                part.Any(static character =>
                    !(char.IsAsciiLetterOrDigit(character) ||
                      character is '-' or '_' or '.'))))
        {
            throw new InvalidOperationException(
                "GitLab repository identity must use namespace/project syntax.");
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
        IReadOnlyList<string> Warnings,
        int RawItemCount = 0);
}
