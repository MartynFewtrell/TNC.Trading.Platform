using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class ProtectedCredentialServiceTests
{
    /// <summary>
    /// Trace: SR2, SR3, TR3, TR12.
    /// Verifies: saved credentials are protected at rest and scoped to the selected broker environment.
    /// Expected: Demo credential presence is populated, Live remains empty, and stored protected values do not match raw secrets.
    /// Why: environment-specific credential isolation is required to avoid plaintext storage and cross-environment secret bleed.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldStoreProtectedValuesPerBrokerEnvironment_WhenCredentialsAreSaved()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var service = new ProtectedCredentialService(
            dbContext,
            InfrastructureReflection.CreateDataProtectionProvider(),
            TimeProvider.System);

        await service.UpdateAsync(BrokerEnvironmentKind.Demo, "demo-api-key", "demo-identifier", "demo-password", "unit-test", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var demoPresence = await service.GetPresenceAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        var livePresence = await service.GetPresenceAsync(BrokerEnvironmentKind.Live, CancellationToken.None);

        Assert.True(demoPresence.HasApiKey);
        Assert.True(demoPresence.HasIdentifier);
        Assert.True(demoPresence.HasPassword);
        Assert.False(livePresence.HasApiKey);

        var credentials = dbContext.ProtectedCredentials.ToArray();

        Assert.Equal(3, credentials.Length);
        Assert.All(credentials, credential => Assert.Equal("Demo", credential.BrokerEnvironment));
        Assert.DoesNotContain(credentials, credential => credential.ProtectedValue == "demo-api-key");
    }

    /// <summary>
    /// Trace: SR3, TR3, TR12.
    /// Verifies: credential rotation reuses the existing rows while reprotecting the stored secret values.
    /// Expected: the credential row count remains stable, protected values change, and updated rows still exclude raw secrets.
    /// Why: secure rotation must preserve operational continuity and historical reviewability without duplicating secret records.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldReuseRowsAndReprotectStoredValues_WhenCredentialsRotate()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var service = new ProtectedCredentialService(
            dbContext,
            InfrastructureReflection.CreateDataProtectionProvider(),
            TimeProvider.System);

        await service.UpdateAsync(BrokerEnvironmentKind.Demo, "initial-api-key", "initial-identifier", "initial-password", "initial-user", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var originalCredentials = dbContext.ProtectedCredentials.ToArray();
        var originalApiKeyProtectedValue = Assert.Single(originalCredentials.Where(item => item.CredentialType == "ApiKey")).ProtectedValue;

        await service.UpdateAsync(BrokerEnvironmentKind.Demo, "rotated-api-key", "rotated-identifier", "rotated-password", "rotation-user", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var rotatedCredentials = dbContext.ProtectedCredentials.ToArray();
        var rotatedApiKey = Assert.Single(rotatedCredentials.Where(item => item.CredentialType == "ApiKey"));

        Assert.Equal(3, rotatedCredentials.Length);
        Assert.Equal("rotation-user", rotatedApiKey.UpdatedBy);
        Assert.NotEqual(originalApiKeyProtectedValue, rotatedApiKey.ProtectedValue);
        Assert.DoesNotContain(rotatedCredentials, credential => credential.ProtectedValue == "rotated-api-key");
        Assert.DoesNotContain(rotatedCredentials, credential => credential.ProtectedValue == "rotated-identifier");
        Assert.DoesNotContain(rotatedCredentials, credential => credential.ProtectedValue == "rotated-password");
    }

    /// <summary>
    /// Trace: SR3, NF3.
    /// Verifies: the runtime credential read path decrypts all stored values for the selected broker environment.
    /// Expected: the returned credentials contain the original API key, identifier, and password values.
    /// Why: broker authentication must be able to use the protected credentials at runtime without exposing them in persisted outputs.
    /// </summary>
    [Fact]
    public async Task GetCredentialsAsync_WhenAllCredentialsPresent_ShouldReturnDecryptedValues()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var service = new ProtectedCredentialService(
            dbContext,
            InfrastructureReflection.CreateDataProtectionProvider(),
            TimeProvider.System);

        await service.UpdateAsync(BrokerEnvironmentKind.Demo, "demo-api-key", "demo-identifier", "demo-password", "unit-test", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var credentials = await service.GetCredentialsAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);

        Assert.Equal("demo-api-key", credentials.ApiKey);
        Assert.Equal("demo-identifier", credentials.Identifier);
        Assert.Equal("demo-password", credentials.Password);
    }

    /// <summary>
    /// Trace: SR3, NF3.
    /// Verifies: missing protected credential rows are mapped to empty strings instead of leaking nulls or throwing.
    /// Expected: any missing credential type returns an empty string while present values are still decrypted correctly.
    /// Why: runtime auth calls need a deterministic secret-safe contract even when an operator has only partially configured credentials.
    /// </summary>
    [Fact]
    public async Task GetCredentialsAsync_WhenCredentialMissing_ShouldReturnEmptyStringForMissingValue()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var service = new ProtectedCredentialService(
            dbContext,
            InfrastructureReflection.CreateDataProtectionProvider(),
            TimeProvider.System);

        await service.UpdateAsync(BrokerEnvironmentKind.Demo, "demo-api-key", null, "demo-password", "unit-test", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var credentials = await service.GetCredentialsAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);

        Assert.Equal("demo-api-key", credentials.ApiKey);
        Assert.Equal(string.Empty, credentials.Identifier);
        Assert.Equal("demo-password", credentials.Password);
    }
}
