using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfAccountDetailsSnapshotStore(PlatformDbContext dbContext) : IAccountDetailsSnapshotStore
{
    public Task<AccountDetailsSnapshot?> GetLatestAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) =>
        ReadLatestAsync(environment, cancellationToken);

    public Task<AccountDetailsSnapshot?> GetBeforeAsync(BrokerEnvironmentKind environment, AccountDetailsCursor cursor, CancellationToken cancellationToken) =>
        ReadAdjacentAsync(environment, cursor, before: true, cancellationToken);

    public Task<AccountDetailsSnapshot?> GetAfterAsync(BrokerEnvironmentKind environment, AccountDetailsCursor cursor, CancellationToken cancellationToken) =>
        ReadAdjacentAsync(environment, cursor, before: false, cancellationToken);

    public Task<bool> ExistsForTradingDayAsync(BrokerEnvironmentKind environment, DateOnly tradingDay, CancellationToken cancellationToken) =>
        dbContext.AccountDetailsRetrievals.AnyAsync(item => item.BrokerEnvironment == environment.ToString() && item.TradingDay == tradingDay && item.TriggerSource == AccountDetailsTriggerSource.Automatic.ToString(), cancellationToken);

    public async Task<AccountDetailsSnapshot> SaveAsync(AccountDetailsSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var entity = new AccountDetailsRetrievalEntity
        {
            AccountDetailsRetrievalId = snapshot.RetrievalId, BrokerEnvironment = snapshot.BrokerEnvironment.ToString(), RetrievedAtUtc = snapshot.RetrievedAtUtc,
            TradingDay = snapshot.TradingDay, AccountCount = snapshot.Accounts.Count, TriggerSource = snapshot.TriggerSource.ToString(),
            Accounts = snapshot.Accounts.Select(account => new AccountDetailsAccountEntity
            {
                AccountDetailsAccountId = Guid.NewGuid(), AccountDetailsRetrievalId = snapshot.RetrievalId, AccountId = account.AccountId, AccountName = account.AccountName,
                AccountAlias = account.AccountAlias, Status = account.Status, AccountType = account.AccountType, IsPreferred = account.IsPreferred, Balance = account.Balance,
                Deposit = account.Deposit, ProfitLoss = account.ProfitLoss, Available = account.Available, Currency = account.Currency,
                CanTransferFrom = account.CanTransferFrom, CanTransferTo = account.CanTransferTo
            }).ToList()
        };
        dbContext.AccountDetailsRetrievals.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    private IQueryable<AccountDetailsRetrievalEntity> Query(BrokerEnvironmentKind environment) => dbContext.AccountDetailsRetrievals.Include(item => item.Accounts).Where(item => item.BrokerEnvironment == environment.ToString());

    private async Task<AccountDetailsSnapshot?> ReadLatestAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) =>
        ToSnapshot(await Query(environment).OrderByDescending(item => item.RetrievedAtUtc).ThenByDescending(item => item.AccountDetailsRetrievalId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));

    private async Task<AccountDetailsSnapshot?> ReadAdjacentAsync(BrokerEnvironmentKind environment, AccountDetailsCursor cursor, bool before, CancellationToken cancellationToken)
    {
        var query = Query(environment).Where(item => before
            ? item.RetrievedAtUtc < cursor.RetrievedAtUtc || item.RetrievedAtUtc == cursor.RetrievedAtUtc && item.AccountDetailsRetrievalId.CompareTo(cursor.RetrievalId) < 0
            : item.RetrievedAtUtc > cursor.RetrievedAtUtc || item.RetrievedAtUtc == cursor.RetrievedAtUtc && item.AccountDetailsRetrievalId.CompareTo(cursor.RetrievalId) > 0);
        var result = before
            ? await query.OrderByDescending(item => item.RetrievedAtUtc).ThenByDescending(item => item.AccountDetailsRetrievalId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : await query.OrderBy(item => item.RetrievedAtUtc).ThenBy(item => item.AccountDetailsRetrievalId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(result);
    }

    private static AccountDetailsSnapshot? ToSnapshot(AccountDetailsRetrievalEntity? item) => item is null ? null : new AccountDetailsSnapshot(
        item.AccountDetailsRetrievalId, Enum.Parse<BrokerEnvironmentKind>(item.BrokerEnvironment), item.RetrievedAtUtc, item.TradingDay,
        Enum.Parse<AccountDetailsTriggerSource>(item.TriggerSource), item.Accounts.OrderBy(account => account.AccountId).Select(account => new AccountDetailsAccount(
            account.AccountId, account.AccountName, account.AccountAlias, account.Status, account.AccountType, account.IsPreferred, account.Balance,
            account.Deposit, account.ProfitLoss, account.Available, account.Currency, account.CanTransferFrom, account.CanTransferTo)).ToList());
}