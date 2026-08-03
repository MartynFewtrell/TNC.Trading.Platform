using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.GetIgLoginHistory.Ports;

internal interface IGetIgLoginHistoryReader
{
    Task<IReadOnlyList<IgLoginSnapshot>> ReadAsync(
        BrokerEnvironmentKind brokerEnvironment,
        CancellationToken cancellationToken);
}