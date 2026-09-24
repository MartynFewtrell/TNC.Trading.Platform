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
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Services;
using AppAccountDetails = TNC.Trading.Platform.Application.Features.AccountDetails;
using AppGetIgLoginHistory = TNC.Trading.Platform.Application.Features.GetIgLoginHistory;
using AppGetPlatformConfiguration = TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;
using AppGetPlatformEvents = TNC.Trading.Platform.Application.Features.GetPlatformEvents;
using AppGetPlatformStatus = TNC.Trading.Platform.Application.Features.GetPlatformStatus;
using AppRecordAuthAuditEvent = TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent;
using AppTriggerManualAuthRetry = TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;
using AppUpdatePlatformConfiguration = TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;
using AppAccountPreferences = TNC.Trading.Platform.Application.Features.AccountPreferences;
using AppBrokerEnvironments = TNC.Trading.Platform.Application.Features.BrokerEnvironments;
using AppMarketCategories = TNC.Trading.Platform.Application.Features.MarketCategories;

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
        platform.MapGet("/broker-environments", ListBrokerEnvironmentsAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer);
        platform.MapGet("/broker-environments/status", GetBrokerEnvironmentStatusAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer);
        platform.MapPost("/broker-environments", CreateBrokerEnvironmentAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Administrator);
        platform.MapPost("/broker-environments/{id:guid}/credentials", SaveBrokerEnvironmentCredentialsAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Administrator);
        platform.MapPost("/broker-environments/{id:guid}/retirement-preview", PreviewBrokerEnvironmentRetirementAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Administrator);
        platform.MapPost("/broker-environments/{id:guid}/retire", RetireBrokerEnvironmentAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Administrator);
        platform.MapPost("/broker-environments/selection", SelectBrokerEnvironmentAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
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
        platform.MapGet("/account-details", GetAccountDetailsAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer);
        platform.MapPost("/account-details/refresh", RefreshAccountDetailsAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
        MarketCategoryInstrumentEndpoints.Map(platform);
        platform.MapGet("/account-preferences", GetAccountPreferencesAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
        platform.MapPut("/account-preferences", UpdateAccountPreferencesAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
        platform.MapGet("/account-preferences/observations", GetAccountPreferencesObservationsAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
        platform.MapPost("/account-preferences/check-status", CheckAccountPreferencesStatusAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
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

    private static async Task<IResult> ListBrokerEnvironmentsAsync(AppBrokerEnvironments.IBrokerEnvironmentCatalogService service, CancellationToken cancellationToken)
        => TypedResults.Ok(await service.ListAsync(cancellationToken));

    private static async Task<IResult> GetBrokerEnvironmentStatusAsync(AppBrokerEnvironments.IBrokerEnvironmentCatalogService service, CancellationToken cancellationToken)
        => TypedResults.Ok(await service.GetStatusAsync(cancellationToken));

    private static async Task<IResult> CreateBrokerEnvironmentAsync(CreateBrokerEnvironmentRequest request, ClaimsPrincipal user, AppBrokerEnvironments.IBrokerEnvironmentCatalogService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new(request.Name, request.DisplayName, request.Provider, request.Kind, request.EndpointProfile, user.Identity?.Name ?? "administrator"), cancellationToken);
        return result.Succeeded ? TypedResults.Ok(result.Item) : TypedResults.BadRequest(new { error = result.Error });
    }

    private static async Task<IResult> SaveBrokerEnvironmentCredentialsAsync(Guid id, SaveBrokerEnvironmentCredentialsRequest request, ClaimsPrincipal user, AppBrokerEnvironments.IBrokerEnvironmentCatalogService service, CancellationToken cancellationToken)
    {
        var result = await service.SaveCredentialsAsync(new(id, request.ApiKey, request.Identifier, request.Password, user.Identity?.Name ?? "administrator"), cancellationToken);
        return result.Succeeded ? TypedResults.Ok(result.Item) : TypedResults.BadRequest(new { error = result.Error });
    }

    private static async Task<IResult> PreviewBrokerEnvironmentRetirementAsync(Guid id, ClaimsPrincipal user, AppBrokerEnvironments.IBrokerEnvironmentCatalogService service, CancellationToken cancellationToken)
    {
        var result = await service.PreviewRetirementAsync(id, user.Identity?.Name ?? "administrator", cancellationToken);
        return result is null ? TypedResults.Conflict(new { error = "The environment is missing, selected, applied, or retired." }) : TypedResults.Ok(result);
    }

    private static async Task<IResult> RetireBrokerEnvironmentAsync(Guid id, RetireBrokerEnvironmentRequest request, ClaimsPrincipal user, AppBrokerEnvironments.IBrokerEnvironmentCatalogService service, CancellationToken cancellationToken)
    {
        var result = await service.RetireAsync(new(id, request.ConfirmationToken, request.ExpectedConcurrencyToken, request.TypedName, user.Identity?.Name ?? "administrator"), cancellationToken);
        return result.Succeeded ? TypedResults.Ok(result) : TypedResults.Conflict(new { error = result.Error });
    }

    private static async Task<IResult> SelectBrokerEnvironmentAsync(SelectBrokerEnvironmentRequest request, ClaimsPrincipal user, AppBrokerEnvironments.IBrokerEnvironmentCatalogService service, CancellationToken cancellationToken)
    {
        var result = await service.SelectAsync(new(request.BrokerEnvironmentId, request.ExpectedRevision, request.Acknowledged, user.Identity?.Name ?? "operator"), cancellationToken);
        return result.Succeeded ? TypedResults.Ok(result.Status) : TypedResults.Conflict(new { error = result.Error });
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

    private static async Task<IResult> GetAccountDetailsAsync(
        string? cursor,
        AppAccountDetails.GetAccountDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppAccountDetails.GetAccountDetailsRequest(cursor), cancellationToken);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> RefreshAccountDetailsAsync(
        AppAccountDetails.RefreshAccountDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppAccountDetails.RefreshAccountDetailsRequest(), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetAccountPreferencesAsync(
        AppAccountPreferences.GetAccountPreferencesHandler handler,
        CancellationToken cancellationToken)
        => await GetAccountPreferencesEndpointHandler.HandleAsync(handler, cancellationToken);

    private static async Task<IResult> UpdateAccountPreferencesAsync(
        UpdateAccountPreferencesHttpRequest request,
        ClaimsPrincipal user,
        AppAccountPreferences.SaveAccountPreferencesHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => await UpdateAccountPreferencesEndpointHandler.HandleAsync(request, user, handler, httpContext, cancellationToken);

    private static async Task<IResult> CheckAccountPreferencesStatusAsync(
        AppAccountPreferences.CheckAccountPreferencesStatusHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AppAccountPreferences.CheckAccountPreferencesStatusCommand(), httpContext.TraceIdentifier, cancellationToken);
        return result.State is { } state
            ? TypedResults.Ok((result.FailureCategory is not null
                ? state with
                {
                    VerificationStatus = AccountPreferencesVerificationStatus.VerificationFailed,
                    FailureSummary = result.SafeReason
                }
                : state).ToResponse())
            : result.FailureCategory is { } category
                ? AccountPreferencesEndpointMapping.ToProblemResult(category, result.SafeReason)
                : TypedResults.Problem(statusCode: 503, title: "Account preferences check is unavailable.");
    }

    private static async Task<IResult> GetAccountPreferencesObservationsAsync(
        int? pageSize,
        string? cursor,
        AppAccountPreferences.GetTrailingStopsPreferenceObservationsHandler handler,
        CancellationToken cancellationToken)
        => await GetTrailingStopsPreferenceObservationsEndpointHandler.HandleAsync(pageSize, cursor, handler, cancellationToken);

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
        AppRecordAuthAuditEvent.RecordAuthAuditEventHandler handler,
        CancellationToken cancellationToken)
        => await RecordAuthAuditEventEndpointHandler.HandleAsync(
            request,
            user,
            httpContext,
            handler,
            cancellationToken);

    private static IResult GetAuthAdministration(IOptions<PlatformAuthenticationOptions> authenticationOptions)
        => TypedResults.Ok(new AuthAdministrationResponse(
            authenticationOptions.Value.Provider,
            authenticationOptions.Value.Authorization.RoleClaimType,
            authenticationOptions.Value.ApiAudience));
}

internal sealed record CreateBrokerEnvironmentRequest(string Name, string DisplayName, string Provider, string Kind, string EndpointProfile);
internal sealed record SaveBrokerEnvironmentCredentialsRequest(string? ApiKey, string? Identifier, string? Password);
internal sealed record SelectBrokerEnvironmentRequest(Guid BrokerEnvironmentId, long ExpectedRevision, bool Acknowledged);
internal sealed record RetireBrokerEnvironmentRequest(string ConfirmationToken, string ExpectedConcurrencyToken, string TypedName);
