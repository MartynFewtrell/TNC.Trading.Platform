using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Api.Hosting;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Api.UnitTests;

public class PlatformApplicationServiceCollectionExtensionsTests
{
    /// <summary>
    /// Trace: trailing-stops remediation Phase 1, Steps 1.1-1.2.
    /// Verifies: the API composition root can construct every account-preferences route and resolve its scoped application services.
    /// Expected: endpoint metadata is created without inferred request-body failures and all four route dependencies resolve from one scope.
    /// Why: missing explicit registrations previously prevented API startup before account-preferences requests could be handled.
    /// </summary>
    [Fact]
    public async Task AddPlatformApplication_ShouldConstructAccountPreferencesRoutesAndResolveServices_WhenApplicationIsBuilt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddPlatformApplication();
        builder.Services.AddScoped<IPlatformConfigurationStore, TestPlatformConfigurationStore>();
        builder.Services.AddScoped<IAccountPreferencesGateway, TestAccountPreferencesGateway>();
        builder.Services.AddScoped<ITrailingStopsPreferenceObservationStore, TestTrailingStopsPreferenceObservationStore>();
        builder.Services.AddScoped<IPlatformEventStore, TestPlatformEventStore>();
        builder.Services.AddSingleton(TimeProvider.System);

        await using var app = builder.Build();
        app.MapPlatformEndpoints();

        _ = app.Services.GetRequiredService<IEnumerable<EndpointDataSource>>()
            .SelectMany(dataSource => dataSource.Endpoints)
            .ToArray();

        using var scope = app.Services.CreateScope();
        Assert.IsType<GetAccountPreferencesHandler>(scope.ServiceProvider.GetRequiredService<GetAccountPreferencesHandler>());
        Assert.IsType<UpdateAccountPreferencesValidator>(scope.ServiceProvider.GetRequiredService<UpdateAccountPreferencesValidator>());
        Assert.IsType<UpdateAccountPreferencesHandler>(scope.ServiceProvider.GetRequiredService<UpdateAccountPreferencesHandler>());
        Assert.IsType<GetTrailingStopsPreferenceObservationsHandler>(scope.ServiceProvider.GetRequiredService<GetTrailingStopsPreferenceObservationsHandler>());
    }

    private sealed class TestPlatformConfigurationStore : IPlatformConfigurationStore
    {
        public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(PlatformEnvironmentKind? platformEnvironment, BrokerEnvironmentKind? brokerEnvironment, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestAccountPreferencesGateway : IAccountPreferencesGateway
    {
        public Task<AccountPreferencesGatewayOutcome> GetAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AccountPreferencesGatewayOutcome> UpdateAsync(bool trailingStopsEnabled, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestTrailingStopsPreferenceObservationStore : ITrailingStopsPreferenceObservationStore
    {
        public Task AppendAsync(TrailingStopsPreferenceObservation observation, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TrailingStopsPreferenceObservationPage> ListAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string? cursor, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestPlatformEventStore : IPlatformEventStore
    {
        public Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddAsync(PlatformEventRecord record, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}