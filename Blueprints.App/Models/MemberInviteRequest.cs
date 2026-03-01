using Blueprints.Core.Enums;

namespace Blueprints.App.Models;

public sealed record MemberInviteRequest(
    string UserId,
    string DisplayName,
    string PublicKey,
    MemberRole Role);
