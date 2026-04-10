using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformEventStore
{
    Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken);

    Task AddAsync(PlatformEventRecord record, CancellationToken cancellationToken);
}
