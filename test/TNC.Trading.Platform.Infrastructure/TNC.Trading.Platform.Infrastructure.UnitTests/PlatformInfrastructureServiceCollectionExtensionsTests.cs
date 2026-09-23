using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.BrokerEnvironments;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.DependencyInjection;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class PlatformInfrastructureServiceCollectionExtensionsTests
{
    /// <summary>
    /// Trace: Operational record retention cancellation remediation Phase 1, Step 1.2.
    /// Verifies: the active Infrastructure composition root registers the catalog service required by broker-environment GET endpoints.
    /// Expected: a scoped catalog service resolves as SqlBrokerEnvironmentCatalogService.
    /// Why: a missing registration makes Minimal APIs infer the service as a request body and prevents API startup.
    /// </summary>
    [Fact]
    public void AddPlatformInfrastructure_ShouldResolveCatalogService_WhenActiveRegistrationIsBuilt()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:UseInMemoryDatabase"] = "true",
                ["Authentication:Provider"] = "Test",
                ["Ig:AccountPreferencesBaseUrl"] = "https://example.test/"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddSingleton<IPlatformEnvironmentContext>(new PlatformEnvironmentContext(PlatformEnvironmentKind.Test));
        services.AddSingleton(TimeProvider.System);
        services.AddPlatformInfrastructure(configuration, new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<SqlBrokerEnvironmentCatalogService>(
            scope.ServiceProvider.GetRequiredService<IBrokerEnvironmentCatalogService>());
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "TNC.Trading.Platform.Infrastructure.UnitTests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = "Test";
    }
}
