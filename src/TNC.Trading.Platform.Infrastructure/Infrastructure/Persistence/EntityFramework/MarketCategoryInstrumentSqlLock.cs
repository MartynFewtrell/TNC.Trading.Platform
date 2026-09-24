using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal static class MarketCategoryInstrumentSqlLock
{
    internal static async Task AcquireAsync(
        PlatformDbContext dbContext,
        string resource,
        string mode,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        if (mode is not ("Shared" or "Exclusive") || string.IsNullOrWhiteSpace(resource) || resource.Length > 255)
        {
            throw new ArgumentException("A valid SQL application lock request is required.");
        }

        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("A SQL application lock must be acquired within a database transaction.");
        command.CommandText = """
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = @mode,
                @LockOwner = 'Transaction',
                @LockTimeout = 30000;
            SELECT @lockResult;
            """;
        var resourceParameter = command.CreateParameter();
        resourceParameter.ParameterName = "@resource";
        resourceParameter.DbType = DbType.String;
        resourceParameter.Size = 255;
        resourceParameter.Value = resource;
        command.Parameters.Add(resourceParameter);
        var modeParameter = command.CreateParameter();
        modeParameter.ParameterName = "@mode";
        modeParameter.DbType = DbType.String;
        modeParameter.Size = 16;
        modeParameter.Value = mode;
        command.Parameters.Add(modeParameter);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new TimeoutException($"Could not acquire the SQL application lock for resource '{resource}'.");
        }
    }
}
