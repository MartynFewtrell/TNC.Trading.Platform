namespace TNC.Trading.Platform.Api.Features.GetPlatformEvents;

internal sealed record GetPlatformEventsRequest(
    string? Category,
    string? Environment);
