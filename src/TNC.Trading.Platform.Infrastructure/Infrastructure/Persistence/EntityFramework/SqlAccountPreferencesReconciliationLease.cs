using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class SqlAccountPreferencesReconciliationLease(PlatformDbContext dbContext) : IAccountPreferencesReconciliationLease
{
    public async Task<IAsyncDisposable?> AcquireAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string accountId, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var resource = $"TNC.Trading.Platform.AccountPreferences.{platformEnvironment}.{brokerEnvironment}.{accountId}";
        try { await using var command = connection.CreateCommand(); command.CommandText = "DECLARE @result int; EXEC @result = sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @result;"; command.Parameters.Add(new SqlParameter("@resource", resource)); var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)); if (result < 0) { await connection.CloseAsync().ConfigureAwait(false); return null; } return new Handle(connection, resource); } catch { await connection.CloseAsync().ConfigureAwait(false); throw; }
    }
    private sealed class Handle(DbConnection connection, string resource) : IAsyncDisposable
    { public async ValueTask DisposeAsync() { try { await using var command = connection.CreateCommand(); command.CommandText = "EXEC sp_releaseapplock @Resource=@resource, @LockOwner='Session';"; command.Parameters.Add(new SqlParameter("@resource", resource)); await command.ExecuteNonQueryAsync().ConfigureAwait(false); } finally { await connection.CloseAsync().ConfigureAwait(false); } } }
}