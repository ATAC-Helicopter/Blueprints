namespace Blueprints.App.Models;

public sealed record IntegrationStatusCard(
    IntegrationProviderType Provider,
    string DisplayName,
    IntegrationConnectionState State,
    string Target,
    string Summary,
    string Guidance,
    string TrustBoundary,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<SourceChangeSummary> RecentChanges)
{
    public VaultSyncStatusSummary? VaultSyncStatus { get; init; }

    public string CheckedAtSummary => $"Checked {CheckedAtUtc:yyyy-MM-dd HH:mm} UTC";

    public string RecentChangeSummary =>
        RecentChanges.Count == 0
            ? "No recent changes loaded"
            : $"{RecentChanges.Count} recent changes loaded";
}
