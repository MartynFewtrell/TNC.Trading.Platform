using AppGetPlatformEvents = TNC.Trading.Platform.Application.Features.GetPlatformEvents;

namespace TNC.Trading.Platform.Api.Features.GetPlatformEvents;

internal static class GetPlatformEventsMapping
{
    public static GetPlatformEventsResponse ToResponse(this AppGetPlatformEvents.GetPlatformEventsResponse response)
        => new(
            response.Events.Select(item => new PlatformEventItemResponse(
                item.EventId,
                item.Category,
                item.EventType,
                item.PlatformEnvironment.ToString(),
                item.BrokerEnvironment.ToString(),
                item.Summary,
                item.Details,
                item.OccurredAtUtc)).ToArray());
}
