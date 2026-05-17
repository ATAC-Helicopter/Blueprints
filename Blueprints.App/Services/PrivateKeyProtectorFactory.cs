using Blueprints.Security.Abstractions;
using Blueprints.Security.Services;

namespace Blueprints.App.Services;

public static class PrivateKeyProtectorFactory
{
    public static IPrivateKeyProtector Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new DpapiPrivateKeyProtector();
        }

        return new LocalFilePrivateKeyProtector(AppEnvironment.GetLocalPrivateKeyProtectionKeyPath());
    }
}
