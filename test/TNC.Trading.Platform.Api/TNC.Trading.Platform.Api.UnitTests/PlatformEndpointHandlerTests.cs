using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Api.Features.TriggerManualAuthRetry;
using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Authentication;
using AppTriggerManualAuthRetry = TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

namespace TNC.Trading.Platform.Api.UnitTests;

public class PlatformEndpointHandlerTests
{
    /// <summary>
    /// Verifies the manual-retry transport contract for an accepted application outcome.
    /// Expected: the endpoint returns HTTP 202 with the retry cycle identifier.
    /// Why: clients use this stable response to begin their status refresh flow without depending on coordinator internals.
    /// </summary>
    [Fact]
    public async Task TriggerManualAuthRetryHandleAsync_ShouldReturnAccepted_WhenManualRetryIsAccepted()
    {
        var retryCycleId = Guid.NewGuid();

        var result = await TriggerManualAuthRetryEndpointHandler.HandleAsync(
            _ => Task.FromResult(new AppTriggerManualAuthRetry.TriggerManualAuthRetryResponse(
                AppTriggerManualAuthRetry.TriggerManualAuthRetryOutcome.Accepted(retryCycleId))),
            CancellationToken.None);

        var accepted = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Accepted<TriggerManualAuthRetryResponse>>(result.Result);
        Assert.Equal(retryCycleId, accepted.Value.RetryCycleId);
    }

    /// <summary>
    /// Verifies the manual-retry transport contract for an expected rejection.
    /// Expected: the endpoint maps the rejection to HTTP 409 with the conflict error.
    /// Why: the UI and API clients need a stable conflict response for unavailable retry operations.
    /// </summary>
    [Fact]
    public async Task TriggerManualAuthRetryHandleAsync_ShouldReturnConflictProblemDetails_WhenManualRetryIsRejected()
    {
        var result = await TriggerManualAuthRetryEndpointHandler.HandleAsync(
            _ => Task.FromResult(new AppTriggerManualAuthRetry.TriggerManualAuthRetryResponse(
                AppTriggerManualAuthRetry.TriggerManualAuthRetryOutcome.Rejected(
                    AppTriggerManualAuthRetry.ManualAuthRetryRejectionReason.RetryLimitNotReached))),
            CancellationToken.None);

        var conflict = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<ManualAuthRetryConflictResponse>>(result.Result);
        Assert.Equal("Manual retry becomes available only after the initial automatic retries are exhausted.", conflict.Value.Error);
    }

    [Fact]
    public async Task UpdatePlatformConfigurationHandleAsync_ShouldReturnValidationProblem_WhenRequestIsInvalid()
    {
        var request = CreateUpdateRequest("invalid", "Demo");
        var validator = new UpdatePlatformConfigurationValidator();

        var result = await UpdatePlatformConfigurationEndpointHandler.HandleAsync(
            request,
            validator,
            handler: null!,
            CancellationToken.None);

        var validationProblem = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ValidationProblem>(result.Result);
        Assert.Contains(nameof(request.PlatformEnvironment), validationProblem.ProblemDetails.Errors.Keys);
        Assert.Equal(400, validationProblem.StatusCode);
        Assert.Equal("One or more validation errors occurred.", validationProblem.ProblemDetails.Title);
    }

    [Fact]
    public async Task TriggerManualAuthRetryHandleAsync_ShouldReturnConflict_WhenManualRetryIsUnavailable()
    {
        var result = await TriggerManualAuthRetryEndpointHandler.HandleAsync(
            _ => Task.FromResult(new AppTriggerManualAuthRetry.TriggerManualAuthRetryResponse(
                AppTriggerManualAuthRetry.TriggerManualAuthRetryOutcome.Rejected(
                    AppTriggerManualAuthRetry.ManualAuthRetryRejectionReason.RetryLimitNotReached))),
            CancellationToken.None);

        var conflict = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<ManualAuthRetryConflictResponse>>(result.Result);
        Assert.Equal("Manual retry becomes available only after the initial automatic retries are exhausted.", conflict.Value.Error);
    }

    [Fact]
    public async Task RecordAuthAuditEventHandleAsync_ShouldReturnValidationProblem_WhenEventTypeIsUnsupported()
    {
        var request = new RecordAuthAuditEventRequest("UnsupportedEvent", "/operator/configuration", null);
        var user = CreatePrincipal(new Claim(PlatformAuthenticationDefaults.Claims.PreferredUserName, "preferred.operator"));
        var httpContext = new DefaultHttpContext();

        var result = await RecordAuthAuditEventEndpointHandler.HandleAsync(
            request,
            user,
            httpContext,
            handler: null!,
            CancellationToken.None);

        var validationProblem = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ValidationProblem>(result.Result);
        Assert.Contains(nameof(request.EventType), validationProblem.ProblemDetails.Errors.Keys);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "TestAuthentication"));

    private static UpdatePlatformConfigurationRequest CreateUpdateRequest(string platformEnvironment, string brokerEnvironment)
    {
        var tradingSchedule = new UpdateTradingScheduleRequest(
            new TimeOnly(8, 0),
            new TimeOnly(16, 30),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            "ExcludeWeekends",
            [],
            "UTC");

        var retryPolicy = new UpdateRetryPolicyRequest(1, 5, 2, 60, 5);
        var notificationSettings = new UpdateNotificationSettingsRequest("RecordedOnly", "owner@example.com");
        var credentials = new UpdateIgCredentialsRequest("api-key", "identifier", "password");

        return new UpdatePlatformConfigurationRequest(
            platformEnvironment,
            brokerEnvironment,
            tradingSchedule,
            retryPolicy,
            notificationSettings,
            credentials,
            "unit-test");
    }

}