namespace Blueprints.App.Services;

public sealed class EnvironmentProviderCredentialSource : IProviderCredentialSource
{
    public string? GetGitHubToken()
    {
        var token = Environment.GetEnvironmentVariable("BLUEPRINTS_GITHUB_TOKEN");
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }
}
