using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Notifications;
using TNC.Trading.Platform.Infrastructure.Persistence;
using AppNotificationDispatcher = TNC.Trading.Platform.Application.Services.INotificationDispatcher;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal static class PlatformInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PlatformDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("platformdb");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseInMemoryDatabase("tnc-trading-platform");
                return;
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
