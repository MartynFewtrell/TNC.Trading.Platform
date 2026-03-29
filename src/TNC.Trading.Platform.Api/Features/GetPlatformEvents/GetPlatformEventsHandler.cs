using TNC.Trading.Platform.Api.Infrastructure.Platform;

namespace TNC.Trading.Platform.Api.Features.GetPlatformEvents;

internal sealed class GetPlatformEventsHandler(PlatformStateCoordinator coordinator)
{
    public async Task<GetPlatformEventsResponse> HandleAsync(GetPlatformEventsRequest request, CancellationToken cancellationToken)
    {
        var events = await coordinator.GetEventsAsync(request.Category, request.Environment, cancellationToken);
        return new GetPlatformEventsResponse(
            events.Select(item => new PlatformEventItemResponse(
                item.EventId,
                item.Category,
                item.EventType,
                item.PlatformEnvironment.ToString(),
                item.BrokerEnvironment.ToString(),
                item.Summary,
                item.Details,
                item.OccurredAtUtc)).ToArray());
    }
}
