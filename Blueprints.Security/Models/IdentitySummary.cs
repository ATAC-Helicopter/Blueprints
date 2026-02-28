namespace Blueprints.Security.Models;

public sealed record IdentitySummary(
    string DisplayName,
    string UserId,
    string KeyStorageProvider);
