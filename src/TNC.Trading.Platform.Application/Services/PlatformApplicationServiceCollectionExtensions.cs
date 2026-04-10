using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;
using TNC.Trading.Platform.Application.Features.GetPlatformEvents;
using TNC.Trading.Platform.Application.Features.GetPlatformStatus;
using TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

namespace TNC.Trading.Platform.Application.Services;

internal static class PlatformApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformApplication(this IServiceCollection services)
    {
        services.AddScoped<PlatformConfigurationService>();
        services.AddScoped<TradingScheduleGate>();
        services.AddScoped<PlatformStateCoordinator>();
        services.AddScoped<GetPlatformStatusHandler>();
        services.AddScoped<GetPlatformConfigurationHandler>();
        services.AddScoped<UpdatePlatformConfigurationHandler>();
        services.AddScoped<TriggerManualAuthRetryHandler>();
        services.AddScoped<GetPlatformEventsHandler>();
        services.AddHostedService<PlatformAuthSupervisor>();

        return services;
    }
}
