using System.Net;
using Bunit;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class AccountDetailsTests
{
    /// <summary>Trace: FR account history. Verifies the Viewer page gives an explicit empty state when no snapshot is saved.</summary>
    [Fact]
    public void Render_ShouldShowEmptyState_WhenNoRetrievalIsSaved()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new { Retrieval = (object?)null, OlderCursor = (string?)null, NewerCursor = (string?)null }));
        var cut = context.RenderComponent<AccountDetails>();
        cut.WaitForAssertion(() => Assert.Contains("No saved account details are available yet", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>Trace: FR account details. Verifies identity, state, transfer flags, currency, and all four balances render for the newest snapshot.</summary>
    [Fact]
    public void Render_ShouldShowAccountFieldsAndDisableNewer_WhenNewestRetrievalIsLoaded()
    {
        var account = new { AccountId = "ABC", AccountName = "Primary", AccountAlias = "Main", Status = "Enabled", AccountType = "CFD", IsPreferred = true, Balance = 100m, Deposit = 20m, ProfitLoss = 5m, Available = 80m, Currency = "GBP", CanTransferFrom = true, CanTransferTo = false };
        var retrieval = new { RetrievalId = Guid.NewGuid(), RetrievedAtUtc = DateTimeOffset.UtcNow, TradingDay = DateOnly.FromDateTime(DateTime.UtcNow), TriggerSource = "Manual", Accounts = new[] { account } };
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new { Retrieval = retrieval, OlderCursor = "older-token", NewerCursor = (string?)null }));
        var cut = context.RenderComponent<AccountDetails>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Primary", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Balance", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("100", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Currency", cut.Markup, StringComparison.Ordinal);
            Assert.True(cut.Find("button[aria-label='View newer account details']").HasAttribute("disabled"));
            Assert.False(cut.Find("button[aria-label='View older account details']").HasAttribute("disabled"));
            Assert.Empty(cut.FindAll("button.platform-button"));
        });
    }

    /// <summary>Trace: FR account refresh. Verifies a failed Operator refresh leaves the saved retrieval rendered and exposes an error.</summary>
    [Fact]
    public void Refresh_ShouldPreserveSavedRetrieval_WhenRefreshFails()
    {
        var retrieval = new { RetrievalId = Guid.NewGuid(), RetrievedAtUtc = DateTimeOffset.UtcNow, TradingDay = DateOnly.FromDateTime(DateTime.UtcNow), TriggerSource = "Automatic", Accounts = new[] { new { AccountId = "ABC", AccountName = "Saved account", AccountAlias = (string?)null, Status = "Enabled", AccountType = "CFD", IsPreferred = true, Balance = 1m, Deposit = 2m, ProfitLoss = 3m, Available = 4m, Currency = "GBP", CanTransferFrom = true, CanTransferTo = true } } };
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new { Retrieval = retrieval, OlderCursor = (string?)null, NewerCursor = (string?)null }),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.BadGateway, new { }));
        var cut = context.RenderComponent<AccountDetails>();
        cut.WaitForAssertion(() => Assert.Contains("Saved account", cut.Markup, StringComparison.Ordinal));
        cut.Find("button.platform-button").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Saved account", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Unable to refresh account details", cut.Markup, StringComparison.Ordinal);
        });
    }
}