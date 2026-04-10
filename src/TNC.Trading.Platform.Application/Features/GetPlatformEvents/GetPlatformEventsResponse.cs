using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.GetPlatformEvents;

internal sealed record GetPlatformEventsResponse(IReadOnlyList<OperationalEventModel> Events);
