namespace Blueprints.App.Models;

public sealed record VaultSyncStatusSummary(
    bool MetadataStoreFound,
    string MetadataStorePath,
    string StatusDocumentPath,
    DateTimeOffset? LastMetadataWriteUtc,
    string ProjectExternalId,
    string ProjectName,
    string DestinationAlias,
    bool? DestinationReachable,
    DateTimeOffset? LatestSnapshotUtc,
    DateTimeOffset? LatestBackupUtc,
    DateTimeOffset? LatestVerificationUtc,
    bool? BackupIndexConsistent,
    string RestoreReadiness,
    int MetadataConflictCount,
    IReadOnlyList<string> Warnings,
    string Summary);
