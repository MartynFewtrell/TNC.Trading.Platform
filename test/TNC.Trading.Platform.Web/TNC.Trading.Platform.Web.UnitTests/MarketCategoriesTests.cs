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
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus()));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("FX", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Tradeable", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Non-tradeable", cut.Markup, StringComparison.Ordinal);
            Assert.NotEmpty(cut.FindAll("table[aria-label], table"));
            Assert.NotEmpty(cut.FindAll("a[href='/market-categories/FX/instruments']"));
            Assert.Contains("Interested", cut.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("input[type='checkbox']"));
            Assert.Empty(cut.FindAll("[data-testid='market-categories-refresh']"));
        });
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, steps 1–2.
    /// Verifies: a protected collector-status API failure does not discard the successfully loaded category snapshot.
    /// Expected: categories remain visible and a separate accessible status error is rendered.
    /// Why: status telemetry is auxiliary to the SQL catalogue and must not turn a successful category read into a page failure.
    /// </summary>
    [Fact]
    public void Render_ShouldKeepCategories_WhenCollectorStatusRequestFails()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, true)),
            _ => PlatformWebTestData.CreateProblemResponse(HttpStatusCode.ServiceUnavailable, new { title = "Status unavailable", detail = "Try again later." }));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("FX", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Unable to load collector status", cut.Find("[data-testid='market-category-collection-status-error']").TextContent, StringComparison.Ordinal);
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
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new
            {
                Categories = Array.Empty<object>(),
                LastRefreshedAtUtc = (DateTimeOffset?)null,
                InterestRevision = 0,
                AppliedBrokerEnvironment = "Demo",
                DormantInterestedCategories = new[] { "OLD-CATEGORY" }
            }),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus()));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No saved market categories are available yet", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("OLD-CATEGORY", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("will not be collected", cut.Markup, StringComparison.Ordinal);
        });
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
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus()),
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

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, steps 1-2.
    /// Verifies: an Operator can save one category interest with the shared revision and receives an accessible confirmation.
    /// Expected: the checkbox is labelled, becomes selected after success, and only GET/PUT API operations are issued.
    /// Why: editing interest must update collector intent without triggering provider collection.
    /// </summary>
    [Fact]
    public void UpdateInterest_ShouldUseRevisionAndAvoidProviderRefresh_WhenOperatorChangesSelection()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new { Revision = 5 }));

        var cut = context.Render<MarketCategories>();
        cut.WaitForAssertion(() => Assert.Contains("Interest revision", cut.Markup, StringComparison.Ordinal));
        cut.Find("label[for='category-interest-0']");
        cut.Find("#category-interest-0").Change(true);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Interest saved for FX", cut.Markup, StringComparison.Ordinal);
            Assert.True(cut.Find("#category-interest-0").HasAttribute("checked"));
        });
        Assert.Equal([HttpMethod.Get, HttpMethod.Get, HttpMethod.Put], context.ApiHandler.Requests.Select(item => item.Method));
        Assert.Contains("\"expectedRevision\":4", context.ApiHandler.Requests[2].Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, steps 2 and 5.
    /// Verifies: a 409 interest conflict keeps the saved selection visible and prompts a reload instead of applying an optimistic change.
    /// Expected: the checkbox stays unchecked and a conflict message is announced.
    /// Why: concurrent operators must not see an uncommitted local value as persisted state.
    /// </summary>
    [Fact]
    public void UpdateInterest_ShouldRetainSavedSelection_WhenRevisionConflicts()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus()),
            _ => PlatformWebTestData.CreateProblemResponse(HttpStatusCode.Conflict, new { title = "Conflict", detail = "Reload the categories." }));

        var cut = context.Render<MarketCategories>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#category-interest-0")));
        cut.Find("#category-interest-0").Change(true);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Interest changed elsewhere", cut.Markup, StringComparison.Ordinal);
            Assert.False(cut.Find("#category-interest-0").HasAttribute("checked"));
        });
        Assert.Equal(HttpMethod.Put, context.ApiHandler.Requests[2].Method);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, steps 2 and 5.
    /// Verifies: provider-owned category codes are rendered as text and URL-escaped in the saved-instrument link.
    /// Expected: the link encodes spaces and ampersands while visible text remains the exact category code.
    /// Why: provider strings are untrusted data and must not break navigation or markup.
    /// </summary>
    [Fact]
    public void Render_ShouldEscapeCategoryCode_WhenCreatingSavedInstrumentLink()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX & CFDs", false, false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus()));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("a[href='/market-categories/FX%20%26%20CFDs/instruments']"));
            Assert.Contains("FX &amp; CFDs instruments", cut.Markup, StringComparison.Ordinal);
        });
    }

    private static object CreateCategories(string code, bool nonTradeable, bool interested) => new
    {
        Categories = new[]
        {
            new
            {
                Code = code,
                NonTradeable = nonTradeable,
                Interested = interested,
                LastSuccessfulCollectionAtUtc = (DateTimeOffset?)null,
                Attempts = 0,
                CollectionOutcome = (string?)null,
                SafeFailure = (string?)null
            }
        },
        LastRefreshedAtUtc = DateTimeOffset.UtcNow,
        InterestRevision = 4,
        AppliedBrokerEnvironment = "Demo",
        DormantInterestedCategories = Array.Empty<string>()
    };

    private static object CreateCollectionStatus() => new
    {
        State = "Available",
        BrokerEnvironment = "Demo",
        TradingDay = DateOnly.FromDateTime(DateTime.UtcNow),
        IsDue = false,
        CurrentSlot = (int?)null,
        NextWakeUpUtc = (DateTimeOffset?)null,
        PauseReason = (string?)null,
        LastCategoryRefreshAtUtc = (DateTimeOffset?)null,
        LastCompletedSlot = (int?)null,
        CycleOutcome = (string?)null,
        CategoryPrerequisiteOutcome = (string?)null,
        SafeCategoryFailure = (string?)null,
        UsedRequestBudget = 0,
        ApprovedDailyRequestAllowance = (int?)null,
        Categories = Array.Empty<object>()
    };
}
