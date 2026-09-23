using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class MarketCategoriesTests
{
    /// <summary>
    /// Trace: Work Item 4, Viewer market-category presentation.
    /// Verifies: a Viewer can render a saved market-category snapshot and sees the semantic table.
    /// Expected: category codes, trading status, and the saved timestamp are visible without an operator refresh action.
    /// Why: this protects the read-only operator workflow and prevents accidental exposure of the refresh capability.
    /// </summary>
    [Fact]
    public void Render_ShouldShowSavedCategories_WhenSnapshotExists()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new
            {
                Categories = new[] { new { Code = "FX", NonTradeable = false }, new { Code = "INDICES", NonTradeable = true } },
                LastRefreshedAtUtc = DateTimeOffset.UtcNow
            }));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("FX", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Tradeable", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Non-tradeable", cut.Markup, StringComparison.Ordinal);
            Assert.NotEmpty(cut.FindAll("table[aria-label], table"));
            Assert.Empty(cut.FindAll("[data-testid='market-categories-refresh']"));
        });
    }

    /// <summary>
    /// Trace: Work Item 4, market-category empty state.
    /// Verifies: a successful response with no saved timestamp is represented as an explicit empty state.
    /// Expected: the page explains that no saved categories are available yet.
    /// Why: operators must be able to distinguish a valid empty store from a loading or failed request.
    /// </summary>
    [Fact]
    public void Render_ShouldShowEmptyState_WhenNoSnapshotExists()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new { Categories = Array.Empty<object>(), LastRefreshedAtUtc = (DateTimeOffset?)null }));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() => Assert.Contains("No saved market categories are available yet", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>
    /// Trace: Work Item 4, operator refresh and stale-data safety.
    /// Verifies: an Operator can refresh and a failed refresh retains the last saved snapshot while displaying a stale failure.
    /// Expected: the saved category remains visible and the failure is announced after the refresh request fails.
    /// Why: a provider outage must not replace usable saved data with a blank or misleading page.
    /// </summary>
    [Fact]
    public void Refresh_ShouldKeepSavedCategories_WhenProviderRefreshFails()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new
            {
                Categories = new[] { new { Code = "FX", NonTradeable = false } },
                LastRefreshedAtUtc = DateTimeOffset.UtcNow
            }),
            _ => PlatformWebTestData.CreateProblemResponse(HttpStatusCode.ServiceUnavailable, new { title = "Provider unavailable", detail = "Try again later." }));

        var cut = context.Render<MarketCategories>();
        Assert.NotEmpty(cut.FindAll("button.platform-primary-action"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='market-categories-refresh']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("FX", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("saved categories are still shown", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Unable to refresh market categories", cut.Markup, StringComparison.Ordinal);
        });
    }
}
