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
    /// Expected: category codes, trading status, and the saved timestamp are visible with read-only interest checkboxes and no manual refresh action.
    /// Why: this protects the read-only workflow and ensures scheduled collection needs no page action.
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
            Assert.Contains("Market details coverage", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Partial", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Last complete:", cut.Markup, StringComparison.Ordinal);
            Assert.NotEmpty(cut.FindAll("table[aria-label], table"));
            Assert.NotEmpty(cut.FindAll("a[href='/market-categories/FX/instruments']"));
            Assert.Equal("Interested in FX", cut.Find("#category-interest-0").GetAttribute("aria-label"));
            Assert.True(cut.Find("#category-interest-0").HasAttribute("checked"));
            Assert.True(cut.Find("#category-interest-0").HasAttribute("disabled"));
            Assert.Empty(cut.FindAll("label[for='category-interest-0']"));
            Assert.Empty(cut.FindAll("[data-testid='market-categories-refresh']"));
            Assert.NotEmpty(cut.FindAll("[data-testid='market-category-collection-status']"));
            Assert.True(cut.Markup.IndexOf("market-category-collection-status", StringComparison.Ordinal)
                < cut.Markup.IndexOf("market-categories-saved", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Trace: Market categories page, instrument collection summary.
    /// Verifies: the next scheduled check and configured quota share one responsive row without internal collector state.
    /// Expected: both summary values are present in the same container, with no collector status or scheduling reason shown.
    /// Why: operators need the actionable schedule and budget at a glance without an internal status code.
    /// </summary>
    [Fact]
    public void Render_ShouldAlignScheduleAndQuota_WhenAllowanceIsConfigured()
    {
        var nextWakeUp = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus(nextWakeUp, 100)));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find("[data-testid='market-category-collection-status'] .market-category-collection-summary");
            var values = summary.QuerySelectorAll("p");
            Assert.Equal(2, values.Length);
            Assert.Contains("Next scheduled check:", values[0].TextContent, StringComparison.Ordinal);
            Assert.Equal(nextWakeUp.ToString("O"), values[0].QuerySelector("time")?.GetAttribute("datetime"));
            Assert.Contains("Daily request quota: 0 used of 100.", values[1].TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Collector:", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("SlotAlreadyObserved", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: Market categories schedule status after operator trading-schedule updates.
    /// Verifies: refreshing the status after Saturday is enabled replaces the old next check without fetching categories or calling IG.
    /// Expected: the displayed UTC instant changes from Monday to Saturday and only the status GET is repeated.
    /// Why: an open categories page must not retain an obsolete schedule after configuration changes.
    /// </summary>
    [Fact]
    public void RefreshCollectionStatus_ShouldShowSaturday_WhenTradingScheduleChanges()
    {
        var monday = new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
        var saturday = new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero);
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus(monday, 100)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus(saturday, 100)));

        var cut = context.Render<MarketCategories>();
        cut.WaitForAssertion(() => Assert.Equal(monday.ToString("O"),
            cut.Find("[data-testid='market-category-collection-status'] time").GetAttribute("datetime")));

        cut.Find("[data-testid='market-category-refresh-schedule']").Click();

        cut.WaitForAssertion(() => Assert.Equal(saturday.ToString("O"),
            cut.Find("[data-testid='market-category-collection-status'] time").GetAttribute("datetime")));
        Assert.Equal(3, context.ApiHandler.CallCount);
        Assert.All(context.ApiHandler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Equal(context.ApiHandler.Requests[1].RequestUri, context.ApiHandler.Requests[2].RequestUri);
    }

    /// <summary>
    /// Trace: Market categories schedule status after operator trading-schedule updates.
    /// Verifies: a status failure clears the previous schedule and a subsequent retry restores the latest value.
    /// Expected: the old next check disappears while the error is visible, then Saturday replaces it on retry.
    /// Why: failed refreshes must not present stale schedule data as current.
    /// </summary>
    [Fact]
    public void RefreshCollectionStatus_ShouldClearStaleCheck_WhenStatusReadFails()
    {
        var monday = new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
        var saturday = new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero);
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus(monday, 100)),
            _ => PlatformWebTestData.CreateProblemResponse(HttpStatusCode.ServiceUnavailable, new { title = "Status unavailable" }),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus(saturday, 100)));

        var cut = context.Render<MarketCategories>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='market-category-collection-status'] time")));
        cut.Find("[data-testid='market-category-refresh-schedule']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='market-category-collection-status'] time"));
            Assert.NotEmpty(cut.FindAll("[data-testid='market-category-collection-status-error']"));
        });
        cut.Find("[data-testid='market-category-refresh-schedule']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(saturday.ToString("O"),
                cut.Find("[data-testid='market-category-collection-status'] time").GetAttribute("datetime"));
            Assert.Empty(cut.FindAll("[data-testid='market-category-collection-status-error']"));
        });
    }

    /// <summary>
    /// Trace: active-window startup collection feedback on the market categories page.
    /// Verifies: the most recent successful category refresh remains visible when the next scheduled slot has moved to a later day.
    /// Expected: Saturday appears as the last refresh and Monday as the next check.
    /// Why: a completed startup check should not be mistaken for a missed Saturday check.
    /// </summary>
    [Fact]
    public void Render_ShouldShowSaturdayRefresh_WhenNextCheckIsMonday()
    {
        var saturday = new DateTimeOffset(2026, 9, 26, 11, 0, 0, TimeSpan.Zero);
        var monday = new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus(monday, 100, saturday)));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find("[data-testid='market-category-collection-status'] .market-category-collection-summary");
            Assert.Equal(monday.ToString("O"), summary.QuerySelectorAll("time")[0].GetAttribute("datetime"));
            Assert.Contains("Last category refresh:", summary.TextContent, StringComparison.Ordinal);
            Assert.Equal(saturday.ToString("O"), summary.QuerySelectorAll("time")[1].GetAttribute("datetime"));
        });
    }

    /// <summary>
    /// Trace: Market categories page, collapsible panels.
    /// Verifies: instrument collection and saved categories use the shared Configuration accordion presentation.
    /// Expected: each panel has its own summary and starts expanded in the existing display order.
    /// Why: either panel can be collapsed without hiding previously visible information by default.
    /// </summary>
    [Fact]
    public void Render_ShouldShowExpandablePanels_WhenSnapshotAndStatusExist()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus()));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() =>
        {
            var panels = cut.FindAll("details.platform-accordion-section");
            Assert.Equal(2, panels.Count);
            Assert.All(panels, panel => Assert.True(panel.HasAttribute("open")));
            Assert.Equal("Instrument collection", panels[0].QuerySelector("summary")?.TextContent);
            Assert.NotNull(panels[0].QuerySelector("[data-testid='market-category-collection-status']"));
            Assert.Equal("Saved categories", panels[1].QuerySelector("summary")?.TextContent);
            Assert.NotNull(panels[1].QuerySelector("[data-testid='market-categories-saved']"));
        });
    }

    /// <summary>
    /// Trace: Market categories page, unconfigured collection allowance.
    /// Verifies: removing the collector-state line does not conceal the explicit paused-allowance message.
    /// Expected: the collection summary still explains why requests are paused when the allowance is absent.
    /// Why: an unset allowance must remain visible as an operator action item.
    /// </summary>
    [Fact]
    public void Render_ShouldShowPausedAllowance_WhenQuotaIsUnconfigured()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus()));

        var cut = context.Render<MarketCategories>();

        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find("[data-testid='market-category-collection-status'] .market-category-collection-summary");
            Assert.Contains("Daily request allowance is not configured; collection is paused.", summary.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Collector:", cut.Markup, StringComparison.Ordinal);
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
    /// Trace: Market categories page, automatic collection.
    /// Verifies: an Operator sees saved categories and collection status without a manual provider refresh.
    /// Expected: no refresh button or provider POST is issued on page load.
    /// Why: scheduled collection owns provider refreshes independently of viewing the catalogue.
    /// </summary>
    [Fact]
    public void Render_ShouldNotOfferManualRefresh_WhenOperatorViewsSavedCategories()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories("FX", false, false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCollectionStatus()));

        var cut = context.Render<MarketCategories>();
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='market-categories-saved']"));
            Assert.NotEmpty(cut.FindAll("[data-testid='market-category-collection-status']"));
            Assert.Empty(cut.FindAll("[data-testid='market-categories-refresh']"));
        });
        Assert.Equal([HttpMethod.Get, HttpMethod.Get], context.ApiHandler.Requests.Select(item => item.Method));
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, steps 1-2.
    /// Verifies: an Operator can save one category interest with the shared revision and receives an accessible confirmation.
    /// Expected: only the checkbox is visible in its cell, with an accessible name, and only GET/PUT API operations are issued.
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
        Assert.Equal("Interested in FX", cut.Find("#category-interest-0").GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("label[for='category-interest-0']"));
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
    /// Expected: the link encodes spaces and ampersands while visible text remains the exact category code, without an underline.
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
            var link = cut.Find("a[href='/market-categories/FX%20%26%20CFDs/instruments']");
            Assert.Equal("FX & CFDs", link.TextContent);
            Assert.Equal("market-category-link", link.GetAttribute("class"));
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
                SafeFailure = (string?)null,
                DetailCoverage = new
                {
                    IsFollowed = true,
                    State = "Partial",
                    ExpectedCount = 2,
                    CompletedCount = 1,
                    ExcludedCount = 0,
                    OutstandingCount = 1,
                    LastCompleteAtUtc = new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.Zero),
                    NextScheduledCheckUtc = (DateTimeOffset?)null,
                    SafeFailureCode = (string?)null
                }
            }
        },
        LastRefreshedAtUtc = DateTimeOffset.UtcNow,
        InterestRevision = 4,
        AppliedBrokerEnvironment = "Demo",
        DormantInterestedCategories = Array.Empty<string>()
    };

    private static object CreateCollectionStatus(
        DateTimeOffset? nextWakeUpUtc = null,
        int? approvedDailyRequestAllowance = null,
        DateTimeOffset? lastCategoryRefreshAtUtc = null) => new
    {
        State = "Available",
        BrokerEnvironment = "Demo",
        TradingDay = DateOnly.FromDateTime(DateTime.UtcNow),
        IsDue = false,
        CurrentSlot = (int?)null,
        NextWakeUpUtc = nextWakeUpUtc,
        PauseReason = "SlotAlreadyObserved",
        LastCategoryRefreshAtUtc = lastCategoryRefreshAtUtc,
        LastCompletedSlot = (int?)null,
        CycleOutcome = (string?)null,
        CategoryPrerequisiteOutcome = (string?)null,
        SafeCategoryFailure = (string?)null,
        UsedRequestBudget = 0,
        ApprovedDailyRequestAllowance = approvedDailyRequestAllowance,
        Categories = Array.Empty<object>()
    };
}
