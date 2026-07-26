using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Api.Features.GetIgLoginHistory;
using TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;
using TNC.Trading.Platform.Api.Features.GetPlatformEvents;
using TNC.Trading.Platform.Api.Features.GetPlatformStatus;
using TNC.Trading.Platform.Api.Features.TriggerManualAuthRetry;
using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Api.Infrastructure.Platform;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Application.Services;
using AppGetIgLoginHistory = TNC.Trading.Platform.Application.Features.GetIgLoginHistory;
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
        app.MapGet("/", GetRootAsync)
            .AllowAnonymous();

        var platform = app.MapGroup("/api/platform");

        platform.MapGet("/status", GetPlatformStatusAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer);
        platform.MapGet("/ig-login/history", GetIgLoginHistoryAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer);
        platform.MapGet("/configuration", GetPlatformConfigurationAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
        platform.MapPut("/configuration", UpdatePlatformConfigurationAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
        platform.MapPost("/auth/manual-retry", TriggerManualAuthRetryAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
        platform.MapPost("/auth/audit", RecordAuthAuditEventAsync)
            .RequireAuthorization();
        platform.MapGet("/events", GetPlatformEventsAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer);
        platform.MapGet("/auth/administration", GetAuthAdministration)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Administrator);

        app.MapGet("/metadata", GetMetadata)
            .AllowAnonymous();
    }

    private static IResult GetRootAsync(IHostEnvironment environment)
        => GetMetadata(environment);

    private static async Task<IResult> GetPlatformStatusAsync(AppGetPlatformStatus.GetPlatformStatusHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppGetPlatformStatus.GetPlatformStatusRequest(), cancellationToken);

        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> GetIgLoginHistoryAsync(AppGetIgLoginHistory.GetIgLoginHistoryHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppGetIgLoginHistory.GetIgLoginHistoryRequest(), cancellationToken);

        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> GetPlatformConfigurationAsync(AppGetPlatformConfiguration.GetPlatformConfigurationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppGetPlatformConfiguration.GetPlatformConfigurationRequest(), cancellationToken);

        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> UpdatePlatformConfigurationAsync(
        UpdatePlatformConfigurationRequest request,
        UpdatePlatformConfigurationValidator validator,
        AppUpdatePlatformConfiguration.UpdatePlatformConfigurationHandler handler,
        CancellationToken cancellationToken)
        => await UpdatePlatformConfigurationEndpointHandler.HandleAsync(request, validator, handler, cancellationToken);

    private static async Task<IResult> TriggerManualAuthRetryAsync(
        AppTriggerManualAuthRetry.TriggerManualAuthRetryHandler handler,
        CancellationToken cancellationToken)
        => await TriggerManualAuthRetryEndpointHandler.HandleAsync(handler, cancellationToken);

    private static async Task<IResult> GetPlatformEventsAsync(
        string? category,
        string? environment,
        AppGetPlatformEvents.GetPlatformEventsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppGetPlatformEvents.GetPlatformEventsRequest(category, environment), cancellationToken);
        return TypedResults.Ok(result.ToResponse());
    }

    private static IResult GetMetadata(IHostEnvironment environment)
        => TypedResults.Ok(new
        {
            service = environment.ApplicationName,
            environment = environment.EnvironmentName
        });

    private static async Task<IResult> RecordAuthAuditEventAsync(
        RecordAuthAuditEventRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        PlatformConfigurationService configurationService,
        IPlatformEventStore eventStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
        => await RecordAuthAuditEventEndpointHandler.HandleAsync(
            request,
            user,
            httpContext,
            configurationService,
            eventStore,
            timeProvider,
            cancellationToken);

    private static IResult GetAuthAdministration(IOptions<PlatformAuthenticationOptions> authenticationOptions)
        => TypedResults.Ok(new AuthAdministrationResponse(
            authenticationOptions.Value.Provider,
            authenticationOptions.Value.Authorization.RoleClaimType,
            authenticationOptions.Value.ApiAudience));
}
