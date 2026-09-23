using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Api.Hosting;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Features.BrokerEnvironments;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Api.UnitTests;

public class PlatformApplicationServiceCollectionExtensionsTests
{
    /// <summary>
    /// Trace: trailing-stops remediation Phase 1, Steps 1.1-1.2.
    /// Verifies: the API composition root can construct mapped account-preferences and broker-environment routes and resolve their scoped services.
    /// Expected: endpoint metadata is created without inferred request-body failures and all required dependencies resolve from one scope.
    /// Why: missing explicit registrations previously prevented API startup before broker-environment requests could be handled.
    /// </summary>
    [Fact]
    public async Task AddPlatformApplication_ShouldConstructMappedRoutesAndResolveServices_WhenApplicationIsBuilt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddPlatformApplication();
        builder.Services.AddScoped<IPlatformConfigurationStore, TestPlatformConfigurationStore>();
        builder.Services.AddScoped<IAccountPreferencesGateway, TestAccountPreferencesGateway>();
        builder.Services.AddScoped<ITrailingStopsPreferenceObservationStore, TestTrailingStopsPreferenceObservationStore>();
        builder.Services.AddScoped<IPlatformEventStore, TestPlatformEventStore>();
        builder.Services.AddScoped<IBrokerEnvironmentCatalogService, TestBrokerEnvironmentCatalogService>();
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
        public Task<AccountPreferencesObservationResult> ObserveAsync(AccountPreferencesObserveRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AccountPreferencesRemediationResult> RemediateAsync(AccountPreferencesRemediateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

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

    private sealed class TestBrokerEnvironmentCatalogService : IBrokerEnvironmentCatalogService
    {
        public Task<IReadOnlyList<BrokerEnvironmentCatalogItem>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BrokerEnvironmentOperationResult> CreateAsync(CreateBrokerEnvironmentCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BrokerEnvironmentOperationResult> SaveCredentialsAsync(SaveBrokerEnvironmentCredentialsCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BrokerEnvironmentOperationResult> SelectAsync(SelectBrokerEnvironmentCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BrokerEnvironmentStatus> GetStatusAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BrokerEnvironmentRetirementPreview?> PreviewRetirementAsync(Guid brokerEnvironmentId, string actor, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BrokerEnvironmentRetirementResult> RetireAsync(RetireBrokerEnvironmentCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}