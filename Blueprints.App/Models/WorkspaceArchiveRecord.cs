namespace Blueprints.App.Models;

public sealed record WorkspaceArchiveRecord(
    int SchemaVersion,
    string ArchiveId,
    string EntityType,
    Guid EntityId,
    string DisplayName,
    DateTimeOffset CreatedUtc,
    Guid ArchivedByUserId,
    string ArchivedByDisplayName,
    string Status);
