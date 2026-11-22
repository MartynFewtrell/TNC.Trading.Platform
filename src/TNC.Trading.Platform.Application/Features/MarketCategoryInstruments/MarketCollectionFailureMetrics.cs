using System.Diagnostics.Metrics;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal static class MarketCollectionFailureMetrics
{
    internal const string MeterName = "TNC.Trading.Platform.MarketCollection";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("platform.market_collection.failures");

    public static void Record(
        BrokerEnvironmentKind environment,
        string category,
        string failureKind,
        string? providerErrorCode = null)
    {
        var safeCode = MarketDetailProviderErrorCodes.IsAllowed(providerErrorCode)
            ? providerErrorCode
            : "none";

        Failures.Add(
            1,
            new KeyValuePair<string, object?>("environment", environment.ToString()),
            new KeyValuePair<string, object?>("category", category),
            new KeyValuePair<string, object?>("failure_kind", failureKind),
            new KeyValuePair<string, object?>("provider_error_code", safeCode));
    }
}
