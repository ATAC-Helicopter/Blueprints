namespace Blueprints.App.Models;

public sealed record ProjectInvitationPayload(
    int SchemaVersion,
    Guid ProjectId,
    string ProjectName,
    string ProjectCode,
    string SharedWorkspaceRoot,
    int MembershipRevision,
    Guid InvitedUserId,
    string InvitedKeyId,
    Guid InviterUserId,
    string InviterDisplayName,
    string InviterKeyId,
    string InviterPublicKeyBase64,
    IReadOnlyList<TrustedProjectKey> TrustedKeys,
    DateTimeOffset CreatedUtc);
