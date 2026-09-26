using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class MarketCategoriesEndpointMapping
{
    public static MarketCategoriesResponse ToResponse(this GetMarketCategoriesResponse response)
        => new(
            response.Categories.Select(category => new MarketCategoryResponse(category.Code, category.NonTradeable)).ToList(),
            response.LastRefreshedAtUtc);

    public static MarketCategoriesResponse ToResponse(this GetMarketCategoriesWithInterestResponse response)
    {
        var status = response.CollectionStatus?.Categories.ToDictionary(item => item.CategoryCode, StringComparer.Ordinal)
            ?? new Dictionary<string, MarketCategoryInstrumentCategoryStatus>(StringComparer.Ordinal);
        var interests = response.Interests.ToDictionary(item => item.CategoryCode, item => item.IsSelected, StringComparer.Ordinal);
        var categories = response.Snapshot?.Categories ?? [];
        var currentCodes = categories.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
        return new(
            categories.OrderBy(item => item.Code, StringComparer.Ordinal)
                .Select(category =>
                {
                    status.TryGetValue(category.Code, out var categoryStatus);
                    interests.TryGetValue(category.Code, out var isInterested);
                    return new MarketCategoryResponse(
                        category.Code,
                        category.NonTradeable,
                        isInterested,
                        categoryStatus?.LastSuccessfulCollectionAtUtc,
                        categoryStatus?.Attempts ?? 0,
                        categoryStatus?.Outcome,
                        categoryStatus?.SafeFailure,
                        categoryStatus?.DetailCoverage?.ToResponse());
                })
                .ToList(),
            response.Snapshot?.LastRefreshedAtUtc,
            response.InterestRevision,
            response.BrokerEnvironment?.ToString(),
            response.Interests
                .Where(item => item.IsSelected && !currentCodes.Contains(item.CategoryCode))
                .Select(item => item.CategoryCode)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray());
    }

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
            MarketCategoriesFailureCategory.RateLimited or
            MarketCategoriesFailureCategory.AllowanceExceeded => TypedResults.Problem(
                "The market category provider rate limit was reached.",
                statusCode: StatusCodes.Status429TooManyRequests),
            MarketCategoriesFailureCategory.ScheduleClosed => TypedResults.Problem(
                "The market category collection window is closed.",
                statusCode: StatusCodes.Status409Conflict),
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

/// <summary>Saved market categories and their environment-scoped interest and collection state.</summary>
internal sealed record MarketCategoriesResponse(
    IReadOnlyList<MarketCategoryResponse> Categories,
    DateTimeOffset? LastRefreshedAtUtc,
    long? InterestRevision = null,
    string? AppliedBrokerEnvironment = null,
    IReadOnlyList<string>? DormantInterestedCategories = null);

/// <summary>Provider category details and safe local collection state.</summary>
internal sealed record MarketCategoryResponse(
    string Code,
    bool NonTradeable,
    bool Interested = false,
    DateTimeOffset? LastSuccessfulCollectionAtUtc = null,
    int Attempts = 0,
    string? CollectionOutcome = null,
    string? SafeFailure = null,
    MarketDetailCoverageResponse? DetailCoverage = null);
