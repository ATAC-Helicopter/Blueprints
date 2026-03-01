using Blueprints.Core.Enums;

namespace Blueprints.App.Models;

public sealed record MemberUpdateRequest(
    Guid UserId,
    string DisplayName,
    MemberRole Role,
    bool IsActive);
