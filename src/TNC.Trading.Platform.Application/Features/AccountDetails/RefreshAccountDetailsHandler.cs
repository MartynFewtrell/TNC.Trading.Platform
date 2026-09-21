using System.Collections.Concurrent;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal sealed class RefreshAccountDetailsHandler(
    PlatformConfigurationService configurationService,
    IAccountDetailsGateway gateway,
    IAccountDetailsSnapshotStore snapshotStore,
    IAccountDetailsRefreshLease lease,
    TradingScheduleGate tradingScheduleGate,
    TimeProvider timeProvider)
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<AccountDetailsRefreshOutcome>>> InFlight = new(StringComparer.Ordinal);

    public Task<RefreshAccountDetailsResponse> HandleAsync(RefreshAccountDetailsRequest request, CancellationToken cancellationToken) =>
        HandleCoreAsync(cancellationToken);

    internal async Task<AccountDetailsRefreshOutcome> CaptureAutomaticAsync(CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var tradingDay = tradingScheduleGate.GetTradingDay(configuration.TradingSchedule, timeProvider.GetUtcNow());
        if (await snapshotStore.ExistsForTradingDayAsync(configuration.BrokerEnvironment, tradingDay, cancellationToken).ConfigureAwait(false))
        {
            return new AccountDetailsRefreshOutcome.Deferred();
        }

        var acquired = await lease.AcquireAsync(configuration.BrokerEnvironment, AccountDetailsTriggerSource.Automatic, cancellationToken).ConfigureAwait(false);
        await using (acquired.ConfigureAwait(false))
        {
            if (!acquired.Acquired)
            {
                return new AccountDetailsRefreshOutcome.Deferred();
            }

            if (await snapshotStore.ExistsForTradingDayAsync(configuration.BrokerEnvironment, tradingDay, cancellationToken).ConfigureAwait(false))
            {
                return new AccountDetailsRefreshOutcome.Deferred();
            }

            return await SaveSnapshotAsync(configuration.BrokerEnvironment, tradingDay, AccountDetailsTriggerSource.Automatic, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<RefreshAccountDetailsResponse> HandleCoreAsync(CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var key = configuration.BrokerEnvironment.ToString();
        var lazy = InFlight.GetOrAdd(key, _ => new Lazy<Task<AccountDetailsRefreshOutcome>>(
            () => RefreshOnceAsync(configuration.BrokerEnvironment, cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return new RefreshAccountDetailsResponse(await lazy.Value.ConfigureAwait(false));
        }
        finally
        {
            InFlight.TryRemove(new KeyValuePair<string, Lazy<Task<AccountDetailsRefreshOutcome>>>(key, lazy));
        }
    }

    private async Task<AccountDetailsRefreshOutcome> RefreshOnceAsync(
        TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind environment,
        CancellationToken cancellationToken)
    {
        var acquired = await lease.AcquireAsync(environment, AccountDetailsTriggerSource.Manual, cancellationToken).ConfigureAwait(false);
        await using (acquired.ConfigureAwait(false))
        {
            if (!acquired.Acquired)
            {
                return new AccountDetailsRefreshOutcome.RefreshInProgress(acquired.LatestRetrievedAtUtc);
            }

            return await SaveSnapshotAsync(environment, tradingScheduleGate.GetTradingDay(
                (await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false)).TradingSchedule,
                timeProvider.GetUtcNow()), AccountDetailsTriggerSource.Manual, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<AccountDetailsRefreshOutcome> SaveSnapshotAsync(
        TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        AccountDetailsTriggerSource trigger,
        CancellationToken cancellationToken)
    {
        var result = await gateway.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
        if (result is not AccountDetailsGatewayResult.Succeeded succeeded || succeeded.Accounts.Count == 0)
        {
            return result is AccountDetailsGatewayResult.Failed failed
                ? new AccountDetailsRefreshOutcome.Failed(failed.Category, failed.Summary)
                : new AccountDetailsRefreshOutcome.Failed(AccountDetailsFailureCategory.MalformedProviderData, "The provider returned no accounts.");
        }

        var snapshot = new AccountDetailsSnapshot(Guid.NewGuid(), environment, timeProvider.GetUtcNow(), tradingDay, trigger, succeeded.Accounts);
        return new AccountDetailsRefreshOutcome.Saved(await snapshotStore.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false));
    }
}
