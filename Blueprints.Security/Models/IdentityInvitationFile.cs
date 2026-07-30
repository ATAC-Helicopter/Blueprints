namespace Blueprints.Security.Models;

public sealed record IdentityInvitationFile(
    IdentityInvitationPayload Identity,
    DetachedSignature Proof);
