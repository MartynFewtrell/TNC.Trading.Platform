using TNC.Trading.Platform.Application.Features.MarketCategories;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class MarketCategoriesEndpointMapping
{
    public static MarketCategoriesResponse ToResponse(this GetMarketCategoriesResponse response)
        => new(
            response.Categories.Select(category => new MarketCategoryResponse(category.Code, category.NonTradeable)).ToList(),
            response.LastRefreshedAtUtc);

    public static IResult ToHttpResult(this RefreshMarketCategoriesResponse response)
        => response.Outcome switch
        {
            MarketCategoriesRefreshOutcome.Saved saved => TypedResults.Ok(ToResponse(saved.Snapshot)),
            MarketCategoriesRefreshOutcome.Failed failed => ToHttpResult(failed),
            _ => throw new InvalidOperationException("Unknown market categories refresh outcome.")
        };

    private static MarketCategoriesResponse ToResponse(MarketCategorySnapshot snapshot)
        => new(
            snapshot.Categories.Select(category => new MarketCategoryResponse(category.Code, category.NonTradeable)).ToList(),
            snapshot.LastRefreshedAtUtc);

    private static IResult ToHttpResult(MarketCategoriesRefreshOutcome.Failed failure)
        => failure.Category switch
        {
            MarketCategoriesFailureCategory.RateLimited => TypedResults.Problem(
                "The market category provider rate limit was reached.",
                statusCode: StatusCodes.Status429TooManyRequests),
            MarketCategoriesFailureCategory.MalformedProviderData => TypedResults.Problem(
                "The market category provider returned invalid data.",
                statusCode: StatusCodes.Status502BadGateway),
            MarketCategoriesFailureCategory.Timeout => TypedResults.Problem(
                "The market category provider timed out.",
                statusCode: StatusCodes.Status504GatewayTimeout),
            MarketCategoriesFailureCategory.Unauthorized or
            MarketCategoriesFailureCategory.Unavailable or
            MarketCategoriesFailureCategory.Rejected or
            MarketCategoriesFailureCategory.Transient or
            MarketCategoriesFailureCategory.UnsupportedEnvironment => TypedResults.Problem(
                "The market category provider is unavailable for the applied environment.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => throw new InvalidOperationException("Unknown market categories failure category.")
        };
}

internal sealed record MarketCategoriesResponse(
    IReadOnlyList<MarketCategoryResponse> Categories,
    DateTimeOffset? LastRefreshedAtUtc);

internal sealed record MarketCategoryResponse(string Code, bool NonTradeable);
