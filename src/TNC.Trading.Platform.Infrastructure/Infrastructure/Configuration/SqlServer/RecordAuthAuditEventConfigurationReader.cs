using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent.Ports;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;

internal sealed class RecordAuthAuditEventConfigurationReader(PlatformConfigurationService configurationService)
    : IRecordAuthAuditEventConfigurationReader
{
    public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) =>
        configurationService.GetCurrentAsync(cancellationToken);
}