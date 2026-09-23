using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Application.Features.MarketCategories;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class MarketCategoriesEndpointMappingTests
{
    /// <summary>
    /// Trace: Market Categories API response contract. Verifies a saved snapshot is exposed with its persisted timestamp and code values.
    /// Expected: the transport response contains the same categories and timestamp, protecting the read-only saved-data contract.
    /// </summary>
    [Fact]
    public void ToResponse_ShouldReturnSavedCategories_WhenSnapshotExists()
    {
        var refreshedAt = DateTimeOffset.UtcNow;
        var response = new GetMarketCategoriesResponse(
            [new MarketCategory("FX", false), new MarketCategory("EQUITIES", true)],
            refreshedAt,
            true).ToResponse();

        Assert.Equal(refreshedAt, response.LastRefreshedAtUtc);
        Assert.Collection(response.Categories,
            category => Assert.Equal(("FX", false), (category.Code, category.NonTradeable)),
            category => Assert.Equal(("EQUITIES", true), (category.Code, category.NonTradeable)));
    }

    /// <summary>
    /// Trace: Market Categories API failure safety. Verifies typed provider failures become non-success Problem Details without exposing provider data.
    /// Expected: malformed provider data maps to HTTP 502 with stable safe detail.
    /// </summary>
    [Fact]
    public void ToHttpResult_ShouldReturnSafeProblemDetails_WhenProviderDataIsMalformed()
    {
        var result = new RefreshMarketCategoriesResponse(
            new MarketCategoriesRefreshOutcome.Failed(
                MarketCategoriesFailureCategory.MalformedProviderData,
                "raw provider response must not escape")).ToHttpResult();

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
        Assert.Equal("The market category provider returned invalid data.", problem.ProblemDetails.Detail);
        Assert.DoesNotContain("raw provider", problem.ProblemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

}
