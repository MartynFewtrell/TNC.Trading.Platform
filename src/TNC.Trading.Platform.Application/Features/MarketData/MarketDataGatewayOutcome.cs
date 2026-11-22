namespace TNC.Trading.Platform.Application.Features.MarketData;

internal abstract record MarketDataGatewayOutcome<T>
{
    private MarketDataGatewayOutcome()
    {
    }

    internal sealed record Succeeded(T Value, DateTimeOffset RetrievedAtUtc) : MarketDataGatewayOutcome<T>;

    internal sealed record Failed(MarketDataFailureCategory Category, string SafeReason) : MarketDataGatewayOutcome<T>;
}
