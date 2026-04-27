using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Notifications;
using TNC.Trading.Platform.Infrastructure.Persistence;
using AppNotificationDispatcher = TNC.Trading.Platform.Application.Services.INotificationDispatcher;

namespace TNC.Trading.Platform.Infrastructure.Platform;

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
        services.AddScoped<IPlatformConfigurationStore, SqlPlatformConfigurationStore>();
        services.AddScoped<IPlatformRuntimeStateStore, EfPlatformRuntimeStateStore>();
        services.AddScoped<IPlatformRetryCycleStore, EfPlatformRetryCycleStore>();
        services.AddScoped<IPlatformEventStore, EfPlatformEventStore>();
        services.AddScoped<INotificationProvider, RecordedNotificationProvider>();
        services.AddScoped<INotificationProvider, SmtpNotificationProvider>();
        services.AddScoped<INotificationProvider, AzureCommunicationServicesEmailNotificationProvider>();
        services.AddScoped<AppNotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<OperationalRecordRetentionProcessor>();
        services.AddHostedService<OperationalRecordRetentionService>();

        return services;
    }
}
