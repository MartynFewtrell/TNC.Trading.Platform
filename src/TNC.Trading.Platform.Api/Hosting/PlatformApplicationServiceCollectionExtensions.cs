using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Features.GetIgLoginHistory;
using TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;
using TNC.Trading.Platform.Application.Features.GetPlatformEvents;
using TNC.Trading.Platform.Application.Features.GetPlatformStatus;
using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;
using TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent;
using TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Api.Hosting;

internal static class PlatformApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformApplication(this IServiceCollection services)
    {
        services.AddSingleton<PlatformAuthSimulationSettings>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var configuredValue = configuration["Bootstrap:AuthSimulation:SessionLifetimeSeconds"];
            var seconds = int.TryParse(configuredValue, out var value) && value > 0 ? value : 900;
            return new PlatformAuthSimulationSettings(TimeSpan.FromSeconds(seconds));
        });
        services.AddSingleton<IPlatformApplicationLogger, PlatformApplicationLogger>();
        services.AddScoped<PlatformConfigurationService>();
        services.AddScoped<TradingScheduleGate>();
        services.AddScoped<PlatformAuthenticationReconciler>();
        services.AddScoped<IPlatformAuthenticationReconciler>(serviceProvider =>
            serviceProvider.GetRequiredService<PlatformAuthenticationReconciler>());
        services.AddScoped<ReconcilePlatformAuthenticationHandler>();
        services.AddScoped<RecordAuthAuditEventHandler>();
        services.AddScoped<UpdatePlatformConfigurationValidator>();
        services.AddScoped<GetPlatformStatusHandler>();
        services.AddScoped<GetPlatformConfigurationHandler>();
        services.AddScoped<UpdatePlatformConfigurationHandler>();
        services.AddScoped<TriggerManualAuthRetryHandler>();
        services.AddScoped<GetPlatformEventsHandler>();
        services.AddScoped<GetIgLoginHistoryHandler>();
        services.AddScoped<GetAccountDetailsHandler>();
        services.AddScoped<RefreshAccountDetailsHandler>();
        services.AddScoped<GetAccountPreferencesHandler>();
        services.AddScoped<UpdateAccountPreferencesValidator>();
        services.AddScoped<UpdateAccountPreferencesHandler>();
        services.AddScoped<ReconcileAccountPreferencesHandler>();
        services.AddScoped<IAccountPreferencesVerificationNudge, NudgeAccountPreferencesVerificationHandler>();
        services.AddScoped<RemediateAccountPreferencesHandler>();
        services.AddScoped<GetTrailingStopsPreferenceObservationsHandler>();

        return services;
    }
}