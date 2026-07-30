using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class IntegrationStatusServiceTests
{
    [Fact]
    public void GetIntegrationStatuses_ReturnsExpectedProviderSpine()
    {
        var service = CreateService(IntegrationSettings.Empty);

        var statuses = service.GetIntegrationStatuses();

        Assert.Collection(
            statuses,
            status => Assert.Equal(IntegrationProviderType.LocalGit, status.Provider),
            status => Assert.Equal(IntegrationProviderType.GitHub, status.Provider),
            status => Assert.Equal(IntegrationProviderType.GitLab, status.Provider),
            status => Assert.Equal(IntegrationProviderType.VaultSync, status.Provider));
        Assert.All(statuses, status => Assert.Equal(IntegrationConnectionState.NotConfigured, status.State));
    }

    [Fact]
    public void GetIntegrationStatuses_KeepsBlueprintsTrustBoundaryForEveryProvider()
    {
        var service = CreateService(IntegrationSettings.Empty);

        var statuses = service.GetIntegrationStatuses();

        Assert.All(
            statuses,
            status =>
            {
                Assert.Contains("Blueprints signatures", status.TrustBoundary, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("audit log", status.TrustBoundary, StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public void GetIntegrationStatuses_DescribesVaultSyncAsPassiveAwarenessFirst()
    {
        var service = CreateService(IntegrationSettings.Empty);

        var vaultSync = service.GetIntegrationStatuses()
            .Single(status => status.Provider == IntegrationProviderType.VaultSync);

        Assert.Contains(".vaultsync/meta/vaultsync.meta.db", vaultSync.Summary, StringComparison.Ordinal);
        Assert.Contains("transport", vaultSync.Guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("backup health", vaultSync.Guidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetIntegrationStatuses_DetectsConfiguredCleanLocalGitRepository()
    {
        var service = CreateService(
            new IntegrationSettings("/repo", string.Empty),
            new LocalGitRepositoryStatus(
                true,
                "/repo",
                "main",
                "https://example.test/repo.git",
                false,
                "v1.0.0",
                [
                    new SourceChangeSummary(
                        "abcdef1234567890",
                        "abcdef1",
                        "BP-101 Wire release intelligence",
                        "Flavio",
                        DateTimeOffset.Parse("2026-05-17T12:00:00Z"),
                        ["BP-101"]),
                ],
                "Repository working tree is clean."));

        var localGit = service.GetIntegrationStatuses()
            .Single(status => status.Provider == IntegrationProviderType.LocalGit);

        Assert.Equal(IntegrationConnectionState.Connected, localGit.State);
        Assert.Equal("/repo", localGit.Target);
        Assert.Contains("main", localGit.Summary, StringComparison.Ordinal);
        Assert.Contains("v1.0.0", localGit.Summary, StringComparison.Ordinal);
        Assert.Single(localGit.RecentChanges);
        Assert.Equal("BP-101", localGit.RecentChanges.Single().MatchedItemKeys.Single());
    }

    [Fact]
    public void GetIntegrationStatuses_WarnsWhenConfiguredLocalGitRepositoryIsDirty()
    {
        var service = CreateService(
            new IntegrationSettings("/repo", string.Empty),
            new LocalGitRepositoryStatus(
                true,
                "/repo",
                "feature",
                "(no origin remote)",
                true,
                "(no tags)",
                [],
                "Repository has uncommitted changes."));

        var localGit = service.GetIntegrationStatuses()
            .Single(status => status.Provider == IntegrationProviderType.LocalGit);

        Assert.Equal(IntegrationConnectionState.Warning, localGit.State);
        Assert.Contains("uncommitted", localGit.Guidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetIntegrationStatuses_InspectsEveryDistinctLinkedRepository()
    {
        var settings = new IntegrationSettings("/repo-a", string.Empty)
        {
            LocalGitRepositoryPaths = ["/repo-a", "/repo-b"],
        };
        var service = CreateService(
            settings,
            new LocalGitRepositoryStatus(
                true,
                string.Empty,
                "main",
                "(no origin remote)",
                false,
                "(no tags)",
                [],
                "Repository working tree is clean."));

        var localGit = service.GetIntegrationStatuses()
            .Where(status => status.Provider == IntegrationProviderType.LocalGit)
            .ToArray();

        Assert.Equal(2, localGit.Length);
        Assert.Equal(["/repo-a", "/repo-b"], localGit.Select(static status => status.Target));
        Assert.All(localGit, status => Assert.Equal(IntegrationConnectionState.Connected, status.State));
    }

    private static IntegrationStatusService CreateService(
        IntegrationSettings settings,
        LocalGitRepositoryStatus? gitStatus = null) =>
        new(
            new TestIntegrationSettingsStore(settings),
            new TestLocalGitRepositoryInspector(gitStatus));

    private sealed class TestIntegrationSettingsStore : IIntegrationSettingsStore
    {
        private IntegrationSettings _settings;

        public TestIntegrationSettingsStore(IntegrationSettings settings)
        {
            _settings = settings;
        }

        public IntegrationSettings Load() => _settings;

        public void Save(IntegrationSettings settings) => _settings = settings;
    }

    private sealed class TestLocalGitRepositoryInspector : ILocalGitRepositoryInspector
    {
        private readonly LocalGitRepositoryStatus? _status;

        public TestLocalGitRepositoryInspector(LocalGitRepositoryStatus? status)
        {
            _status = status;
        }

        public LocalGitRepositoryStatus Inspect(string repositoryPath) =>
            _status is null
                ? new LocalGitRepositoryStatus(
                    false,
                    repositoryPath,
                    string.Empty,
                    string.Empty,
                    false,
                    string.Empty,
                    [],
                    "Configured path is not a Git repository.")
                : _status with { RepositoryRoot = repositoryPath };
    }
}
