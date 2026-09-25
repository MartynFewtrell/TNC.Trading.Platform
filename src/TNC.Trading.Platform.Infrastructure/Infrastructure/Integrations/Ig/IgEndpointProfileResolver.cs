using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal static class IgEndpointProfileResolver
{
    private static readonly Uri DemoBaseAddress = new("https://demo-api.ig.com/gateway/deal/");
    private static readonly Uri LiveBaseAddress = new("https://api.ig.com/gateway/deal/");

    internal static bool TryResolve(
        AppliedBrokerEnvironmentContext? context,
        out BrokerEnvironmentKind environment,
        out Uri baseAddress)
    {
        environment = default;
        baseAddress = null!;
        if (context is null
            || !string.Equals(context.Provider, "IG", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(context.Lifecycle, "Active", StringComparison.Ordinal)
            || !string.Equals(context.Availability, "Available", StringComparison.Ordinal)
            || !context.CanAccessMarketData)
        {
            return false;
        }

        if (string.Equals(context.Kind, nameof(BrokerEnvironmentKind.Demo), StringComparison.OrdinalIgnoreCase)
            && string.Equals(context.EndpointProfile, "IgDemo", StringComparison.Ordinal))
        {
            environment = BrokerEnvironmentKind.Demo;
            baseAddress = DemoBaseAddress;
            return true;
        }

        if (string.Equals(context.Kind, nameof(BrokerEnvironmentKind.Live), StringComparison.OrdinalIgnoreCase)
            && string.Equals(context.EndpointProfile, "IgLive", StringComparison.Ordinal))
        {
            environment = BrokerEnvironmentKind.Live;
            baseAddress = LiveBaseAddress;
            return true;
        }

        return false;
    }

    internal static async Task<bool> IsStillAppliedAsync(
        IAppliedBrokerEnvironmentContextResolver contextResolver,
        Guid expectedEnvironmentId,
        BrokerEnvironmentKind expectedEnvironment,
        string expectedEndpointProfile,
        Uri expectedBaseAddress,
        CancellationToken cancellationToken)
    {
        var context = await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        return context is not null
            && context.BrokerEnvironmentId == expectedEnvironmentId
            && string.Equals(context.EndpointProfile, expectedEndpointProfile, StringComparison.Ordinal)
            && TryResolve(context, out var environment, out var baseAddress)
            && environment == expectedEnvironment
            && baseAddress == expectedBaseAddress;
    }
}
