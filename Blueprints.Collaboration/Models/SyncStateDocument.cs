namespace Blueprints.Collaboration.Models;

public sealed record SyncStateDocument(
    int SchemaVersion,
    int LastPulledManifestVersion,
    int LastPushedManifestVersion,
    DateTimeOffset? LastSuccessfulTrustValidationUtc,
    IReadOnlyList<string> KnownRemoteBatchIds,
    IReadOnlyList<string> UnresolvedConflicts,
    IReadOnlyList<SyncTrackedEntry> TrackedEntries);
