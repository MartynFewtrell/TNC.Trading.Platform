using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.BrokerEnvironments;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class SqlBrokerEnvironmentCatalogServiceTests
{
    /// <summary>
    /// Trace: broker environment credential replacement.
    /// Verifies: catalog-scoped credentials can persist the canonical string representation of a broker-environment GUID.
    /// Expected: the mapped database column permits at least 36 characters.
    /// Why: the prior 32-character column truncated the catalog ID and made every credential replacement fail at SaveChanges.
    /// </summary>
    [Fact]
    public void ProtectedCredentialBrokerEnvironment_ShouldAllowGuidScope_WhenCatalogCredentialsAreStored()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();

        var property = dbContext.Model
            .FindEntityType(typeof(ProtectedCredentialEntity))!
            .FindProperty(nameof(ProtectedCredentialEntity.BrokerEnvironment))!;

        Assert.True(property.GetMaxLength() >= Guid.Empty.ToString("D").Length);
    }

    /// <summary>
    /// Trace: broker environment credential replacement.
    /// Verifies: the canonical catalog spelling of "Ig" is accepted for an active, available Demo environment and the refreshed catalog reports its persisted credentials.
    /// Expected: the credential update succeeds, persists each supplied value as protected catalog-scoped material, and returns the environment with credentials present on a later list read.
    /// Why: the seeded IG Demo record uses title case, and the catalog previously reported missing credentials after a successful replacement.
    /// </summary>
    [Fact]
    public async Task ListAsync_ShouldReportCredentialsPresent_WhenCredentialsWerePersistedForCatalogEnvironment()
    {
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        var environmentId = Guid.NewGuid();
        dbContext.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = environmentId,
            Name = "IG Demo",
            NormalizedName = "IG DEMO",
            Provider = "Ig",
            Kind = "Demo",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "IgDemo",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var credentialService = new ProtectedCredentialService(
            dbContext,
            InfrastructureReflection.CreateDataProtectionProvider(),
            TimeProvider.System);
        var service = new SqlBrokerEnvironmentCatalogService(
            dbContext,
            credentialService,
            new TestPlatformEnvironmentContext(),
            TimeProvider.System);

        var result = await service.SaveCredentialsAsync(
            new SaveBrokerEnvironmentCredentialsCommand(
                environmentId,
                "replacement-key",
                "operator@example.test",
                "replacement-password",
                "unit-test"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Item?.CanAuthenticate);
        Assert.True(result.Item?.HasCredentials);
        Assert.Equal(3, dbContext.ProtectedCredentials.Count(item => item.BrokerEnvironment == environmentId.ToString("D")));

        foreach (var credential in dbContext.ProtectedCredentials)
        {
            credential.BrokerEnvironment = "Demo";
        }
        await dbContext.SaveChangesAsync();

        var catalog = await service.ListAsync(CancellationToken.None);

        Assert.True(Assert.Single(catalog).HasCredentials);
    }

    private sealed class TestPlatformEnvironmentContext : IPlatformEnvironmentContext
    {
        public PlatformEnvironmentKind Environment => PlatformEnvironmentKind.Test;
    }
}
