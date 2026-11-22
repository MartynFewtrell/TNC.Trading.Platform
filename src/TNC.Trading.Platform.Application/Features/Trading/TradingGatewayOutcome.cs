namespace TNC.Trading.Platform.Application.Features.Trading;

internal abstract record TradingGatewayOutcome<T>
{
    private TradingGatewayOutcome()
    {
    }

    internal sealed record Succeeded(T Value, DateTimeOffset RetrievedAtUtc) : TradingGatewayOutcome<T>;

    internal sealed record Failed(TradingFailureCategory Category, string SafeReason) : TradingGatewayOutcome<T>;

    internal sealed record Indeterminate(TradingFailureCategory Category, string SafeReason) : TradingGatewayOutcome<T>;
}
