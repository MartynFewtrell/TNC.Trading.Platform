using Microsoft.AspNetCore.Http;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class MarketDetailEndpointMapping
{
    public static IResult ToHttpResult(this GetMarketDetailResponse response) =>
        response.Status switch
        {
            GetMarketDetailStatus.Found => TypedResults.Ok(response.ToResponse()),
            GetMarketDetailStatus.CurrentMembershipNotFound => TypedResults.Problem(
                "The requested instrument is not a member of the current saved category snapshot.",
                statusCode: StatusCodes.Status404NotFound),
            GetMarketDetailStatus.StaleListingVersion => TypedResults.Problem(
                "The category instrument snapshot changed; reload the current listing before opening market details.",
                statusCode: StatusCodes.Status409Conflict),
            GetMarketDetailStatus.AppliedEnvironmentUnavailable => TypedResults.Problem(
                "Saved market details are unavailable for the applied environment.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => throw new InvalidOperationException("Unknown market-detail read result.")
        };
}
