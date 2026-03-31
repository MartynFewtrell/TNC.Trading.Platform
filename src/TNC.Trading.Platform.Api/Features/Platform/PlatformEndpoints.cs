using TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;
using TNC.Trading.Platform.Api.Features.GetPlatformEvents;
using TNC.Trading.Platform.Api.Features.GetPlatformStatus;
using TNC.Trading.Platform.Api.Features.TriggerManualAuthRetry;
using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Api.Infrastructure.Platform;
using TNC.Trading.Platform.Application.Configuration;
using AppGetPlatformConfiguration = TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;
using AppGetPlatformEvents = TNC.Trading.Platform.Application.Features.GetPlatformEvents;
using AppGetPlatformStatus = TNC.Trading.Platform.Application.Features.GetPlatformStatus;
using AppTriggerManualAuthRetry = TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;
using AppUpdatePlatformConfiguration = TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this WebApplication app)
    {
        app.MapGet("/", GetRootAsync);

        var platform = app.MapGroup("/api/platform");

        platform.MapGet("/status", GetPlatformStatusAsync);
        platform.MapGet("/configuration", GetPlatformConfigurationAsync);
        platform.MapPut("/configuration", UpdatePlatformConfigurationAsync);
        platform.MapPost("/auth/manual-retry", TriggerManualAuthRetryAsync);
        platform.MapGet("/events", GetPlatformEventsAsync);

        app.MapGet("/metadata", GetMetadata);
    }

    private static Task<IResult> GetRootAsync(AppGetPlatformStatus.GetPlatformStatusHandler handler, CancellationToken cancellationToken)
        => GetPlatformStatusAsync(handler, cancellationToken);

    private static async Task<IResult> GetPlatformStatusAsync(AppGetPlatformStatus.GetPlatformStatusHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppGetPlatformStatus.GetPlatformStatusRequest(), cancellationToken);
        var status = result.Status;

        return TypedResults.Ok(new GetPlatformStatusResponse(
            status.PlatformEnvironment.ToString(),
            status.BrokerEnvironment.ToString(),
            status.LiveOptionVisible,
            status.LiveOptionAvailable,
            new TradingScheduleResponse(
                status.TradingSchedule.StartOfDay,
                status.TradingSchedule.EndOfDay,
                status.TradingSchedule.TradingDays,
                status.TradingSchedule.WeekendBehavior.ToString(),
                status.TradingSchedule.BankHolidayExclusions,
                status.TradingSchedule.TimeZone),
            new TradingScheduleStateResponse(
                status.TradingScheduleStatus.IsActive,
                status.TradingScheduleStatus.Reason),
            new AuthStateResponse(
                status.SessionStatus.ToString(),
                status.IsDegraded,
                status.BlockedReason),
            new RetryStateResponse(
                status.RetryState.Phase.ToString(),
                status.RetryState.AutomaticAttemptNumber,
                status.RetryState.NextRetryAtUtc,
                status.RetryState.RetryLimitReached,
                status.RetryState.ManualRetryAvailable),
            status.UpdatedAtUtc));
    }

    private static async Task<IResult> GetPlatformConfigurationAsync(AppGetPlatformConfiguration.GetPlatformConfigurationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppGetPlatformConfiguration.GetPlatformConfigurationRequest(), cancellationToken);
        var configuration = result.Configuration;

        return TypedResults.Ok(new GetPlatformConfigurationResponse(
            configuration.PlatformEnvironment.ToString(),
            configuration.BrokerEnvironment.ToString(),
            new ConfigurationTradingScheduleResponse(
                configuration.TradingSchedule.StartOfDay,
                configuration.TradingSchedule.EndOfDay,
                configuration.TradingSchedule.TradingDays,
                configuration.TradingSchedule.WeekendBehavior.ToString(),
                configuration.TradingSchedule.BankHolidayExclusions,
                configuration.TradingSchedule.TimeZone),
            new ConfigurationRetryPolicyResponse(
                configuration.RetryPolicy.InitialDelaySeconds,
                configuration.RetryPolicy.MaxAutomaticRetries,
                configuration.RetryPolicy.Multiplier,
                configuration.RetryPolicy.MaxDelaySeconds,
                configuration.RetryPolicy.PeriodicDelayMinutes),
            new ConfigurationNotificationSettingsResponse(
                configuration.NotificationSettings.Provider,
                configuration.NotificationSettings.EmailTo),
            new CredentialPresenceResponse(
                configuration.Credentials.HasApiKey,
                configuration.Credentials.HasIdentifier,
                configuration.Credentials.HasPassword),
            configuration.RestartRequired,
            configuration.UpdatedAtUtc));
    }

    private static async Task<IResult> UpdatePlatformConfigurationAsync(
        UpdatePlatformConfigurationRequest request,
        UpdatePlatformConfigurationValidator validator,
        AppUpdatePlatformConfiguration.UpdatePlatformConfigurationHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            validator.Validate(request);

            var result = await handler.HandleAsync(
                new AppUpdatePlatformConfiguration.UpdatePlatformConfigurationRequest(
                    new PlatformConfigurationUpdate(
                        Enum.Parse<PlatformEnvironmentKind>(request.PlatformEnvironment, ignoreCase: true),
                        Enum.Parse<BrokerEnvironmentKind>(request.BrokerEnvironment, ignoreCase: true),
                        new TradingScheduleConfiguration(
                            request.TradingSchedule.StartOfDay,
                            request.TradingSchedule.EndOfDay,
                            request.TradingSchedule.TradingDays,
                            Enum.Parse<WeekendBehavior>(request.TradingSchedule.WeekendBehavior, ignoreCase: true),
                            request.TradingSchedule.BankHolidayExclusions,
                            request.TradingSchedule.TimeZone),
                        new RetryPolicyConfiguration(
                            request.RetryPolicy.InitialDelaySeconds,
                            request.RetryPolicy.MaxAutomaticRetries,
                            request.RetryPolicy.Multiplier,
                            request.RetryPolicy.MaxDelaySeconds,
                            request.RetryPolicy.PeriodicDelayMinutes),
                        new NotificationSettingsConfiguration(
                            request.NotificationSettings.Provider,
                            request.NotificationSettings.EmailTo),
                        request.Credentials.ApiKey,
                        request.Credentials.Identifier,
                        request.Credentials.Password,
                        request.ChangedBy)),
                cancellationToken);

            var response = result.Result;

            return TypedResults.Ok(new UpdatePlatformConfigurationResponse(
                response.Snapshot.PlatformEnvironment.ToString(),
                response.Snapshot.BrokerEnvironment.ToString(),
                new UpdatedTradingScheduleResponse(
                    response.Snapshot.TradingSchedule.StartOfDay,
                    response.Snapshot.TradingSchedule.EndOfDay,
                    response.Snapshot.TradingSchedule.TradingDays,
                    response.Snapshot.TradingSchedule.WeekendBehavior.ToString(),
                    response.Snapshot.TradingSchedule.BankHolidayExclusions,
                    response.Snapshot.TradingSchedule.TimeZone),
                new UpdatedRetryPolicyResponse(
                    response.Snapshot.RetryPolicy.InitialDelaySeconds,
                    response.Snapshot.RetryPolicy.MaxAutomaticRetries,
                    response.Snapshot.RetryPolicy.Multiplier,
                    response.Snapshot.RetryPolicy.MaxDelaySeconds,
                    response.Snapshot.RetryPolicy.PeriodicDelayMinutes),
                new UpdatedNotificationSettingsResponse(
                    response.Snapshot.NotificationSettings.Provider,
                    response.Snapshot.NotificationSettings.EmailTo),
                new UpdatedCredentialPresenceResponse(
                    response.Snapshot.Credentials.HasApiKey,
                    response.Snapshot.Credentials.HasIdentifier,
                    response.Snapshot.Credentials.HasPassword),
                response.RestartRequired,
                response.Snapshot.UpdatedAtUtc));
        }
        catch (PlatformValidationException exception)
        {
            return TypedResults.ValidationProblem(exception.Errors.ToDictionary(item => item.Key, item => item.Value));
        }
    }

    private static async Task<IResult> TriggerManualAuthRetryAsync(
        AppTriggerManualAuthRetry.TriggerManualAuthRetryHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await handler.HandleAsync(new AppTriggerManualAuthRetry.TriggerManualAuthRetryRequest(), cancellationToken);
            return TypedResults.Accepted("/api/platform/status", new TriggerManualAuthRetryResponse(response.Result.RetryCycleId));
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.Conflict(new { error = exception.Message });
        }
    }

    private static async Task<IResult> GetPlatformEventsAsync(
        string? category,
        string? environment,
        AppGetPlatformEvents.GetPlatformEventsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppGetPlatformEvents.GetPlatformEventsRequest(category, environment), cancellationToken);
        return TypedResults.Ok(new GetPlatformEventsResponse(
            result.Events.Select(item => new PlatformEventItemResponse(
                item.EventId,
                item.Category,
                item.EventType,
                item.PlatformEnvironment.ToString(),
                item.BrokerEnvironment.ToString(),
                item.Summary,
                item.Details,
                item.OccurredAtUtc)).ToArray()));
    }

    private static IResult GetMetadata(IHostEnvironment environment)
        => TypedResults.Ok(new
        {
            service = environment.ApplicationName,
            environment = environment.EnvironmentName
        });
}
