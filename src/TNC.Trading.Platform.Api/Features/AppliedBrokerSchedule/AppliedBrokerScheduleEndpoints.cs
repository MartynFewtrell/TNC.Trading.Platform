using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Api.Infrastructure.Platform;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;
using ApplicationFeatures = TNC.Trading.Platform.Application.Features.AppliedBrokerSchedule;

namespace TNC.Trading.Platform.Api.Features.AppliedBrokerSchedule;

internal static class AppliedBrokerScheduleEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/configuration/applied-broker-schedule", GetAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
        platform.MapPut("/configuration/applied-broker-schedule", UpdateAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator);
    }

    private static async Task<IResult> GetAsync(
        ApplicationFeatures.GetAppliedBrokerScheduleProfileHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(
                new ApplicationFeatures.GetAppliedBrokerScheduleProfileRequest(),
                cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(result.Profile.ToResponse());
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Conflict(new { error = "The applied broker Trading Day schedule is unavailable." });
        }
    }

    private static async Task<IResult> UpdateAsync(
        UpdateAppliedBrokerScheduleProfileHttpRequest request,
        ClaimsPrincipal user,
        ApplicationFeatures.UpdateAppliedBrokerScheduleProfileHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(
                request.ToApplicationRequest(user.Identity?.Name ?? "operator"),
                cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(result.Profile.ToResponse());
        }
        catch (ConfigurationValidationException exception)
        {
            return TypedResults.ValidationProblem(exception.Errors.ToDictionary(item => item.Key, item => item.Value));
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Conflict(new { error = "The applied broker Trading Day schedule is unavailable." });
        }
    }
}
