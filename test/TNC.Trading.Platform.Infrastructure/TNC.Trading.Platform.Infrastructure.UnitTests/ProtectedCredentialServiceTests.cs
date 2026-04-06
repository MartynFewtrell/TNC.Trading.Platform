using System.Reflection;
using Microsoft.EntityFrameworkCore;

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
        var service = InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Platform.ProtectedCredentialService",
            dbContext,
            InfrastructureReflection.CreateDataProtectionProvider(),
            TimeProvider.System);

        var demoEnvironment = InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", "Demo");
        var liveEnvironment = InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", "Live");

        _ = await InfrastructureReflection.InvokeAsync(service, "UpdateAsync", demoEnvironment, "demo-api-key", "demo-identifier", "demo-password", "unit-test", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var demoPresence = await InfrastructureReflection.InvokeAsync(service, "GetPresenceAsync", demoEnvironment, CancellationToken.None);
        var livePresence = await InfrastructureReflection.InvokeAsync(service, "GetPresenceAsync", liveEnvironment, CancellationToken.None);

        Assert.True(InfrastructureReflection.GetProperty<bool>(demoPresence!, "HasApiKey"));
        Assert.True(InfrastructureReflection.GetProperty<bool>(demoPresence!, "HasIdentifier"));
        Assert.True(InfrastructureReflection.GetProperty<bool>(demoPresence!, "HasPassword"));
        Assert.False(InfrastructureReflection.GetProperty<bool>(livePresence!, "HasApiKey"));

        var credentials = ((IEnumerable<object>)dbContext.GetType().GetProperty("ProtectedCredentials", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();

        Assert.Equal(3, credentials.Length);
        Assert.All(credentials, credential => Assert.Equal("Demo", InfrastructureReflection.GetProperty<string>(credential, "BrokerEnvironment")));
        Assert.DoesNotContain(credentials, credential => InfrastructureReflection.GetProperty<string>(credential, "ProtectedValue") == "demo-api-key");
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
        var service = InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Platform.ProtectedCredentialService",
            dbContext,
            InfrastructureReflection.CreateDataProtectionProvider(),
            TimeProvider.System);

        var demoEnvironment = InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", "Demo");

        _ = await InfrastructureReflection.InvokeAsync(service, "UpdateAsync", demoEnvironment, "initial-api-key", "initial-identifier", "initial-password", "initial-user", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var originalCredentials = ((IEnumerable<object>)dbContext.GetType().GetProperty("ProtectedCredentials", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();
        var originalApiKeyProtectedValue = InfrastructureReflection.GetProperty<string>(
            Assert.Single(originalCredentials.Where(item => InfrastructureReflection.GetProperty<string>(item, "CredentialType") == "ApiKey")),
            "ProtectedValue");

        _ = await InfrastructureReflection.InvokeAsync(service, "UpdateAsync", demoEnvironment, "rotated-api-key", "rotated-identifier", "rotated-password", "rotation-user", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var rotatedCredentials = ((IEnumerable<object>)dbContext.GetType().GetProperty("ProtectedCredentials", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();
        var rotatedApiKey = Assert.Single(rotatedCredentials.Where(item => InfrastructureReflection.GetProperty<string>(item, "CredentialType") == "ApiKey"));

        Assert.Equal(3, rotatedCredentials.Length);
        Assert.Equal("rotation-user", InfrastructureReflection.GetProperty<string>(rotatedApiKey, "UpdatedBy"));
        Assert.NotEqual(originalApiKeyProtectedValue, InfrastructureReflection.GetProperty<string>(rotatedApiKey, "ProtectedValue"));
        Assert.DoesNotContain(rotatedCredentials, credential => InfrastructureReflection.GetProperty<string>(credential, "ProtectedValue") == "rotated-api-key");
        Assert.DoesNotContain(rotatedCredentials, credential => InfrastructureReflection.GetProperty<string>(credential, "ProtectedValue") == "rotated-identifier");
        Assert.DoesNotContain(rotatedCredentials, credential => InfrastructureReflection.GetProperty<string>(credential, "ProtectedValue") == "rotated-password");
    }
}
