namespace Blueprints.App.Models;

public sealed record ConflictRecoveryRecord(
    int SchemaVersion,
    string RecoveryId,
    DateTimeOffset CreatedUtc,
    string DocumentPath,
    ConflictResolutionChoice Choice,
    bool LocalDocumentPresent,
    bool LocalSignaturePresent,
    bool SharedDocumentPresent,
    bool SharedSignaturePresent,
    string Status);
