namespace Blueprints.App.Models;

public sealed record IntegrationSettings(
    string LocalGitRepositoryPath,
    string VaultSyncMetadataRoot)
{
    public IReadOnlyList<string> LocalGitRepositoryPaths { get; init; } = [];

    public IReadOnlyList<string> EffectiveLocalGitRepositoryPaths =>
        LocalGitRepositoryPaths
            .Append(LocalGitRepositoryPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToArray();

    public static IntegrationSettings Empty { get; } = new(string.Empty, string.Empty);
}
