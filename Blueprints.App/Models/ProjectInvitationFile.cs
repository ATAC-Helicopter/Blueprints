using Blueprints.Security.Models;

namespace Blueprints.App.Models;

public sealed record ProjectInvitationFile(
    ProjectInvitationPayload Project,
    DetachedSignature Proof);
