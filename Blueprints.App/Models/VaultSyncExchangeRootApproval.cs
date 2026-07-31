namespace Blueprints.App.Models;

public sealed record VaultSyncExchangeRootApproval(
    Guid ApprovalId,
    VaultSyncExchangeRootIntent Intent,
    DateTimeOffset ApprovedUtc,
    DateTimeOffset ExpiresUtc);
