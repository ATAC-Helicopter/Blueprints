using Blueprints.Core.Enums;

namespace Blueprints.Core.Models;

public sealed record ProjectMember(
    Guid UserId,
    string DisplayName,
    string PublicKey,
    MemberRole Role,
    DateTimeOffset JoinedUtc,
    bool IsActive);
