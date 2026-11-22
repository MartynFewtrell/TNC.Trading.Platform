using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Application.Features.MarketNavigation;
using TNC.Trading.Platform.Infrastructure.DependencyInjection;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class IgCollectionHttpPipelineTests
{
    /// <summary>
    /// Trace: IG market-data recovery Phase 2, Step 2.2.
    /// Verifies: the production infrastructure registrations remove the ServiceDefaults standard retry pipeline only from market-data collection clients.
    /// Expected: each failing test handler observes exactly one send for category discovery, listing, detail, and phase-1 navigation probe clients.
    /// Why: collection gateways own bounded retry and daily-budget accounting, so hidden handler replays must not duplicate provider attempts.
    /// </summary>
    [Fact]
    public async Task AddPlatformInfrastructure_ShouldUseSingleAttemptPipelines_WhenCollectionRequestsFail()
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
        services.AddSingleton<IPlatformEnvironmentContext>(
            new PlatformEnvironmentContext(PlatformEnvironmentKind.Test));
        services.AddSingleton(TimeProvider.System);
        services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());
        services.AddPlatformInfrastructure(configuration, new TestHostEnvironment());

        var counters = new Dictionary<string, SendCounter>(StringComparer.Ordinal);
        foreach (var clientType in new[]
                 {
                     typeof(IMarketCategoriesGateway),
                     typeof(IMarketCategoryInstrumentsGateway),
                     typeof(IMarketDetailsGateway),
                     typeof(IMarketNavigationGateway)
                 })
        {
            var name = clientType.Name;
            var counter = new SendCounter();
            counters.Add(name, counter);
            services.Configure<HttpClientFactoryOptions>(name, options =>
                options.HttpMessageHandlerBuilderActions.Add(builder =>
                    builder.PrimaryHandler = new FailingHandler(counter)));
        }

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        foreach (var (name, counter) in counters)
        {
            using var client = factory.CreateClient(name);
            using var response = await client.GetAsync("https://provider.test/failure");
            Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(counter.Count == 1, $"{name} sent {counter.Count} times.");
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "TNC.Trading.Platform.Infrastructure.UnitTests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = "Test";
    }

    private sealed class SendCounter
    {
        public int Count { get; private set; }

        public void Increment() => Count++;
    }

    private sealed class FailingHandler(SendCounter counter) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            counter.Increment();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
        }
    }
}
