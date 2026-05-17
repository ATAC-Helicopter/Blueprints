namespace Blueprints.App.Models;

public sealed record IntegrationSettings(
    string LocalGitRepositoryPath,
    string VaultSyncMetadataRoot)
{
    public static IntegrationSettings Empty { get; } = new(string.Empty, string.Empty);
}
