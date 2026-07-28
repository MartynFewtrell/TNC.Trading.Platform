using TNC.Trading.Platform.Application.Features.GetPlatformEvents.Ports;

namespace TNC.Trading.Platform.Application.Features.GetPlatformEvents;

internal sealed class GetPlatformEventsHandler(IPlatformEventsProjectionReader projectionReader)
{
    public async Task<GetPlatformEventsResponse> HandleAsync(GetPlatformEventsRequest request, CancellationToken cancellationToken)
    {
        var events = await projectionReader.ReadAsync(request.Category, request.Environment, cancellationToken).ConfigureAwait(false);
        return new GetPlatformEventsResponse(events);
    }
}
