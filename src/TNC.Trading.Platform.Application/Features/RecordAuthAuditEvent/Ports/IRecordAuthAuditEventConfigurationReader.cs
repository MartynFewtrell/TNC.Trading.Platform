using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent.Ports;

internal interface IRecordAuthAuditEventConfigurationReader
{
    Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken);
}