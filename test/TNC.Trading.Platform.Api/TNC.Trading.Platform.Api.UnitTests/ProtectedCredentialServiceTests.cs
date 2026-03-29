using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace TNC.Trading.Platform.Api.UnitTests;

public class ProtectedCredentialServiceTests
{
    [Fact]
    public async Task UpdateAsync_StoresProtectedValuesPerBrokerEnvironment()
    {
        using var dbContext = ApiReflection.CreateDbContext();
        var service = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Infrastructure.Platform.ProtectedCredentialService",
            dbContext,
            ApiReflection.CreateDataProtectionProvider(),
            TimeProvider.System);

        var demoEnvironment = ApiReflection.ParseEnum("TNC.Trading.Platform.Api.Configuration.BrokerEnvironmentKind", "Demo");
        var liveEnvironment = ApiReflection.ParseEnum("TNC.Trading.Platform.Api.Configuration.BrokerEnvironmentKind", "Live");

        _ = await ApiReflection.InvokeAsync(service, "UpdateAsync", demoEnvironment, "demo-api-key", "demo-identifier", "demo-password", "unit-test", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var demoPresence = await ApiReflection.InvokeAsync(service, "GetPresenceAsync", demoEnvironment, CancellationToken.None);
        var livePresence = await ApiReflection.InvokeAsync(service, "GetPresenceAsync", liveEnvironment, CancellationToken.None);

        Assert.True(ApiReflection.GetProperty<bool>(demoPresence!, "HasApiKey"));
        Assert.True(ApiReflection.GetProperty<bool>(demoPresence!, "HasIdentifier"));
        Assert.True(ApiReflection.GetProperty<bool>(demoPresence!, "HasPassword"));
        Assert.False(ApiReflection.GetProperty<bool>(livePresence!, "HasApiKey"));

        var credentials = ((IEnumerable<object>)dbContext.GetType().GetProperty("ProtectedCredentials", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();

        Assert.Equal(3, credentials.Length);
        Assert.All(credentials, credential => Assert.Equal("Demo", ApiReflection.GetProperty<string>(credential, "BrokerEnvironment")));
        Assert.DoesNotContain(credentials, credential => ApiReflection.GetProperty<string>(credential, "ProtectedValue") == "demo-api-key");
    }
}
