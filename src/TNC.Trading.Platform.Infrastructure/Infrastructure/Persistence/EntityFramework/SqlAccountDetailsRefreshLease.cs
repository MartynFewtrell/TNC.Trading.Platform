using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountDetails;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class SqlAccountDetailsRefreshLease(PlatformDbContext dbContext) : IAccountDetailsRefreshLease
{
    public async Task<AccountDetailsRefreshLeaseResult> AcquireAsync(BrokerEnvironmentKind environment, AccountDetailsTriggerSource trigger, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
                SELECT @result;
                """;
            command.Parameters.Add(new SqlParameter("@resource", $"TNC.Trading.Platform.AccountDetailsRefresh.{environment}"));
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (result < 0)
            {
                await connection.CloseAsync().ConfigureAwait(false);
                var latest = trigger == AccountDetailsTriggerSource.Manual
                    ? await dbContext.AccountDetailsRetrievals.Where(item => item.BrokerEnvironment == environment.ToString()).MaxAsync(item => (DateTimeOffset?)item.RetrievedAtUtc, cancellationToken).ConfigureAwait(false)
                    : null;
                return new AccountDetailsRefreshLeaseResult(false, latest);
            }

            return new AccountDetailsRefreshLeaseResult(true, null, new SqlAccountDetailsRefreshLeaseHandle(connection, environment));
        }
        catch
        {
            await connection.CloseAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class SqlAccountDetailsRefreshLeaseHandle(DbConnection connection, BrokerEnvironmentKind environment) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
                command.Parameters.Add(new SqlParameter("@resource", $"TNC.Trading.Platform.AccountDetailsRefresh.{environment}"));
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}