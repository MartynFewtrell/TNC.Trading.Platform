using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class ProtectedCredentialServiceTests
{
    [Fact]
    public async Task UpdateAsync_StoresProtectedValuesPerBrokerEnvironment()
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
}
