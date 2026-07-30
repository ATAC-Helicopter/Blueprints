namespace Blueprints.App.Models;

public sealed record ProviderWriteApproval(
    Guid ApprovalId,
    ProviderOperationIntent Intent,
    DateTimeOffset ApprovedUtc,
    DateTimeOffset ExpiresUtc);
