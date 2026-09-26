using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class MarketDetailsTests
{
    private static readonly DateTimeOffset ListingTime = new(2026, 9, 25, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DetailTime = new(2026, 9, 25, 17, 56, 59, TimeSpan.Zero);

    /// <summary>
    /// Trace: Market Details Work Item 6, steps 3-6.
    /// Verifies: the deep-link page renders saved provenance, quote disclaimers, typed terms, ordered currencies and bands, and escaped provider notices.
    /// Expected: retrieval timestamps are visibly UTC, zero remains zero, a null band maximum is unbounded, and no provider notice becomes markup.
    /// Why: the detail view is an analysis snapshot and must preserve provider facts without implying live prices or creating an injection surface.
    /// </summary>
    [Fact]
    public void Render_ShouldShowSavedTermsAndEscapedNotices_WhenObservationIsComplete()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateDetail("CS.D.ADAUSD.CFD.IP")));
        context.NavigationManager.NavigateTo(
            "/market-categories/FX%20%26%20CFDs/instruments/CS.D.ADAUSD.CFD.IP/market-details?listingVersion=7");

        var cut = context.Render<MarketDetails>(parameters => parameters
            .Add(page => page.CategoryCode, "FX & CFDs")
            .Add(page => page.Epic, "CS.D.ADAUSD.CFD.IP"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Saved quote snapshot — not live", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("historical provider snapshot", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Coverage and provenance", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("BulkV2", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("/markets", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("2026-09-25 17:56:59 UTC", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("17:56:59", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Not supplied by IG", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("No upper bound", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("0 POINTS", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("ADA market notice", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(cut.FindAll("time"), time => time.GetAttribute("datetime") == DetailTime.ToString("O"));
            Assert.Equal(2, cut.FindAll("[data-testid='market-detail-currencies'] tbody tr").Count);
            Assert.Equal(2, cut.FindAll("[data-testid='market-detail-margin-bands'] tbody tr").Count);
            Assert.NotEmpty(cut.FindAll("th[scope='col']"));
            Assert.NotEmpty(cut.FindAll("section[data-testid='market-detail-technical-source']"));
            Assert.NotEmpty(cut.FindAll("details.platform-accordion-section:not([open])"));
            Assert.Empty(cut.FindAll("img"));
            Assert.DoesNotContain("Refresh from IG", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Place order", cut.Markup, StringComparison.Ordinal);
        });

        Assert.Single(context.ApiHandler.Requests);
        Assert.Equal(HttpMethod.Get, context.ApiHandler.Requests[0].Method);
    }

    /// <summary>
    /// Trace: Market Details Work Item 6, steps 1, 2 and 5.
    /// Verifies: a known current EPIC with no saved observation has its own state and saved-data Reload remains a single Viewer GET.
    /// Expected: the empty-observation panel explains that Reload does not collect from IG.
    /// Why: absence of detail must be discoverable without hiding the deep link or introducing a provider refresh control.
    /// </summary>
    [Fact]
    public void Render_ShouldShowNotCollectedState_WhenNoObservationHasBeenSaved()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateDetail("CS.D.ADAUSD.CFD.IP", includeObservation: false)));

        var cut = context.Render<MarketDetails>(parameters => parameters
            .Add(page => page.CategoryCode, "CRYPTO")
            .Add(page => page.Epic, "CS.D.ADAUSD.CFD.IP"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Not collected", cut.Find("[data-testid='market-detail-coverage']").TextContent, StringComparison.Ordinal);
            Assert.Contains("No saved market observation", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Reload reads saved data only", cut.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("[data-testid='market-detail-snapshot']"));
        });

        var request = Assert.Single(context.ApiHandler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("/market-details", request.RequestUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Details Work Item 6, steps 5 and 6.
    /// Verifies: a stale listing-version conflict is accessible and Reload retries against current saved membership without the stale precondition.
    /// Expected: the initial 409 has an alert and a reload action; the second request succeeds and shows the refreshed saved state.
    /// Why: deep links from an older listing snapshot must not silently display data for a changed membership.
    /// </summary>
    [Fact]
    public void Reload_ShouldRecoverFromStaleListingVersion_WhenServerReturnsConflict()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateProblemResponse(HttpStatusCode.Conflict, new
            {
                title = "Conflict",
                detail = "The listing snapshot changed."
            }),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateDetail("CS.D.ADAUSD.CFD.IP", includeObservation: false)));
        context.NavigationManager.NavigateTo(
            "/market-categories/FX/instruments/CS.D.ADAUSD.CFD.IP/market-details?listingVersion=9");

        var cut = context.Render<MarketDetails>(parameters => parameters
            .Add(page => page.CategoryCode, "FX")
            .Add(page => page.Epic, "CS.D.ADAUSD.CFD.IP"));

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[role='alert'][data-testid='market-details-error']"));
            Assert.NotEmpty(cut.FindAll("[data-testid='market-details-reload-current']"));
        });
        Assert.Contains("listingVersion=9", context.ApiHandler.Requests[0].RequestUri, StringComparison.Ordinal);

        cut.Find("[data-testid='market-details-reload-current']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='market-details-error']"));
            Assert.NotEmpty(cut.FindAll("[data-testid='market-details-no-observation']"));
        });
        Assert.Equal(2, context.ApiHandler.Requests.Count);
        Assert.DoesNotContain("listingVersion=", context.ApiHandler.Requests[1].RequestUri, StringComparison.Ordinal);
        Assert.All(context.ApiHandler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    /// <summary>
    /// Trace: Market Details Work Item 6, step 5.
    /// Verifies: a later saved-detail read failure is announced without discarding an already displayed validated observation.
    /// Expected: the page retains its saved instrument and snapshot while presenting the failure as an alert.
    /// Why: transient SQL/API status failures must not erase useful last-good analysis data.
    /// </summary>
    [Fact]
    public void Reload_ShouldKeepDisplayedObservation_WhenSavedReadFails()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateDetail("CS.D.ADAUSD.CFD.IP")),
            _ => PlatformWebTestData.CreateProblemResponse(HttpStatusCode.ServiceUnavailable, new
            {
                title = "Saved read unavailable",
                detail = "Try again later."
            }));

        var cut = context.Render<MarketDetails>(parameters => parameters
            .Add(page => page.CategoryCode, "FX")
            .Add(page => page.Epic, "CS.D.ADAUSD.CFD.IP"));
        cut.WaitForAssertion(() => Assert.Contains("Cardano ($1)", cut.Markup, StringComparison.Ordinal));

        cut.Find("[data-testid='market-details-reload']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[role='alert'][data-testid='market-details-error']"));
            Assert.Contains("Cardano ($1)", cut.Find("[data-testid='market-detail-instrument']").TextContent, StringComparison.Ordinal);
            Assert.Contains("Saved quote snapshot — not live", cut.Markup, StringComparison.Ordinal);
        });
        Assert.Equal(2, context.ApiHandler.Requests.Count);
    }

    /// <summary>
    /// Trace: Market Details Work Item 6, step 5.
    /// Verifies: a slower response for a prior EPIC cannot overwrite the newest route's saved detail after navigation changes.
    /// Expected: the presenter retains only the response whose route request is current.
    /// Why: rapid route changes must not associate one instrument's terms or price snapshot with another EPIC.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ShouldIgnorePriorEpicResponse_WhenRouteChangesBeforeResponseCompletes()
    {
        var firstResponse = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequestSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = PlatformComponentTestContext.CreateServiceContext(
            "local-viewer",
            null,
            _ =>
            {
                firstRequestSent.TrySetResult(true);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new DelayedJsonContent(firstResponse.Task)
                };
            },
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateDetail("EPIC-NEW")));
        var presenter = context.Services.GetRequiredService<MarketDetailsPagePresenter>();
        using var oldRequest = new CancellationTokenSource();

        var oldLoad = presenter.LoadAsync("FX", "EPIC-OLD", 3, oldRequest.Token);
        await firstRequestSent.Task;
        oldRequest.Cancel();
        await presenter.LoadAsync("FX", "EPIC-NEW", 4, CancellationToken.None);
        firstResponse.SetResult(JsonSerializer.Serialize(
            CreateDetail("EPIC-OLD"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await oldLoad;

        Assert.Equal("EPIC-NEW", presenter.Detail?.Epic);
        Assert.False(presenter.IsLoading);
    }

    /// <summary>
    /// Trace: Market Details Work Item 6, steps 2 and 6.
    /// Verifies: leaving the instruments page for a saved-detail deep link and returning restores the in-memory page and selected EPIC.
    /// Expected: the detail breadcrumb returns to page two with its selected instrument, using the retained opaque cursor.
    /// Why: drill-down navigation should preserve the user's place without exposing cursor contents in the deep link.
    /// </summary>
    [Fact]
    public void Navigation_ShouldRestoreInstrumentPageAndSelection_WhenReturningFromDetails()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateInstrumentPage("opaque+cursor", CreateInstrumentRow("EPIC-1"), CreateInstrumentRow("EPIC-1B"))),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateInstrumentPage(null, CreateInstrumentRow("EPIC-2"))),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateDetail("EPIC-2")),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateInstrumentPage(null, CreateInstrumentRow("EPIC-2"))),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateCategories()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateStatus()));

        var instrumentsPage = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));
        instrumentsPage.WaitForAssertion(() => Assert.Contains("EPIC-1", instrumentsPage.Markup, StringComparison.Ordinal));
        instrumentsPage.FindAll("[role='tab']")[1].Click();
        instrumentsPage.FindAll("button").Single(button => button.TextContent.Contains("Next page", StringComparison.Ordinal)).Click();
        instrumentsPage.WaitForAssertion(() => Assert.Contains("EPIC-2", instrumentsPage.Markup, StringComparison.Ordinal));

        var detailHref = instrumentsPage.Find("a[href*='/market-details']").GetAttribute("href")!;
        Assert.Contains("listingVersion=7", detailHref, StringComparison.Ordinal);
        context.NavigationManager.NavigateTo(detailHref);
        instrumentsPage.Dispose();

        var detailsPage = context.Render<MarketDetails>(parameters => parameters
            .Add(page => page.CategoryCode, "FX")
            .Add(page => page.Epic, "EPIC-2"));
        detailsPage.WaitForAssertion(() => Assert.Contains("Cardano ($1)", detailsPage.Markup, StringComparison.Ordinal));
        var backHref = detailsPage.Find("a[href='/market-categories/FX/instruments']").GetAttribute("href")!;
        context.NavigationManager.NavigateTo(backHref);
        detailsPage.Dispose();

        var restoredPage = context.Render<MarketCategoryInstruments>(parameters =>
            parameters.Add(page => page.CategoryCode, "FX"));
        restoredPage.WaitForAssertion(() =>
        {
            Assert.Contains("Page 2 of 2", restoredPage.Markup, StringComparison.Ordinal);
            Assert.Contains("EPIC-2", restoredPage.Find("[data-testid='market-category-instrument']").TextContent, StringComparison.Ordinal);
        });

        Assert.Contains("cursor=opaque%2Bcursor", context.ApiHandler.Requests[5].RequestUri, StringComparison.Ordinal);
        Assert.Equal(8, context.ApiHandler.Requests.Count);
    }

    private static object CreateDetail(string epic, bool includeObservation = true) => new
    {
        State = includeObservation ? "Complete" : "NotCollected",
        CategoryCode = "FX",
        Epic = epic,
        ListingSnapshotVersion = 7L,
        ListingRetrievedAtUtc = ListingTime,
        Coverage = new
        {
            IsFollowed = true,
            State = includeObservation ? "Complete" : "Partial",
            ExpectedCount = 4,
            CompletedCount = includeObservation ? 4 : 2,
            ExcludedCount = 0,
            OutstandingCount = includeObservation ? 0 : 2,
            LastCompleteAtUtc = includeObservation ? DetailTime.AddMinutes(-1) : (DateTimeOffset?)null,
            NextScheduledCheckUtc = (DateTimeOffset?)null,
            SafeFailureCode = (string?)null
        },
        SavedObservation = includeObservation ? CreateObservation(epic) : null
    };

    private static object CreateObservation(string epic) => new
    {
        RetrievedAtUtc = DetailTime,
        Source = "BulkV2",
        SourceEndpoint = "/markets",
        SourceVersion = 2,
        ProviderUpdateTimeText = "17:56:59",
        Instrument = new
        {
            Epic = epic,
            Expiry = "-",
            Name = "Cardano ($1)",
            MarketId = "ADAUSD",
            Type = "CURRENCIES",
            Unit = "CONTRACTS",
            LotSize = 1m,
            ForceOpenAllowed = true,
            StopsLimitsAllowed = true,
            ControlledRiskAllowed = true,
            StreamingPricesAvailable = true,
            Currencies = new[]
            {
                new { Code = "USD", Symbol = "$", BaseExchangeRate = (decimal?)1.324262m, ExchangeRate = 0.66m, IsDefault = false },
                new { Code = "EUR", Symbol = "€", BaseExchangeRate = (decimal?)null, ExchangeRate = 0m, IsDefault = true }
            },
            MarginDepositBands = new[]
            {
                new { Minimum = 0m, Maximum = new { Presence = "ExplicitNull", Value = (decimal?)null, Unit = "USD" }, Margin = 100m, Currency = "USD" },
                new { Minimum = 1m, Maximum = new { Presence = "Value", Value = (decimal?)10m, Unit = "USD" }, Margin = 100m, Currency = "USD" }
            },
            MarginFactor = 100m,
            MarginFactorUnit = "PERCENTAGE",
            SlippageFactor = Quantity("Value", 100m, "pct"),
            LimitedRiskPremium = Quantity("ExplicitNull", null, "POINTS"),
            SprintMarketsMinimumExpiryTime = Quantity("NotSupplied", null, null),
            SprintMarketsMaximumExpiryTime = Quantity("NotSupplied", null, null),
            OpeningHoursJson = "{\"market\":\"24/7\"}",
            ExpiryDetailsJson = (string?)null,
            RolloverDetailsJson = (string?)null,
            NewsCode = "ADA=",
            ChartCode = (string?)null,
            Country = (string?)null,
            ValueOfOnePip = "1.00",
            OnePipMeans = "0.01",
            ContractSize = "100",
            SpecialInfo = new[] { "<img src=x onerror=alert(1)>", "ADA market notice" }
        },
        DealingRules = new
        {
            ControlledRiskSpacing = Quantity("Value", 5m, "POINTS"),
            MaxStopOrLimitDistance = Quantity("Value", 75m, "PERCENTAGE"),
            MinControlledRiskStopDistance = Quantity("Value", 10m, "PERCENTAGE"),
            MinDealSize = Quantity("Value", 0m, "POINTS"),
            MinNormalStopOrLimitDistance = Quantity("Value", 1m, "POINTS"),
            MinStepDistance = Quantity("Value", 1m, "POINTS"),
            MarketOrderPreference = "AVAILABLE_DEFAULT_OFF",
            TrailingStopsPreference = "NOT_AVAILABLE"
        },
        Snapshot = new
        {
            MarketStatus = "TRADEABLE",
            NetChange = Quantity("Value", 0.64m, null),
            PercentageChange = Quantity("Value", 2.59m, null),
            UpdateTimeText = "17:56:59",
            DelayTime = Quantity("Value", 0m, null),
            Bid = Quantity("Value", 25.33m, null),
            Offer = Quantity("Value", 25.43m, null),
            High = Quantity("Value", 25.90m, null),
            Low = Quantity("Value", 24.60m, null),
            BinaryOdds = Quantity("ExplicitNull", null, null),
            DecimalPlacesFactor = Quantity("Value", 2m, null),
            ScalingFactor = Quantity("Value", 1m, null),
            ControlledRiskExtraSpread = Quantity("ExplicitNull", null, null)
        }
    };

    private static object Quantity(string presence, decimal? value, string? unit) =>
        new { Presence = presence, Value = value, Unit = unit };

    private static object CreateInstrumentPage(string? nextCursor, params object[] instruments) => new
    {
        State = "Complete",
        CategoryCode = "FX",
        SnapshotVersion = 7L,
        LastRetrievedAtUtc = ListingTime,
        Instruments = instruments,
        NextCursor = nextCursor
    };

    private static object CreateInstrumentRow(string epic) => new
    {
        Epic = epic,
        Name = epic,
        InstrumentType = "CURRENCIES",
        UnderlyingName = "ADA/USD",
        Expiry = "-",
        LotSize = 1m,
        OtcTradeable = true,
        ScalingFactor = 1m,
        ExpiryTimestamp = (long?)null,
        MarketStatus = "TRADEABLE",
        DelayTime = 0,
        Bid = 25m,
        Offer = 26m,
        High = 27m,
        Low = 24m,
        NetChange = 1m,
        PercentageChange = 4m,
        UpdateTime = "17:56:59",
        Popularity = 1L,
        MarketDetails = new { State = "NotCollected", DetailRetrievedAtUtc = (DateTimeOffset?)null, SafeFailureCode = (string?)null }
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
                LastSuccessfulCollectionAtUtc = ListingTime,
                Attempts = 1,
                CollectionOutcome = "Complete",
                SafeFailure = (string?)null,
                DetailCoverage = new
                {
                    IsFollowed = true,
                    State = "Partial",
                    ExpectedCount = 2,
                    CompletedCount = 0,
                    ExcludedCount = 0,
                    OutstandingCount = 2,
                    LastCompleteAtUtc = (DateTimeOffset?)null,
                    NextScheduledCheckUtc = (DateTimeOffset?)null,
                    SafeFailureCode = (string?)null
                }
            }
        },
        LastRefreshedAtUtc = ListingTime,
        InterestRevision = 1,
        AppliedBrokerEnvironment = "Demo",
        DormantInterestedCategories = Array.Empty<string>()
    };

    private static object CreateStatus() => new
    {
        State = "Available",
        BrokerEnvironment = "Demo",
        TradingDay = DateOnly.FromDateTime(ListingTime.UtcDateTime),
        IsDue = false,
        CurrentSlot = (int?)null,
        NextWakeUpUtc = (DateTimeOffset?)null,
        PauseReason = (string?)null,
        LastCategoryRefreshAtUtc = ListingTime,
        LastCompletedSlot = 0,
        CycleOutcome = "Complete",
        CategoryPrerequisiteOutcome = "Complete",
        SafeCategoryFailure = (string?)null,
        UsedRequestBudget = 1,
        ApprovedDailyRequestAllowance = 10,
        Categories = new[]
        {
            new
            {
                CategoryCode = "FX",
                LastSuccessfulCollectionAtUtc = ListingTime,
                Attempts = 1,
                Outcome = "Complete",
                SafeFailure = (string?)null,
                DetailCoverage = (object?)null
            }
        }
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
