using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.GetPlatformEvents.Ports;

internal interface IPlatformEventsProjectionReader
{
    Task<IReadOnlyList<OperationalEventModel>> ReadAsync(
        string? category,
        string? environment,
        CancellationToken cancellationToken);
}