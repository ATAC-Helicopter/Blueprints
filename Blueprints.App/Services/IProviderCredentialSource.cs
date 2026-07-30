namespace Blueprints.App.Services;

public interface IProviderCredentialSource
{
    string? GetGitHubToken();

    string? GetGitLabToken();
}
