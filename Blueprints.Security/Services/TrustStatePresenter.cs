using Blueprints.Core.Enums;

namespace Blueprints.Security.Services;

public static class TrustStatePresenter
{
    public static string ToDisplayText(TrustState trustState) =>
        trustState switch
        {
            TrustState.Trusted => "Trusted",
            TrustState.Untrusted => "Untrusted",
            TrustState.Corrupt => "Corrupt",
            _ => "Unknown",
        };
}
