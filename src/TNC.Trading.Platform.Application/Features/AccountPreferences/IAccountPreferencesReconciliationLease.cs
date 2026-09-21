using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal interface IAccountPreferencesReconciliationLease
{
    Task<IAsyncDisposable?> AcquireAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string accountId, CancellationToken cancellationToken);
}