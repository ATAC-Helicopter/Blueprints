using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.Core.Enums;

namespace Blueprints.Tests;

public sealed class ReleaseReadinessDiagnosticBuilderTests
{
    [Fact]
    public void Build_BlocksDirtyRepositoryAndCallsOutUnmatchedChanges()
    {
        var version = CreateVersion(
            new WorkspaceItemCard(Guid.NewGuid(), "BP-10", "Done", string.Empty, "feature", "added", true),
            new WorkspaceItemCard(Guid.NewGuid(), "BP-11", "Open", string.Empty, "feature", "added", false));
        var change = new SourceChangeSummary(
            "abcdef123",
            "abcdef1",
            "Refactor internal pipeline",
            "Flavio",
            DateTimeOffset.UtcNow,
            []);
        var sourceDiagnostic = new VersionSourceChangeDiagnostic(change, [], false);

        var diagnostics = ReleaseReadinessDiagnosticBuilder.Build(
            version,
            CreateLocalGit(IntegrationConnectionState.Warning, [change]),
            [sourceDiagnostic]);

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Level == ReleaseReadinessLevel.Blocking
                && diagnostic.Title.Contains("uncommitted", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Title.Contains("unmatched", StringComparison.OrdinalIgnoreCase)
                && diagnostic.Detail.Contains("abcdef1", StringComparison.Ordinal));
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Title.Contains("Incomplete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_ReturnsReadyWhenRepositoryIsCleanAndEveryChangeMatches()
    {
        var item = new WorkspaceItemCard(
            Guid.NewGuid(),
            "BP-10",
            "Done",
            string.Empty,
            "feature",
            "added",
            true);
        var version = CreateVersion(item);
        var change = new SourceChangeSummary(
            "abcdef123",
            "abcdef1",
            "BP-10 Finish readiness",
            "Flavio",
            DateTimeOffset.UtcNow,
            ["BP-10"]);

        var diagnostics = ReleaseReadinessDiagnosticBuilder.Build(
            version,
            CreateLocalGit(IntegrationConnectionState.Connected, [change]),
            [new VersionSourceChangeDiagnostic(change, ["BP-10"], true)]);

        var ready = Assert.Single(diagnostics);
        Assert.Equal(ReleaseReadinessLevel.Ready, ready.Level);
    }

    [Fact]
    public void Build_AddsAdvisoryWhenVaultSyncIsNotConfigured()
    {
        var version = CreateVersion();

        var diagnostics = ReleaseReadinessDiagnosticBuilder.Build(
            version,
            CreateLocalGit(IntegrationConnectionState.Connected, []),
            [],
            CreateVaultSync(IntegrationConnectionState.NotConfigured));

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Level == ReleaseReadinessLevel.Attention &&
                diagnostic.Title.Contains("not configured", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Level == ReleaseReadinessLevel.Blocking);
    }

    [Fact]
    public void Build_ReportsRecentVerifiedVaultSyncEvidenceAsReady()
    {
        var now = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var version = CreateVersion();
        var status = CreateVaultSync(
            IntegrationConnectionState.Connected,
            CreateVaultSyncSummary(now.AddHours(-2)));

        var diagnostics = ReleaseReadinessDiagnosticBuilder.Build(
            version,
            CreateLocalGit(IntegrationConnectionState.Connected, []),
            [],
            status,
            now);

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Level == ReleaseReadinessLevel.Ready &&
                diagnostic.Title.Contains("VaultSync", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WarnsForStaleOrFutureDatedVaultSyncEvidence()
    {
        var now = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var version = CreateVersion();

        var stale = ReleaseReadinessDiagnosticBuilder.Build(
            version,
            CreateLocalGit(IntegrationConnectionState.Connected, []),
            [],
            CreateVaultSync(
                IntegrationConnectionState.Connected,
                CreateVaultSyncSummary(
                    now - ReleaseReadinessDiagnosticBuilder.MaximumVaultSyncEvidenceAge - TimeSpan.FromMinutes(1))),
            now);
        var future = ReleaseReadinessDiagnosticBuilder.Build(
            version,
            CreateLocalGit(IntegrationConnectionState.Connected, []),
            [],
            CreateVaultSync(
                IntegrationConnectionState.Connected,
                CreateVaultSyncSummary(
                    now + ReleaseReadinessDiagnosticBuilder.MaximumFutureClockSkew + TimeSpan.FromMinutes(1))),
            now);

        Assert.Contains(
            stale,
            diagnostic => diagnostic.Title.Contains("stale", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            future,
            diagnostic => diagnostic.Title.Contains("future", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_KeepsRiskyVaultSyncHealthAdvisoryByDefault()
    {
        var version = CreateVersion();

        var diagnostics = ReleaseReadinessDiagnosticBuilder.Build(
            version,
            CreateLocalGit(IntegrationConnectionState.Connected, []),
            [],
            CreateVaultSync(IntegrationConnectionState.Warning));

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Level == ReleaseReadinessLevel.Attention &&
                diagnostic.Title.Contains("needs review", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Level == ReleaseReadinessLevel.Blocking);
    }

    private static WorkspaceVersionCard CreateVersion(params WorkspaceItemCard[] items) =>
        new(
            Guid.NewGuid(),
            "0.4.0",
            ReleaseStatus.InProgress,
            string.Empty,
            items.Length,
            items.Count(static item => item.IsDone),
            items);

    private static IntegrationStatusCard CreateLocalGit(
        IntegrationConnectionState state,
        IReadOnlyList<SourceChangeSummary> changes) =>
        new(
            IntegrationProviderType.LocalGit,
            "Local Git",
            state,
            "/repo",
            state == IntegrationConnectionState.Warning
                ? "Repository has uncommitted changes."
                : "Repository working tree is clean.",
            "Review repository state.",
            "Blueprints remains authoritative.",
            DateTimeOffset.UtcNow,
            changes);

    private static IntegrationStatusCard CreateVaultSync(
        IntegrationConnectionState state,
        VaultSyncStatusSummary? status = null) =>
        new(
            IntegrationProviderType.VaultSync,
            "VaultSync",
            state,
            "/backup/.vaultsync/meta/vaultsync.meta.db",
            status?.Summary ?? "VaultSync health is unavailable.",
            "Review VaultSync backup health.",
            "Blueprints remains authoritative.",
            DateTimeOffset.UtcNow,
            [])
        {
            VaultSyncStatus = status,
        };

    private static VaultSyncStatusSummary CreateVaultSyncSummary(
        DateTimeOffset evidenceUtc) =>
        new(
            true,
            "/backup/.vaultsync/meta/vaultsync.meta.db",
            "/backup/.vaultsync/meta/blueprints.status.json",
            evidenceUtc,
            "project-42",
            "Blueprints",
            "NAS",
            true,
            evidenceUtc,
            evidenceUtc,
            evidenceUtc,
            true,
            "Ready",
            0,
            [],
            $"Restore readiness: Ready. Verified {evidenceUtc:O}.");
}
