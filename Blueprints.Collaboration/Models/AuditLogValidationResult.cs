namespace Blueprints.Collaboration.Models;

public sealed record AuditLogValidationResult(
    bool IsValid,
    int EntryCount,
    string? LatestEntryHash,
    IReadOnlyList<string> InvalidEntryPaths,
    string Summary);
