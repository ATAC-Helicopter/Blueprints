namespace Blueprints.App.Services;

public sealed class EnvironmentProviderCredentialSource : IProviderCredentialSource
{
    public string? GetGitHubToken()
    {
        var token = Environment.GetEnvironmentVariable("BLUEPRINTS_GITHUB_TOKEN");
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    public string? GetGitLabToken()
    {
        var token = Environment.GetEnvironmentVariable("BLUEPRINTS_GITLAB_TOKEN");
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }
}
