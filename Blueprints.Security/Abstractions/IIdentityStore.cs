using Blueprints.Security.Models;

namespace Blueprints.Security.Abstractions;

public interface IIdentityStore
{
    StoredIdentity Create(string displayName);

    StoredIdentity Load(Guid userId);
}
