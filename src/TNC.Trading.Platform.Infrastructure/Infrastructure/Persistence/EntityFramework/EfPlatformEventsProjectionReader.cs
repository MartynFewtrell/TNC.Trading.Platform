using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.GetPlatformEvents.Ports;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfPlatformEventsProjectionReader(IPlatformEventStore eventStore) : IPlatformEventsProjectionReader
{
    public Task<IReadOnlyList<OperationalEventModel>> ReadAsync(
        string? category,
        string? environment,
        CancellationToken cancellationToken) =>
        eventStore.GetEventsAsync(category, environment, cancellationToken);
}