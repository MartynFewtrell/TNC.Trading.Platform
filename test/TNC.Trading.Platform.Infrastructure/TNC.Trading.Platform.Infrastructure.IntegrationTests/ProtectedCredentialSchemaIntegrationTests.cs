using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.BrokerEnvironments;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class ProtectedCredentialSchemaIntegrationTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>
    /// Trace: broker environment credential replacement.
    /// Verifies: migrations expand the persisted credential scope to hold a catalog environment GUID.
    /// Expected: a fresh SQL Server schema exposes a 64-character BrokerEnvironment column.
    /// Why: catalog-scoped credential writes use canonical GUID text, which exceeded the previous 32-character schema limit and caused all replacements to fail.
    /// </summary>
    [Fact]
    public async Task MigrateAsync_ShouldExpandCredentialEnvironmentScope_WhenCatalogCredentialsUseGuidIdentifiers()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();

        await dbContext.Database.MigrateAsync(fixture.CancellationToken);
        var maximumLength = await dbContext.Database.SqlQueryRaw<int>(
            """
            SELECT CHARACTER_MAXIMUM_LENGTH AS [Value]
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo'
              AND TABLE_NAME = 'ProtectedCredentials'
              AND COLUMN_NAME = 'BrokerEnvironment'
            """)
            .SingleAsync(fixture.CancellationToken);

        Assert.Equal(64, maximumLength);
    }

    /// <summary>
    /// Trace: broker environment credential replacement.
    /// Verifies: the catalog credential update persists the broker-environment foreign key alongside its GUID text scope.
    /// Expected: all protected credential rows reference the seeded catalog record and the SQL foreign-key constraint accepts the save.
    /// Why: leaving BrokerEnvironmentId at Guid.Empty caused every replacement request to fail after the string-length defect was corrected.
    /// </summary>
    [Fact]
    public async Task SaveCredentialsAsync_ShouldPersistCatalogForeignKey_WhenReplacingCredentials()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(dbContext, fixture.CancellationToken);
        var dataProtectionProvider = DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        var service = new SqlBrokerEnvironmentCatalogService(
            dbContext,
            new ProtectedCredentialService(dbContext, dataProtectionProvider, TimeProvider.System),
            new TestPlatformEnvironmentContext(),
            TimeProvider.System);

        var result = await service.SaveCredentialsAsync(
            new SaveBrokerEnvironmentCredentialsCommand(
                environmentId,
                "replacement-key",
                "operator@example.test",
                "replacement-password",
                "integration-test"),
            fixture.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(
            3,
            await dbContext.ProtectedCredentials.CountAsync(
                item => item.BrokerEnvironmentId == environmentId,
                fixture.CancellationToken));
    }

    private sealed class TestPlatformEnvironmentContext : IPlatformEnvironmentContext
    {
        public PlatformEnvironmentKind Environment => PlatformEnvironmentKind.Test;
    }
}
