using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class IntegrationStatusService
{
    private readonly IIntegrationSettingsStore _settingsStore;
    private readonly ILocalGitRepositoryInspector _localGitRepositoryInspector;
    private readonly IProviderCredentialSource _providerCredentialSource;
    private readonly IVaultSyncStatusReader _vaultSyncStatusReader;

    public IntegrationStatusService()
        : this(
            new FileSystemIntegrationSettingsStore(AppEnvironment.GetIntegrationSettingsPath()),
            new GitCommandLocalGitRepositoryInspector(),
            new EnvironmentProviderCredentialSource(),
            new FileSystemVaultSyncStatusReader())
    {
    }

    public IntegrationStatusService(
        IIntegrationSettingsStore settingsStore,
        ILocalGitRepositoryInspector localGitRepositoryInspector)
        : this(
            settingsStore,
            localGitRepositoryInspector,
            new EnvironmentProviderCredentialSource(),
            new FileSystemVaultSyncStatusReader())
    {
    }

    public IntegrationStatusService(
        IIntegrationSettingsStore settingsStore,
        ILocalGitRepositoryInspector localGitRepositoryInspector,
        IProviderCredentialSource providerCredentialSource,
        IVaultSyncStatusReader? vaultSyncStatusReader = null)
    {
        _settingsStore = settingsStore;
        _localGitRepositoryInspector = localGitRepositoryInspector;
        _providerCredentialSource = providerCredentialSource;
        _vaultSyncStatusReader = vaultSyncStatusReader ?? new FileSystemVaultSyncStatusReader();
    }

    public IReadOnlyList<IntegrationStatusCard> GetIntegrationStatuses()
    {
        var checkedAtUtc = DateTimeOffset.UtcNow;
        var settings = _settingsStore.Load();

        return
        [
            .. GetLocalGitStatuses(settings, checkedAtUtc),
            GetGitHubStatus(checkedAtUtc),
            GetGitLabStatus(checkedAtUtc),
            GetVaultSyncStatus(settings, checkedAtUtc),
        ];
    }

    public IntegrationSettings GetSettings() => _settingsStore.Load();

    public void SaveSettings(IntegrationSettings settings) => _settingsStore.Save(settings);

    private IntegrationStatusCard GetGitHubStatus(DateTimeOffset checkedAtUtc)
    {
        var hasCredential = !string.IsNullOrWhiteSpace(
            _providerCredentialSource.GetGitHubToken());
        return new IntegrationStatusCard(
            IntegrationProviderType.GitHub,
            "GitHub",
            hasCredential
                ? IntegrationConnectionState.Connected
                : IntegrationConnectionState.Warning,
            hasCredential
                ? "Direct API credential available"
                : "Public API discovery only",
            hasCredential
                ? "Source Lens can read issues, pull requests, releases, and repository-linked Project drafts through GitHub's API."
                : "Source Lens can read public issues, pull requests, and releases. Private repositories and Project drafts require an environment credential.",
            hasCredential
                ? "The credential is read from BLUEPRINTS_GITHUB_TOKEN and is never persisted by Blueprints. Provider access remains read-only."
                : "Set BLUEPRINTS_GITHUB_TOKEN in the application environment when private or Project discovery is needed.",
            BlueprintsTrustBoundary(),
            checkedAtUtc,
            []);
    }

    private IntegrationStatusCard GetGitLabStatus(DateTimeOffset checkedAtUtc)
    {
        var hasCredential = !string.IsNullOrWhiteSpace(
            _providerCredentialSource.GetGitLabToken());
        return new IntegrationStatusCard(
            IntegrationProviderType.GitLab,
            "GitLab",
            hasCredential
                ? IntegrationConnectionState.Connected
                : IntegrationConnectionState.Warning,
            hasCredential
                ? "Direct API credential available"
                : "Public API discovery only",
            hasCredential
                ? "Source Lens can read issues, merge requests, releases, and milestones from private and public GitLab.com projects."
                : "Source Lens can read public GitLab.com issues, merge requests, releases, and milestones.",
            hasCredential
                ? "The credential is read from BLUEPRINTS_GITLAB_TOKEN and is never persisted by Blueprints. Provider access remains read-only."
                : "Set BLUEPRINTS_GITLAB_TOKEN in the application environment when private-project discovery is needed.",
            BlueprintsTrustBoundary(),
            checkedAtUtc,
            []);
    }

    private IntegrationStatusCard GetVaultSyncStatus(
        IntegrationSettings settings,
        DateTimeOffset checkedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(settings.VaultSyncMetadataRoot))
        {
            return new IntegrationStatusCard(
                IntegrationProviderType.VaultSync,
                "VaultSync",
                IntegrationConnectionState.NotConfigured,
                "No VaultSync metadata root configured",
                $"Link a destination or metadata root containing .vaultsync/meta/{FileSystemVaultSyncStatusReader.MetadataFileName}.",
                "Blueprints reads passive backup health only. VaultSync remains responsible for transport and recovery; Blueprints signatures remain authoritative.",
                BlueprintsTrustBoundary(),
                checkedAtUtc,
                []);
        }

        var status = _vaultSyncStatusReader.Inspect(settings.VaultSyncMetadataRoot);
        var hasRisk = !status.MetadataStoreFound ||
            status.DestinationReachable is not true ||
            status.BackupIndexConsistent is not true ||
            status.LatestSnapshotUtc is null ||
            status.LatestBackupUtc is null ||
            status.LatestVerificationUtc is null ||
            status.MetadataConflictCount > 0 ||
            status.Warnings.Count > 0 ||
            !(status.RestoreReadiness.Equals("Ready", StringComparison.OrdinalIgnoreCase) ||
              status.RestoreReadiness.Equals("Healthy", StringComparison.OrdinalIgnoreCase));
        var target = string.IsNullOrWhiteSpace(status.DestinationAlias)
            ? status.MetadataStorePath
            : $"{status.DestinationAlias} · {status.MetadataStorePath}";
        var registeredRoot = settings.RegisteredVaultSyncExchangeRoot;
        var registeredRootMissing =
            !string.IsNullOrWhiteSpace(registeredRoot) &&
            !Directory.Exists(registeredRoot);
        hasRisk |= registeredRootMissing;
        var guidance = status.Warnings.Count == 0
            ? "Backup metadata is healthy. Blueprints used read-only evidence and did not modify VaultSync or signed project truth."
            : string.Join(" ", status.Warnings);
        if (!string.IsNullOrWhiteSpace(registeredRoot))
        {
            guidance += registeredRootMissing
                ? $" The registered exchange root is unavailable: {registeredRoot}"
                : $" Registered exchange root: {registeredRoot}";
        }

        return new IntegrationStatusCard(
            IntegrationProviderType.VaultSync,
            "VaultSync",
            !status.MetadataStoreFound
                ? IntegrationConnectionState.Error
                : hasRisk
                    ? IntegrationConnectionState.Warning
                    : IntegrationConnectionState.Connected,
            target,
            status.Summary,
            guidance,
            BlueprintsTrustBoundary(),
            checkedAtUtc,
            []);
    }

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
