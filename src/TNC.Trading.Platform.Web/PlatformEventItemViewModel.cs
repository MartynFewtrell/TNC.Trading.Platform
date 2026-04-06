namespace TNC.Trading.Platform.Web;

internal sealed record PlatformEventItemViewModel(
    long EventId,
    string Category,
    string EventType,
    string PlatformEnvironment,
    string BrokerEnvironment,
    string Summary,
    string Details,
    DateTimeOffset OccurredAtUtc);
