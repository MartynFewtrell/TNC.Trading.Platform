using TNC.Trading.Platform.Application.Features.GetIgLoginHistory.Ports;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.GetIgLoginHistory;

internal sealed class GetIgLoginHistoryHandler(
    PlatformConfigurationService platformConfigurationService,
    IGetIgLoginHistoryReader historyReader)
{
    public async Task<GetIgLoginHistoryResponse> HandleAsync(GetIgLoginHistoryRequest request, CancellationToken cancellationToken)
    {
        var configuration = await platformConfigurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = await historyReader
            .ReadAsync(configuration.BrokerEnvironment, cancellationToken)
            .ConfigureAwait(false);

        return new GetIgLoginHistoryResponse(snapshots);
    }
}
