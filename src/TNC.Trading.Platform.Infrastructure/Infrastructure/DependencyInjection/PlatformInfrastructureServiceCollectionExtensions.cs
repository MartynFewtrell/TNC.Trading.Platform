using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.GetIgLoginHistory.Ports;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry.Ports;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Features.GetPlatformEvents.Ports;
using TNC.Trading.Platform.Application.Features.GetPlatformStatus.Ports;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent.Ports;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;
using TNC.Trading.Platform.Infrastructure.Notifications;
using TNC.Trading.Platform.Infrastructure.Notifications.AzureCommunicationServices;
using TNC.Trading.Platform.Infrastructure.Notifications.Recorded;
using TNC.Trading.Platform.Infrastructure.Notifications.Smtp;
using TNC.Trading.Platform.Infrastructure.Operations.Retention;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;
using TNC.Trading.Platform.Infrastructure.Startup;
using TNC.Trading.Platform.Infrastructure.Platform;
using AppNotificationDispatcher = TNC.Trading.Platform.Application.Services.INotificationDispatcher;

namespace TNC.Trading.Platform.Infrastructure.DependencyInjection;

internal static class PlatformInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddDbContext<PlatformDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("platformdb");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                if (configuration.GetValue<bool>("Persistence:UseInMemoryDatabase"))
                {
                    if (!string.Equals(configuration["Authentication:Provider"], "Test", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "The in-memory persistence mode is reserved for isolated automated tests and requires 'Authentication:Provider' to be set to 'Test'.");
                    }

                    options.UseInMemoryDatabase("tnc-trading-platform");
                    return;
                }

                throw new InvalidOperationException(
                    "The 'platformdb' connection string is required but has not been configured. " +
                    "Ensure the connection string is provided via application configuration before starting the application, " +
                    "or explicitly enable the in-memory persistence mode for isolated automated tests.");
            }

            options.UseSqlServer(connectionString);
        });

        services.AddScoped<ProtectedCredentialService>();
        services.AddScoped<IProtectedCredentialService>(serviceProvider => serviceProvider.GetRequiredService<ProtectedCredentialService>());
        services.AddScoped<SqlPlatformConfigurationStore>();
        services.AddScoped<IPlatformConfigurationStore>(serviceProvider =>
            serviceProvider.GetRequiredService<SqlPlatformConfigurationStore>());
        services.AddScoped<IUpdatePlatformConfigurationCommitter>(serviceProvider =>
            serviceProvider.GetRequiredService<SqlPlatformConfigurationStore>());
        services.AddScoped<IPlatformRuntimeStateStore, EfPlatformRuntimeStateStore>();
        services.AddScoped<IPlatformReconciliationLease, SqlPlatformReconciliationLease>();
        services.AddScoped<IPlatformStatusProjectionReader, EfPlatformStatusProjectionReader>();
        services.AddScoped<EfPlatformIgLoginSnapshotStore>();
        services.AddScoped<IPlatformIgLoginSnapshotStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfPlatformIgLoginSnapshotStore>());
        services.AddScoped<IGetIgLoginHistoryReader>(serviceProvider =>
            serviceProvider.GetRequiredService<EfPlatformIgLoginSnapshotStore>());
        services.AddScoped<IPlatformRetryCycleStore, EfPlatformRetryCycleStore>();
        services.AddScoped<IPlatformEventStore, EfPlatformEventStore>();
        services.AddScoped<IRecordAuthAuditEventConfigurationReader, RecordAuthAuditEventConfigurationReader>();
        services.AddScoped<IRecordAuthAuditEventCommitter, RecordAuthAuditEventCommitter>();
        services.AddScoped<IManualAuthRetryCommitter, ManualAuthRetryCommitter>();
        services.AddScoped<IPlatformEventsProjectionReader, EfPlatformEventsProjectionReader>();
        services.AddScoped<INotificationProvider, RecordedNotificationProvider>();
        services.AddScoped<INotificationProvider, SmtpNotificationProvider>();
        services.AddScoped<INotificationProvider, AzureCommunicationServicesEmailNotificationProvider>();
        services.AddScoped<AppNotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<OperationalRecordRetentionProcessor>();
        services.AddScoped<PlatformStartupInitializer>();
        services.AddHostedService<OperationalRecordRetentionService>();

        services.AddScoped<IPlatformIgProofDataStore, EfPlatformIgProofDataStore>();
        services.AddScoped<EfAccountDetailsSnapshotStore>();
        services.AddScoped<IAccountDetailsSnapshotStore>(provider => provider.GetRequiredService<EfAccountDetailsSnapshotStore>());
        services.AddScoped<EfTrailingStopsPreferenceObservationStore>();
        services.AddScoped<ITrailingStopsPreferenceObservationStore>(provider => provider.GetRequiredService<EfTrailingStopsPreferenceObservationStore>());
        services.AddScoped<IAccountPreferencesCurrentStateStore, EfAccountPreferencesCurrentStateStore>();
        services.AddScoped<IAccountPreferencesReconciliationLease, SqlAccountPreferencesReconciliationLease>();
        services.AddScoped<IAccountDetailsRefreshLease, SqlAccountDetailsRefreshLease>();
        services.AddHttpClient<IAccountDetailsGateway, IgAccountDetailsGateway>(client =>
        {
            client.BaseAddress = new Uri("https://demo-api.ig.com/gateway/deal/");
        });
        var accountPreferencesBaseUrl = configuration["Ig:AccountPreferencesBaseUrl"];
        if (!Uri.TryCreate(accountPreferencesBaseUrl, UriKind.Absolute, out var parsedAccountPreferencesBaseUrl)
            || parsedAccountPreferencesBaseUrl.Scheme is not ("http" or "https")
            || !accountPreferencesBaseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("'Ig:AccountPreferencesBaseUrl' must be an absolute HTTP(S) URI with a trailing slash.");
        }

        services.AddHttpClient<IAccountPreferencesGateway, IgAccountPreferencesGateway>(client =>
        {
            client.BaseAddress = parsedAccountPreferencesBaseUrl;
        });

        services.AddHttpClient<IBrokerAuthenticationGateway, IgBrokerAuthenticationGateway>(client =>
        {
            client.BaseAddress = new Uri("https://demo-api.ig.com/gateway/deal/");
        });

        return services;
    }
}