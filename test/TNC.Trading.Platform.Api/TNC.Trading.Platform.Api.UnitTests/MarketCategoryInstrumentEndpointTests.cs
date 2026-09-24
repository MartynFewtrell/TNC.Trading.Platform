using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Api.Hosting;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class MarketCategoryInstrumentEndpointTests
{
    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, steps 1-4.
    /// Verifies: saved category and instrument reads use Viewer authorization while interest edits and manual refresh use Operator authorization.
    /// Expected: all five resource routes are mapped with their least-privilege named policies.
    /// Why: accidental route omission or role broadening would expose shared trading configuration or status.
    /// </summary>
    [Fact]
    public async Task MapPlatformEndpoints_ShouldApplyViewerAndOperatorPolicies_WhenRoutesAreBuilt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddPlatformApplication();
        builder.Services.AddDataProtection();
        await using var app = builder.Build();
        MarketCategoryInstrumentEndpoints.Map(app.MapGroup("/api/platform"));

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is not null)
            .ToDictionary(endpoint => endpoint.RoutePattern.RawText!, StringComparer.Ordinal);

        AssertPolicy(endpoints, "/api/platform/market-categories", PlatformAuthenticationDefaults.Policies.Viewer);
        AssertPolicy(endpoints, "/api/platform/market-categories/{categoryCode}/instruments", PlatformAuthenticationDefaults.Policies.Viewer);
        AssertPolicy(endpoints, "/api/platform/instrument-collection/status", PlatformAuthenticationDefaults.Policies.Viewer);
        AssertPolicy(endpoints, "/api/platform/market-categories/{categoryCode}/interest", PlatformAuthenticationDefaults.Policies.Operator);
        AssertPolicy(endpoints, "/api/platform/market-categories/refresh", PlatformAuthenticationDefaults.Policies.Operator);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, steps 2 and 4.
    /// Verifies: JSON cannot omit either required field from an interest update command.
    /// Expected: deserialization rejects bodies missing interested or expectedRevision.
    /// Why: silently defaulting omitted values could turn malformed updates into unintended shared preference changes.
    /// </summary>
    [Theory]
    [InlineData("{\"expectedRevision\":1}")]
    [InlineData("{\"interested\":true}")]
    public void UpdateInterestRequest_ShouldRejectMissingFields_WhenJsonIsDeserialized(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateMarketCategoryInterestHttpRequest>(json));
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, steps 1 and 4.
    /// Verifies: the next-page token is protected and carries the environment, exact category, snapshot version, and keyset EPIC.
    /// Expected: the generated cursor can only be decoded with this feature's Data Protection purpose and retains every binding value.
    /// Why: cursor tampering or reuse against another category/environment/version must not expose or mix snapshot pages.
    /// </summary>
    [Fact]
    public void ToResponse_ShouldProtectAllCursorBindings_WhenAnotherPageExists()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();
        var protector = provider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("TNC.Trading.Platform.MarketCategoryInstrumentCursor.v1");
        var page = new MarketCategoryInstrumentSnapshotPage(7, DateTimeOffset.UtcNow, [], "EPIC-123");

        var response = page.ToResponse("FX & CFDs", BrokerEnvironmentKind.Demo, protector);

        var cursor = JsonSerializer.Deserialize<InstrumentPageCursor>(protector.Unprotect(response.NextCursor!));
        Assert.Equal("Demo", cursor!.BrokerEnvironment);
        Assert.Equal("FX & CFDs", cursor.CategoryCode);
        Assert.Equal(7, cursor.SnapshotVersion);
        Assert.Equal("EPIC-123", cursor.AfterEpic);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, steps 3 and 4.
    /// Verifies: the manual category refresh schedule-closed outcome maps to a safe HTTP conflict response.
    /// Expected: the API reports 409 without exposing internal schedule or provider details.
    /// Why: the Operator must be told the window is closed while the schedule guard remains an enforced provider boundary.
    /// </summary>
    [Fact]
    public void ToHttpResult_ShouldReturnConflict_WhenManualRefreshIsOutsideSchedule()
    {
        var result = new RefreshMarketCategoriesResponse(
            new MarketCategoriesRefreshOutcome.Failed(
                MarketCategoriesFailureCategory.ScheduleClosed,
                "internal schedule details"))
            .ToHttpResult();

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("The market category collection window is closed.", problem.ProblemDetails.Detail);
    }

    private static void AssertPolicy(
        IReadOnlyDictionary<string, RouteEndpoint> endpoints,
        string route,
        string expectedPolicy)
    {
        var endpoint = Assert.Contains(route, endpoints);
        var authorization = Assert.Single(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.Equal(expectedPolicy, authorization.Policy);
    }
}
