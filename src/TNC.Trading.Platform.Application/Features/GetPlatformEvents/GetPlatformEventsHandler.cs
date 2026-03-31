using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.GetPlatformEvents;

internal sealed class GetPlatformEventsHandler(PlatformStateCoordinator coordinator)
{
    public async Task<GetPlatformEventsResponse> HandleAsync(GetPlatformEventsRequest request, CancellationToken cancellationToken)
    {
        var events = await coordinator.GetEventsAsync(request.Category, request.Environment, cancellationToken).ConfigureAwait(false);
        return new GetPlatformEventsResponse(events);
    }
}
