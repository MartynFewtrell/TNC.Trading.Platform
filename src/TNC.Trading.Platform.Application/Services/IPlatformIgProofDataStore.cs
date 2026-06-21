using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformIgProofDataStore
{
    Task<IgProofDataSnapshot?> GetLatestAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken);
    Task SaveAsync(BrokerEnvironmentKind brokerEnvironment, IgProofDataSnapshot snapshot, CancellationToken cancellationToken);
}
