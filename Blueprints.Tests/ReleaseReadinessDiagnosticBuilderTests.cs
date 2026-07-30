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
}
