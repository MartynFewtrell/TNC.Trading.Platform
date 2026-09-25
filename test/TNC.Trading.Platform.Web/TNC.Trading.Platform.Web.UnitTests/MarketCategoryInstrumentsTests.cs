using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class MarketCategoryInstrumentsTests
{
    private static readonly DateTimeOffset SnapshotTime = new(2026, 9, 25, 9, 34, 47, TimeSpan.Zero);

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, step 3.
    /// Verifies: a saved page renders the instrument fields in a tab, snapshot disclaimer, and SQL-only metadata.
    /// Expected: saved values and retrieval time are shown and every request is a GET.
    /// Why: instrument browsing must remain provider-free and clearly distinguish a stored snapshot from a live quote.
    /// </summary>
    [Fact]
    public void Render_ShouldShowSavedSnapshotAndDisclaimer_WhenInstrumentPageIsComplete()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreatePage("Complete", "EPIC-2", CreateInstrument("EPIC-1"))),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateStatus()));

        var cut = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Saved instruments: FX", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Saved snapshot", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("not a live quote or trading instruction", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Bid", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Offer", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("FX &amp; CFD", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("EPIC-1", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("10:15:00", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(["FX & CFD"], cut.FindAll("[role='tab']").Select(tab => tab.TextContent.Trim()).ToArray());
            Assert.Empty(cut.FindAll("table"));
            Assert.Contains("Underlying", cut.Find("[data-testid='market-category-instrument']").TextContent, StringComparison.Ordinal);
            Assert.NotEmpty(cut.FindAll(".instrument-tabs .rz-tabview"));
            Assert.Equal(2, cut.FindAll("button.platform-primary-action").Count);
            Assert.Contains("market-category-link", cut.Find("a[href='/market-categories']").ClassName, StringComparison.Ordinal);
            Assert.Contains("Page 1 of at least 2", cut.Find("nav[aria-label='Saved instrument pages']").TextContent, StringComparison.Ordinal);
            Assert.False(cut.FindAll("nav[aria-label='Saved instrument pages'] button")[1].HasAttribute("disabled"));
        });
        Assert.All(context.ApiHandler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Contains("/api/platform/market-categories/FX/instruments", context.ApiHandler.Requests[0].RequestUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Instrument drill-down date and layout request.
    /// Verifies: saved timestamps use the same local short date/time presentation as the scheduled check.
    /// Expected: the three visible times match local "g" formatting, retain machine-readable UTC values, and collection labels share a row container.
    /// Why: raw UTC strings and vertically stacked schedule metadata made the snapshot difficult to scan.
    /// </summary>
    [Fact]
    public void Render_ShouldAlignAndFormatCollectionTimes_WhenSavedSnapshotHasMetadata()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreatePage("Complete", null, CreateInstrument("EPIC-1"))),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateStatus()));

        var cut = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));
        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find(".instrument-collection-summary");
            Assert.Contains("Last successful collection:", summary.TextContent, StringComparison.Ordinal);
            Assert.Contains("Next scheduled collection check:", summary.TextContent, StringComparison.Ordinal);
            var times = cut.FindAll("time");
            Assert.Equal(3, times.Count);
            Assert.Equal(2, summary.QuerySelectorAll("time").Length);
            Assert.All(times, time =>
            {
                Assert.Equal(SnapshotTime.ToString("O"), time.GetAttribute("datetime"));
                Assert.Equal(SnapshotTime.ToLocalTime().ToString("g"), time.TextContent);
                Assert.DoesNotContain(" UTC", time.TextContent, StringComparison.Ordinal);
            });
        });
    }

    /// <summary>
    /// Trace: Instrument drill-down tabs request.
    /// Verifies: each instrument has a named tab and switching tabs displays only its market data.
    /// Expected: the selected panel changes without loading additional API pages or rendering all 50 instruments' details.
    /// Why: tab navigation must stay usable and bounded when many instruments share a category.
    /// </summary>
    [Fact]
    public void Tabs_ShouldShowSelectedInstrumentOnly_WhenAnotherTabIsActivated()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreatePage("Complete", null,
                CreateInstrument("EPIC-1", "First market"), CreateInstrument("EPIC-2", "Second market"))),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateStatus()));

        var cut = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));
        cut.WaitForAssertion(() => Assert.Equal(["First market", "Second market"],
            cut.FindAll("[role='tab']").Select(tab => tab.TextContent.Trim()).ToArray()));
        Assert.Contains("Page 1 of 1", cut.Find("nav[aria-label='Saved instrument pages']").TextContent, StringComparison.Ordinal);
        Assert.True(cut.FindAll("nav[aria-label='Saved instrument pages'] button")[1].HasAttribute("disabled"));
        Assert.Contains("EPIC-1", cut.Find("[data-testid='market-category-instrument']").TextContent, StringComparison.Ordinal);
        cut.FindAll("[role='tab']")[1].Click();
        Assert.Contains("EPIC-2", cut.Find("[data-testid='market-category-instrument']").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("EPIC-1", cut.Find("[data-testid='market-category-instrument']").TextContent, StringComparison.Ordinal);
        Assert.Equal(3, context.ApiHandler.Requests.Count);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, step 3.
    /// Verifies: server paging retains prior cursor tokens for the in-page Previous action.
    /// Expected: Next sends the opaque cursor and Previous reloads the first page with no cursor.
    /// Why: navigation must honor the API's forward-only keyset contract without downloading unbounded instrument sets.
    /// </summary>
    [Fact]
    public void Paging_ShouldUseOpaqueCursorAndReturnToPriorPage_WhenNextAndPreviousAreSelected()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreatePage("Complete", "opaque+cursor", CreateInstrument("EPIC-1"), CreateInstrument("EPIC-3"))),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreatePage("Complete", null, CreateInstrument("EPIC-2"))),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreatePage("Complete", "opaque+cursor", CreateInstrument("EPIC-1"))));

        var cut = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));
        cut.WaitForAssertion(() => Assert.Contains("EPIC-1", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("Page 1 of at least 2", cut.Find("nav[aria-label='Saved instrument pages']").TextContent, StringComparison.Ordinal);
        cut.FindAll("[role='tab']")[1].Click();
        cut.FindAll("button").Single(button => button.TextContent.Contains("Next page", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("EPIC-2", cut.Find("[data-testid='market-category-instrument']").TextContent, StringComparison.Ordinal);
            Assert.Contains("Page 2 of 2", cut.Find("nav[aria-label='Saved instrument pages']").TextContent, StringComparison.Ordinal);
        });
        cut.FindAll("button").Single(button => button.TextContent.Contains("Previous page", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("EPIC-1", cut.Find("[data-testid='market-category-instrument']").TextContent, StringComparison.Ordinal);
            Assert.Contains("Page 1 of at least 2", cut.Find("nav[aria-label='Saved instrument pages']").TextContent, StringComparison.Ordinal);
        });

        Assert.Contains("cursor=opaque%2Bcursor", context.ApiHandler.Requests[3].RequestUri, StringComparison.Ordinal);
        Assert.DoesNotContain("cursor=", context.ApiHandler.Requests[4].RequestUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, steps 3 and 5.
    /// Verifies: a 409 during paging retains the last rendered page and offers a restart from the current snapshot.
    /// Expected: existing instruments remain visible with an accessible stale warning and Reload action.
    /// Why: a worker refresh must not blank useful saved results or permit continuing with a stale cursor.
    /// </summary>
    [Fact]
    public void Paging_ShouldRetainVisiblePage_WhenSnapshotCursorBecomesStale()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreatePage("Complete", "opaque-cursor", CreateInstrument("EPIC-1"))),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateStatus()),
            _ => PlatformWebTestData.CreateProblemResponse(HttpStatusCode.Conflict, new { title = "Conflict", detail = "Restart browsing." }));

        var cut = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));
        cut.WaitForAssertion(() => Assert.Contains("EPIC-1", cut.Markup, StringComparison.Ordinal));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Next page", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("EPIC-1", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("snapshot changed", cut.Markup, StringComparison.Ordinal);
            Assert.NotEmpty(cut.FindAll("[data-testid='market-category-instruments-reload']"));
        });
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, step 3.
    /// Verifies: failed category/status metadata reads do not hide a successfully loaded SQL instrument page.
    /// Expected: the saved instrument remains visible beside an accessible metadata warning.
    /// Why: collector telemetry must not invalidate or blank a useful versioned snapshot.
    /// </summary>
    [Fact]
    public void Render_ShouldKeepSavedPage_WhenMetadataRequestsFail()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreatePage("Complete", null, CreateInstrument("EPIC-1"))),
            _ => PlatformWebTestData.CreateProblemResponse(HttpStatusCode.ServiceUnavailable, new { title = "Unavailable", detail = "Category metadata unavailable." }),
            _ => PlatformWebTestData.CreateProblemResponse(HttpStatusCode.ServiceUnavailable, new { title = "Unavailable", detail = "Collection status unavailable." }));

        var cut = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("EPIC-1", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Unable to load category details", cut.Find("[data-testid='market-category-instruments-metadata-error']").TextContent, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, step 5.
    /// Verifies: the initial Viewer route exposes an accessible loading state until its saved SQL page arrives.
    /// Expected: the loading marker is initially rendered and is replaced by the saved snapshot after the response completes.
    /// Why: slow SQL/API responses must not leave the route blank or imply that no snapshot exists.
    /// </summary>
    [Fact]
    public void Render_ShouldShowLoadingState_UntilSavedPageArrives()
    {
        var pageResponse = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new DelayedJsonContent(pageResponse.Task) },
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateStatus()));

        var cut = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));

        Assert.NotEmpty(cut.FindAll("[data-testid='market-category-instruments-loading']"));
        pageResponse.SetResult(JsonSerializer.Serialize(
            CreatePage("Complete", null, CreateInstrument("EPIC-1")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='market-category-instruments-loading']"));
            Assert.Contains("EPIC-1", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, step 3.
    /// Verifies: never-collected and successfully collected empty snapshots render distinct states.
    /// Expected: each API state has its own explanatory accessible panel.
    /// Why: absence of history is not equivalent to a complete provider response containing zero instruments.
    /// </summary>
    [Theory]
    [InlineData("NeverCollected", "market-category-instruments-never-collected", "Not collected yet")]
    [InlineData("Complete", "market-category-instruments-complete-empty", "Complete empty snapshot")]
    public void Render_ShouldDistinguishCollectionStates_WhenSnapshotHasNoRows(
        string state,
        string testId,
        string heading)
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreatePage(state, null)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateStatus()));

        var cut = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll($"[data-testid='{testId}']"));
            Assert.Contains(heading, cut.Markup, StringComparison.Ordinal);
        });
    }

    private static object CreatePage(string state, string? nextCursor, params object[] instruments) => new
    {
        State = state,
        CategoryCode = "FX",
        SnapshotVersion = state == "Complete" ? 4L : (long?)null,
        LastRetrievedAtUtc = state == "Complete" ? SnapshotTime : (DateTimeOffset?)null,
        Instruments = instruments,
        NextCursor = nextCursor
    };

    private static object CreateInstrument(string epic, string name = "FX & CFD") => new
    {
        Epic = epic,
        Name = name,
        InstrumentType = "CURRENCIES",
        UnderlyingName = "EUR/USD",
        Expiry = "-",
        LotSize = 1m,
        OtcTradeable = true,
        ScalingFactor = 1m,
        ExpiryTimestamp = (long?)null,
        MarketStatus = "TRADEABLE",
        DelayTime = 0,
        Bid = 1.1m,
        Offer = 1.2m,
        High = 1.3m,
        Low = 1.0m,
        NetChange = 0.1m,
        PercentageChange = 1m,
        UpdateTime = "10:15:00",
        Popularity = 20L
    };

    private static object CreateCategories() => new
    {
        Categories = new[]
        {
            new
            {
                Code = "FX",
                NonTradeable = false,
                Interested = true,
                LastSuccessfulCollectionAtUtc = SnapshotTime,
                Attempts = 1,
                CollectionOutcome = "Complete",
                SafeFailure = (string?)null
            }
        },
        LastRefreshedAtUtc = DateTimeOffset.UtcNow,
        InterestRevision = 1,
        AppliedBrokerEnvironment = "Demo",
        DormantInterestedCategories = Array.Empty<string>()
    };

    private static object CreateStatus() => new
    {
        State = "Available",
        BrokerEnvironment = "Demo",
        TradingDay = DateOnly.FromDateTime(DateTime.UtcNow),
        IsDue = false,
        CurrentSlot = (int?)null,
        NextWakeUpUtc = SnapshotTime,
        PauseReason = (string?)null,
        LastCategoryRefreshAtUtc = DateTimeOffset.UtcNow,
        LastCompletedSlot = 0,
        CycleOutcome = "Complete",
        CategoryPrerequisiteOutcome = "Complete",
        SafeCategoryFailure = (string?)null,
        UsedRequestBudget = 1,
        ApprovedDailyRequestAllowance = 10,
        Categories = new[] { new { CategoryCode = "FX", LastSuccessfulCollectionAtUtc = DateTimeOffset.UtcNow, Attempts = 1, Outcome = "Complete", SafeFailure = (string?)null } }
    };

    private sealed class DelayedJsonContent(Task<string> json) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            var content = Encoding.UTF8.GetBytes(await json.ConfigureAwait(false));
            await stream.WriteAsync(content).ConfigureAwait(false);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
