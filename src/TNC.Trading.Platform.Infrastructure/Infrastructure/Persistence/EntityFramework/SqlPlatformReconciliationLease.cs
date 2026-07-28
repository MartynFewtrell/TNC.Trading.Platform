using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class SqlPlatformReconciliationLease(PlatformDbContext dbContext) : IPlatformReconciliationLease
{
    private const string ResourceName = "TNC.Trading.Platform.Reconciliation";

    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Session',
                    @LockTimeout = 0;
                SELECT @result;
                """;
            command.Parameters.Add(new SqlParameter("@resource", ResourceName));

            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (result < 0)
            {
                throw new InvalidOperationException(
                    "Platform reconciliation is already owned by another replica. The current tick will retry on its next scheduled attempt.");
            }

            return new SqlPlatformReconciliationLeaseHandle(connection);
        }
        catch
        {
            await connection.CloseAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class SqlPlatformReconciliationLeaseHandle(DbConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
                command.Parameters.Add(new SqlParameter("@resource", ResourceName));
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}
