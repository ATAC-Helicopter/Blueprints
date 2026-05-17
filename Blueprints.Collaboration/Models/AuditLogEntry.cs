namespace Blueprints.Collaboration.Models;

public sealed record AuditLogEntry(
    int SchemaVersion,
    string ChangeId,
    Guid ProjectId,
    string Operation,
    string Summary,
    DateTimeOffset TimestampUtc,
    Guid AuthorUserId,
    string AuthorDisplayName,
    int MembershipRevisionSeen,
    string? PreviousEntryHash);
