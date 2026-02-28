namespace Blueprints.Collaboration.Models;

public sealed record SyncManifestDocument(
    int SchemaVersion,
    Guid ProjectId,
    int ManifestVersion,
    string BatchId,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<SyncManifestEntry> Entries);
