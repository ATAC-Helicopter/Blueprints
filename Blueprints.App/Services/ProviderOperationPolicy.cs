using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class ProviderOperationPolicy
{
    public static readonly TimeSpan MaximumApprovalLifetime = TimeSpan.FromMinutes(10);
    private readonly HashSet<Guid> _consumedApprovals = [];
    private readonly Lock _lock = new();

    public void Authorize(
        ProviderOperationIntent intent,
        ProviderWriteApproval? approval,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ValidateIntent(intent);
        if (intent.Operation == ProviderOperationKind.ReadSource)
        {
            return;
        }

        if (approval is null)
        {
            throw new InvalidOperationException(
                "Provider write operations require a separate explicit approval.");
        }

        if (approval.ApprovalId == Guid.Empty ||
            approval.Intent != intent ||
            approval.ApprovedUtc > nowUtc ||
            approval.ExpiresUtc <= nowUtc ||
            approval.ExpiresUtc - approval.ApprovedUtc > MaximumApprovalLifetime)
        {
            throw new InvalidOperationException(
                "Provider write approval is invalid, expired, or does not match the exact operation.");
        }

        lock (_lock)
        {
            if (!_consumedApprovals.Add(approval.ApprovalId))
            {
                throw new InvalidOperationException(
                    "Provider write approval has already been consumed.");
            }
        }
    }

    private static void ValidateIntent(ProviderOperationIntent intent)
    {
        if (intent.Provider == SourceProviderKind.Local ||
            string.IsNullOrWhiteSpace(intent.Repository) ||
            string.IsNullOrWhiteSpace(intent.Target))
        {
            throw new InvalidOperationException(
                "Provider operations require a hosted provider, repository, and exact target.");
        }
    }
}
