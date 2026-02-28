using System.Runtime.Versioning;
using Blueprints.Security.Services;

namespace Blueprints.Tests;

[SupportedOSPlatform("windows")]
public sealed class DpapiPrivateKeyProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsPrivateKeyPayload()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var protector = new DpapiPrivateKeyProtector();
        var original = new byte[] { 1, 2, 3, 4, 5, 6 };

        var protectedBytes = protector.Protect(original);
        var unprotectedBytes = protector.Unprotect(protectedBytes);

        Assert.NotEqual(original, protectedBytes);
        Assert.Equal(original, unprotectedBytes);
    }
}
