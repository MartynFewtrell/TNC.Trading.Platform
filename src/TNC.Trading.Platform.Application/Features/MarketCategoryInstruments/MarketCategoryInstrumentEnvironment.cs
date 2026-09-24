using System.Diagnostics.CodeAnalysis;
using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal static class MarketCategoryInstrumentEnvironment
{
    public static bool TryGetSupported(
        [NotNullWhen(true)] AppliedBrokerEnvironmentContext? applied,
        out BrokerEnvironmentKind environment)
    {
        environment = default;
        if (applied is null
            || !applied.IsExecutable
            || !applied.CanAccessMarketData
            || !string.Equals(applied.Provider, "IG", StringComparison.OrdinalIgnoreCase)
            || !Enum.TryParse(applied.Kind, true, out environment))
        {
            return false;
        }

        return environment switch
        {
            BrokerEnvironmentKind.Demo => string.Equals(applied.EndpointProfile, "IgDemo", StringComparison.Ordinal),
            BrokerEnvironmentKind.Live => string.Equals(applied.EndpointProfile, "IgLive", StringComparison.Ordinal),
            _ => false
        };
    }
}
