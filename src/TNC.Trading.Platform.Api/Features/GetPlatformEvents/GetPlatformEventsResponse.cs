namespace TNC.Trading.Platform.Api.Features.GetPlatformEvents;

internal sealed record GetPlatformEventsResponse(
    IReadOnlyList<PlatformEventItemResponse> Events);

internal sealed record PlatformEventItemResponse(
    long EventId,
    string Category,
    string EventType,
    string PlatformEnvironment,
    string BrokerEnvironment,
    string Summary,
    string Details,
    DateTimeOffset OccurredAtUtc);
