using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TNC.Trading.Platform.AppHost.UnitTests;

public sealed class DataProtectionRegistrationTests
{
    /// <summary>
    /// Verifies: the shared registration uses one application discriminator and the documented 90-day default rotation lifetime.
    /// Expected: both options are present before any key-ring I/O occurs.
    /// Why: API and Web must derive compatible protected payloads while retaining explicit, reviewable rotation semantics.
    /// </summary>
    [Fact]
    public void AddPlatformDataProtection_ShouldUseSharedApplicationAndDefaultLifetime_WhenNoLifetimeIsConfigured()
    {
        var builder = CreateBuilder();

        builder.AddPlatformDataProtection();

        using var services = builder.Services.BuildServiceProvider();
        var dataProtectionOptions = services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;
        var keyManagementOptions = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        Assert.Equal("TNC.Trading.Platform", dataProtectionOptions.ApplicationDiscriminator);
        Assert.Equal(TimeSpan.FromDays(90), keyManagementOptions.NewKeyLifetime);
    }

    /// <summary>
    /// Verifies: configured key rotation lifetimes must be positive.
    /// Expected: invalid configuration fails during composition rather than silently disabling rotation.
    /// Why: an accidental zero or negative lifetime would undermine the documented key-retention contract.
    /// </summary>
    [Fact]
    public void AddPlatformDataProtection_ShouldRejectNonPositiveLifetime_WhenRotationConfigurationIsInvalid()
    {
        var builder = CreateBuilder();
        builder.Configuration["DataProtection:KeyLifetimeDays"] = "0";

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddPlatformDataProtection());

        Assert.Contains("KeyLifetimeDays", exception.Message, StringComparison.Ordinal);
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:platformdb"] = "Server=(localdb)\\mssqllocaldb;Database=Phase10;Trusted_Connection=True;";
        return builder;
    }
}