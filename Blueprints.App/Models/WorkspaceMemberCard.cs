using Blueprints.Core.Enums;

namespace Blueprints.App.Models;

public sealed record WorkspaceMemberCard(
    Guid UserId,
    string DisplayName,
    string PublicKey,
    MemberRole Role,
    bool IsActive,
    bool IsCurrentIdentity);
