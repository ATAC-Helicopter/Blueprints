using Blueprints.App.Models;

namespace Blueprints.App.Services;

public static class ReleaseReadinessDiagnosticBuilder
{
    public static readonly TimeSpan MaximumVaultSyncEvidenceAge = TimeSpan.FromDays(7);
    public static readonly TimeSpan MaximumFutureClockSkew = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<ReleaseReadinessDiagnostic> Build(
        WorkspaceVersionCard? version,
        IntegrationStatusCard? localGit,
        IReadOnlyList<VersionSourceChangeDiagnostic> sourceChanges,
        IntegrationStatusCard? vaultSync = null,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(sourceChanges);
        var diagnostics = new List<ReleaseReadinessDiagnostic>();

        if (version is null)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Blocking,
                    "No release version selected",
                    "Source history cannot be evaluated without a target version.",
                    "Select or create a version before reviewing release readiness."));
            return diagnostics;
        }

        AddRepositoryDiagnostic(diagnostics, localGit);
        if (vaultSync is not null)
        {
            AddVaultSyncDiagnostic(
                diagnostics,
                vaultSync,
                nowUtc ?? DateTimeOffset.UtcNow);
        }

        var incompleteItemCount = version.Items.Count(static item => !item.IsDone);
        if (incompleteItemCount > 0)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "Incomplete release items",
                    $"{incompleteItemCount} selected-version items are not marked complete.",
                    "Finish them, move them to another version, or deliberately export them as incomplete."));
        }

        if (localGit is { State: IntegrationConnectionState.Connected or IntegrationConnectionState.Warning })
        {
            AddSourceChangeDiagnostics(diagnostics, sourceChanges);
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Ready,
                    "Source history is ready",
                    "The linked repository is clean and every recent change maps to a completed item in this version.",
                    "Review the changelog preview, then freeze or release when the human review is complete."));
        }

        return diagnostics;
    }

    private static void AddVaultSyncDiagnostic(
        ICollection<ReleaseReadinessDiagnostic> diagnostics,
        IntegrationStatusCard vaultSync,
        DateTimeOffset nowUtc)
    {
        if (vaultSync.Provider != IntegrationProviderType.VaultSync)
        {
            throw new ArgumentException(
                "VaultSync release readiness requires the VaultSync integration card.",
                nameof(vaultSync));
        }

        if (vaultSync.State == IntegrationConnectionState.NotConfigured)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "Backup health is not configured",
                    "No VaultSync recovery evidence is available for this release.",
                    "Link VaultSync health if backup verification should be part of release review. Core release planning remains available."));
            return;
        }

        if (vaultSync.State is IntegrationConnectionState.Warning or IntegrationConnectionState.Error ||
            vaultSync.VaultSyncStatus is not { } status)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "Backup health needs review",
                    vaultSync.Summary,
                    vaultSync.Guidance));
            return;
        }

        var evidenceTimes = new[]
        {
            ("snapshot", status.LatestSnapshotUtc),
            ("backup", status.LatestBackupUtc),
            ("verification", status.LatestVerificationUtc),
        };
        if (evidenceTimes.Any(static evidence => evidence.Item2 is null))
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "Backup evidence is incomplete",
                    "VaultSync did not report every required snapshot, backup, and verification timestamp.",
                    "Refresh VaultSync health before relying on recovery readiness."));
            return;
        }

        var futureEvidence = evidenceTimes
            .FirstOrDefault(evidence =>
                evidence.Item2 is DateTimeOffset timestamp &&
                timestamp > nowUtc.Add(MaximumFutureClockSkew));
        if (futureEvidence.Item2 is DateTimeOffset futureTimestamp)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "Backup evidence is future-dated",
                    $"The latest {futureEvidence.Item1} timestamp is {futureTimestamp:yyyy-MM-dd HH:mm} UTC, beyond the allowed clock skew.",
                    "Check the producing machine clock and refresh VaultSync health before relying on this evidence."));
            return;
        }

        var staleEvidence = evidenceTimes
            .FirstOrDefault(evidence =>
                evidence.Item2 is DateTimeOffset timestamp &&
                nowUtc - timestamp > MaximumVaultSyncEvidenceAge);
        if (staleEvidence.Item2 is DateTimeOffset staleTimestamp)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "Backup evidence is stale",
                    $"The latest {staleEvidence.Item1} evidence is from {staleTimestamp:yyyy-MM-dd HH:mm} UTC.",
                    $"Refresh snapshot, backup, and verification evidence within {MaximumVaultSyncEvidenceAge.TotalDays:0} days before relying on recovery readiness."));
            return;
        }

        diagnostics.Add(
            new ReleaseReadinessDiagnostic(
                ReleaseReadinessLevel.Ready,
                "VaultSync recovery evidence is ready",
                status.Summary,
                "The producer reports a reachable destination, consistent index, and recent snapshot, backup, and verification. This is advisory evidence, not a replacement for Blueprints signatures."));
    }

    private static void AddRepositoryDiagnostic(
        ICollection<ReleaseReadinessDiagnostic> diagnostics,
        IntegrationStatusCard? localGit)
    {
        if (localGit is null || localGit.State == IntegrationConnectionState.NotConfigured)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "No source repository linked",
                    "Blueprints cannot compare this release plan with source history.",
                    "Link a local Git repository in Source Lens. Core release planning remains available offline."));
            return;
        }

        if (localGit.State == IntegrationConnectionState.Error)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Blocking,
                    "Linked repository is unavailable",
                    localGit.Summary,
                    localGit.Guidance));
            return;
        }

        if (localGit.State == IntegrationConnectionState.Warning)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Blocking,
                    "Repository has uncommitted changes",
                    $"{localGit.Target} is not clean, so its source history is not a stable release baseline.",
                    "Review, commit, stash, or deliberately discard the working-tree changes outside Blueprints, then refresh health."));
        }
    }

    private static void AddSourceChangeDiagnostics(
        ICollection<ReleaseReadinessDiagnostic> diagnostics,
        IReadOnlyList<VersionSourceChangeDiagnostic> sourceChanges)
    {
        if (sourceChanges.Count == 0)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "No recent source changes found",
                    "There are no commits after the repository's latest tag to compare with this version.",
                    "Confirm the tag boundary and repository link are correct."));
            return;
        }

        var unmatched = sourceChanges
            .Where(static change => !change.MatchesSelectedVersion)
            .ToArray();
        if (unmatched.Length > 0)
        {
            var examples = string.Join(
                ", ",
                unmatched
                    .Take(3)
                    .Select(static change => $"{change.Change.ShortHash} {change.Change.Subject}"));
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "Recent commits are unmatched",
                    $"{unmatched.Length} recent commits do not reference a completed item in this version. {examples}",
                    "Connect the commits to completed item keys, move the relevant item into this version, or confirm the commits are intentionally out of scope."));
        }
    }
}
