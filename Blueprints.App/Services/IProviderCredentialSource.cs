namespace Blueprints.App.Services;

public interface IProviderCredentialSource
{
    string? GetGitHubToken();
}
