using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal interface IAccountDetailsSnapshotStore
{
    Task<AccountDetailsSnapshot?> GetLatestAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken);

    Task<AccountDetailsSnapshot?> GetBeforeAsync(BrokerEnvironmentKind environment, AccountDetailsCursor cursor, CancellationToken cancellationToken);

    Task<AccountDetailsSnapshot?> GetAfterAsync(BrokerEnvironmentKind environment, AccountDetailsCursor cursor, CancellationToken cancellationToken);

    Task<bool> ExistsForTradingDayAsync(BrokerEnvironmentKind environment, DateOnly tradingDay, CancellationToken cancellationToken);

    Task<AccountDetailsSnapshot> SaveAsync(AccountDetailsSnapshot snapshot, CancellationToken cancellationToken);
}
