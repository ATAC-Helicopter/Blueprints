using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class IntegrationStatusService
{
    private readonly IIntegrationSettingsStore _settingsStore;
    private readonly ILocalGitRepositoryInspector _localGitRepositoryInspector;

    public IntegrationStatusService()
        : this(
            new FileSystemIntegrationSettingsStore(AppEnvironment.GetIntegrationSettingsPath()),
            new GitCommandLocalGitRepositoryInspector())
    {
    }

    public IntegrationStatusService(
        IIntegrationSettingsStore settingsStore,
        ILocalGitRepositoryInspector localGitRepositoryInspector)
    {
        _settingsStore = settingsStore;
        _localGitRepositoryInspector = localGitRepositoryInspector;
    }

    public IReadOnlyList<IntegrationStatusCard> GetIntegrationStatuses()
    {
        var checkedAtUtc = DateTimeOffset.UtcNow;
        var settings = _settingsStore.Load();

        return
        [
            .. GetLocalGitStatuses(settings, checkedAtUtc),
            new IntegrationStatusCard(
                IntegrationProviderType.GitHub,
                "GitHub",
                IntegrationConnectionState.NotConfigured,
                "No GitHub repository linked",
                "GitHub should enrich releases with issues, pull requests, checks, and draft release publishing.",
                "Connect only after the provider-agnostic repository and release models are stable. Remote writes must be explicit user actions.",
                BlueprintsTrustBoundary(),
                checkedAtUtc,
                []),
            new IntegrationStatusCard(
                IntegrationProviderType.GitLab,
                "GitLab",
                IntegrationConnectionState.NotConfigured,
                "No GitLab project linked",
                "GitLab should reuse the same source-control model as GitHub for issues, merge requests, pipelines, and releases.",
                "Implement after the common provider model exists so GitHub does not become the only supported worldview.",
                BlueprintsTrustBoundary(),
                checkedAtUtc,
                []),
            new IntegrationStatusCard(
                IntegrationProviderType.VaultSync,
                "VaultSync",
                IntegrationConnectionState.NotConfigured,
                "No VaultSync metadata root configured",
                "VaultSync passive awareness should detect .vaultsync/meta/vaultsync.meta.db and surface destination, snapshot, backup, verify, and restore-readiness health.",
                "Treat VaultSync as transport and backup health. Do not modify the production VaultSync app from Blueprints work; read metadata first and keep Blueprints signatures authoritative.",
                BlueprintsTrustBoundary(),
                checkedAtUtc,
                []),
        ];
    }

    public IntegrationSettings GetSettings() => _settingsStore.Load();

    public void SaveSettings(IntegrationSettings settings) => _settingsStore.Save(settings);

    private IReadOnlyList<IntegrationStatusCard> GetLocalGitStatuses(
        IntegrationSettings settings,
        DateTimeOffset checkedAtUtc)
    {
        var repositoryPaths = settings.EffectiveLocalGitRepositoryPaths;
        if (repositoryPaths.Count == 0)
        {
            return
            [
                new IntegrationStatusCard(
                IntegrationProviderType.LocalGit,
                "Local Git",
                IntegrationConnectionState.NotConfigured,
                "No repository linked",
                "Local Git awareness is ready to be wired as the first provider.",
                "Link a repository path to detect branch, origin remote, dirty state, and latest tag.",
                BlueprintsTrustBoundary(),
                checkedAtUtc,
                []),
            ];
        }

        return repositoryPaths
            .Select(path => GetLocalGitStatus(path, checkedAtUtc))
            .ToArray();
    }

    private IntegrationStatusCard GetLocalGitStatus(
        string repositoryPath,
        DateTimeOffset checkedAtUtc)
    {
        var gitStatus = _localGitRepositoryInspector.Inspect(repositoryPath);
        if (!gitStatus.IsRepository)
        {
            return new IntegrationStatusCard(
                IntegrationProviderType.LocalGit,
                "Local Git",
                IntegrationConnectionState.Error,
                gitStatus.RepositoryRoot,
                gitStatus.Summary,
                "Choose a folder inside a valid Git repository. Detection is read-only and will not modify the repository.",
                BlueprintsTrustBoundary(),
                checkedAtUtc,
                []);
        }

        return new IntegrationStatusCard(
            IntegrationProviderType.LocalGit,
            "Local Git",
            gitStatus.IsDirty ? IntegrationConnectionState.Warning : IntegrationConnectionState.Connected,
            gitStatus.RepositoryRoot,
            $"Branch: {gitStatus.Branch}. Latest tag: {gitStatus.LatestTag}. Origin: {gitStatus.RemoteUrl}. Recent changes: {gitStatus.RecentChanges.Count}.",
            gitStatus.IsDirty
                ? "Review uncommitted changes before releasing. Blueprints will not commit, tag, or publish automatically."
                : "Repository is clean. Recent commits can now be matched to Blueprints item keys for release planning.",
            BlueprintsTrustBoundary(),
            checkedAtUtc,
            gitStatus.RecentChanges);
    }

    private static string BlueprintsTrustBoundary() =>
        "Blueprints signatures, membership, manifests, and audit log remain the trust authority.";
}
