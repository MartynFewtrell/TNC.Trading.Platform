namespace TNC.Trading.Platform.Api.Features.GetPlatformEvents;

internal sealed record GetPlatformEventsResponse(
    IReadOnlyList<PlatformEventItemResponse> Events);
