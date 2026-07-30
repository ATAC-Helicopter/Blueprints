using Blueprints.Core.Enums;

namespace Blueprints.App.Models;

public sealed record TrustedProjectKey(
    Guid UserId,
    string DisplayName,
    string KeyId,
    string PublicKeyBase64,
    DateTimeOffset FirstTrustedUtc,
    MemberRole Role = MemberRole.Editor,
    bool IsActive = true);
