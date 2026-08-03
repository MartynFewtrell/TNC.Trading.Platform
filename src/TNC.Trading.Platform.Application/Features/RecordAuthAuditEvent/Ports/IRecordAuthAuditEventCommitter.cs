namespace TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent.Ports;

internal interface IRecordAuthAuditEventCommitter
{
    Task CommitAsync(RecordAuthAuditEventIntent intent, CancellationToken cancellationToken);
}