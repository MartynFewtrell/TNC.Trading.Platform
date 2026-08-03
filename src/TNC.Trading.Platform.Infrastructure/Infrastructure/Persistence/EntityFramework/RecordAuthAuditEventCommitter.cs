using TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent;
using TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent.Ports;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class RecordAuthAuditEventCommitter(IPlatformEventStore eventStore) : IRecordAuthAuditEventCommitter
{
    public Task CommitAsync(RecordAuthAuditEventIntent intent, CancellationToken cancellationToken) =>
        eventStore.AddAsync(intent.Event, cancellationToken);
}