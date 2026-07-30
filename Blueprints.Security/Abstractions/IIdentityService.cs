using Blueprints.Security.Models;

namespace Blueprints.Security.Abstractions;

public interface IIdentityService
{
    StoredIdentity CreateIdentity(string displayName);

    StoredIdentity GetOrCreateDefaultIdentity(string displayName);

    IReadOnlyList<IdentityProfile> ListProfiles();
}
