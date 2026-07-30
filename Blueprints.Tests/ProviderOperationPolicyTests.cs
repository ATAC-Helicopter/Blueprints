using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class ProviderOperationPolicyTests
{
    [Fact]
    public void Authorize_AllowsReadWithoutApproval()
    {
        var policy = new ProviderOperationPolicy();

        policy.Authorize(
            Intent(ProviderOperationKind.ReadSource),
            null,
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Authorize_RequiresExactFreshSingleUseApprovalForWrites()
    {
        var policy = new ProviderOperationPolicy();
        var now = DateTimeOffset.UtcNow;
        var intent = Intent(ProviderOperationKind.PublishRelease);
        var approval = new ProviderWriteApproval(
            Guid.NewGuid(),
            intent,
            now,
            now.AddMinutes(5));

        Assert.Throws<InvalidOperationException>(
            () => policy.Authorize(intent, null, now));
        Assert.Throws<InvalidOperationException>(
            () => policy.Authorize(
                intent,
                approval with
                {
                    Intent = intent with { Target = "v0.4.1" },
                },
                now));

        policy.Authorize(intent, approval, now);

        Assert.Throws<InvalidOperationException>(
            () => policy.Authorize(intent, approval, now));
    }

    [Fact]
    public void Authorize_RejectsExpiredAndOverlongApprovals()
    {
        var policy = new ProviderOperationPolicy();
        var now = DateTimeOffset.UtcNow;
        var intent = Intent(ProviderOperationKind.CreateIssue);

        Assert.Throws<InvalidOperationException>(
            () => policy.Authorize(
                intent,
                new ProviderWriteApproval(
                    Guid.NewGuid(),
                    intent,
                    now.AddMinutes(-2),
                    now.AddSeconds(-1)),
                now));
        Assert.Throws<InvalidOperationException>(
            () => policy.Authorize(
                intent,
                new ProviderWriteApproval(
                    Guid.NewGuid(),
                    intent,
                    now,
                    now.AddMinutes(11)),
                now));
    }

    private static ProviderOperationIntent Intent(ProviderOperationKind operation) =>
        new(
            SourceProviderKind.GitHub,
            "example/project",
            operation,
            operation == ProviderOperationKind.PublishRelease
                ? "v0.4.0"
                : "new");
}
