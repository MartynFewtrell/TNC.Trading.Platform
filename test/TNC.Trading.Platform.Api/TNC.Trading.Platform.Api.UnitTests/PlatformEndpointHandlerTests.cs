using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Authentication;
using AppTriggerManualAuthRetry = TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

namespace TNC.Trading.Platform.Api.UnitTests;

public class PlatformEndpointHandlerTests
{
    [Fact]
    public async Task UpdatePlatformConfigurationHandleAsync_ShouldReturnValidationProblem_WhenRequestIsInvalid()
    {
        var request = CreateUpdateRequest("Test", "Live");
        var validator = new UpdatePlatformConfigurationValidator();

        var result = await UpdatePlatformConfigurationEndpointHandler.HandleAsync(
            request,
            validator,
            handler: null!,
            CancellationToken.None);

        var validationProblem = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ValidationProblem>(result.Result);
        Assert.Contains("BrokerEnvironment", validationProblem.ProblemDetails.Errors.Keys);
    }

    [Fact]
    public async Task TriggerManualAuthRetryHandleAsync_ShouldReturnConflict_WhenManualRetryIsUnavailable()
    {
        var result = await TriggerManualAuthRetryEndpointHandler.HandleAsync(
            _ => throw new InvalidOperationException("Retry is not available."),
            CancellationToken.None);

        var conflict = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<ManualAuthRetryConflictResponse>>(result.Result);
        Assert.Equal("Retry is not available.", conflict.Value.Error);
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
            configurationService: null!,
            eventStore: null!,
            TimeProvider.System,
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