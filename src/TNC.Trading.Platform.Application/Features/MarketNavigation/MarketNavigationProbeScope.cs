namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

/// <summary>One sequential probe's shared attempt ceiling, including sessions and reauthentication.</summary>
internal sealed class MarketNavigationProbeScope(ProbeMarketNavigationRequest request)
{
    internal ProbeMarketNavigationRequest Request { get; } = request;
    internal int HttpAttempts { get; private set; }
    internal bool HasCapacity => HttpAttempts < Request.MaximumHttpAttempts;

    internal void RecordSend() => HttpAttempts++;
}