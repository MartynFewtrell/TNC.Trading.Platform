using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal interface IAccountDetailsRefreshLease
{
    Task<AccountDetailsRefreshLeaseResult> AcquireAsync(BrokerEnvironmentKind environment, AccountDetailsTriggerSource trigger, CancellationToken cancellationToken);
}

internal sealed record AccountDetailsRefreshLeaseResult(bool Acquired, DateTimeOffset? LatestRetrievedAtUtc = null, IAsyncDisposable? Handle = null) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Handle?.DisposeAsync() ?? ValueTask.CompletedTask;
}
