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

    /// <summary>Trace: FR account details. Verifies source-ordered type tabs group two CFD accounts, expose the tab-list name, and render the requested fields without per-account tabs.</summary>
    [Fact]
    public void Render_ShouldGroupAccountsIntoSourceOrderedTypeTabs_WhenRetrievalContainsMultipleAccountTypes()
    {
        var retrieval = CreateRetrieval(Account("CFD-1", "Primary", "Main", "Enabled", "CFD", 100m), Account("CFD-2", "Secondary", "Second", "Disabled", "CFD", 200m), Account("SB-1", "Spreadbet account", "Spread", "Enabled", "Spreadbet", 300m));
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new { Retrieval = retrieval, OlderCursor = "older-token", NewerCursor = (string?)null }));
        var cut = context.RenderComponent<AccountDetails>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Primary", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Balance", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("CFD-1", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Main", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Enabled", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Preferred", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Transfer from", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Transfer to", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Currency", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Deposit", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Profit/loss", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Available", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(["CFD", "Spreadbet"], cut.FindAll("[role='tab']").Select(tab => tab.TextContent.Trim()).ToArray());
            Assert.Contains("Account types", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(2, cut.FindAll("[data-testid='account-details-account']").Count);
            Assert.DoesNotContain("Spreadbet account", cut.Find("[data-testid='account-details-panel']").TextContent, StringComparison.Ordinal);
            Assert.True(cut.Find("button[aria-label='View newer account details']").HasAttribute("disabled"));
            Assert.False(cut.Find("button[aria-label='View older account details']").HasAttribute("disabled"));
        });
    }

    /// <summary>Trace: FR account details layout. Verifies retrieval metadata and the account type tabs retain stable page-local hooks so spacing and tab styling remain scoped without changing tab behavior.</summary>
    [Fact]
    public void Render_ShouldExposeAccountDetailsLayoutHooks_WhenRetrievalContainsAnAccountType()
    {
        using var context = new PlatformComponentTestContext("local-viewer", null,
            _ => Response(CreateRetrieval(Account("CFD-1", "Primary", "Main", "Enabled", "CFD", 100m))));
        var cut = context.RenderComponent<AccountDetails>();

        cut.WaitForAssertion(() =>
        {
            var retrieval = cut.Find("dl.platform-key-value-grid.account-details-retrieval");
            var tabs = cut.Find(".account-details-tabs");

            Assert.Contains("platform-key-value-grid", retrieval.GetAttribute("class"), StringComparison.Ordinal);
            Assert.Contains("account-details-retrieval", retrieval.GetAttribute("class"), StringComparison.Ordinal);
            Assert.NotEmpty(tabs.QuerySelectorAll(".rz-tabview"));
        });
    }

    /// <summary>Trace: FR account details. Verifies selecting a visible type tab renders only that type and keeps metadata before tabs and history navigation after all tab panels.</summary>
    [Fact]
    public void Render_ShouldShowOnlySelectedAccountType_WhenSpreadbetTabIsActivated()
    {
        var retrieval = CreateRetrieval(Account("CFD-1", "Primary", "Main", "Enabled", "CFD", 100m), Account("SB-1", "Spreadbet account", "Spread", "Enabled", "Spreadbet", 300m));
        using var context = new PlatformComponentTestContext("local-viewer", null, _ => Response(retrieval, "older-token"));
        var cut = context.RenderComponent<AccountDetails>();
        cut.WaitForAssertion(() => cut.FindAll("[role='tab']").Single(tab => tab.TextContent.Trim() == "Spreadbet").Click());
        var panel = cut.Find("[data-testid='account-details-panel']");
        Assert.DoesNotContain("Primary", panel.TextContent, StringComparison.Ordinal);
        Assert.Contains("Spreadbet account", panel.TextContent, StringComparison.Ordinal);
        Assert.True(cut.Markup.IndexOf("Retrieved at", StringComparison.Ordinal) < cut.Markup.IndexOf("CFD", StringComparison.Ordinal));
        Assert.True(cut.Markup.IndexOf("data-testid=\"account-details-account\"", StringComparison.Ordinal) < cut.Markup.IndexOf("View older account details", StringComparison.Ordinal));
    }

    /// <summary>Trace: FR account details. Verifies casing-insensitive grouping and selection retention after refresh, older, and newer page-local retrieval replacements.</summary>
    [Fact]
    public void Selection_ShouldRetainNormalizedType_WhenRetrievalIsReplacedByRefresh()
    {
        var initial = CreateRetrieval(Account("CFD-1", "Initial CFD", "One", "Enabled", "cfd", 1m), Account("SB-1", "Initial Spreadbet", "Spread", "Enabled", "SpreadBet", 2m));
        var refreshed = CreateRetrieval(Account("CFD-2", "Refreshed CFD", "Two", "Enabled", "CFD", 3m), Account("SB-2", "Refreshed Spreadbet", "Spread", "Enabled", "spreadbet", 4m));
        using var context = new PlatformComponentTestContext("local-operator", null, _ => Response(initial), _ => RefreshResponse(refreshed));
        var cut = context.RenderComponent<AccountDetails>();
        cut.WaitForAssertion(() => cut.FindAll("[role='tab']").Single(tab => tab.TextContent.Trim() == "SpreadBet").Click());
        cut.Find("button.platform-button").Click();
        cut.WaitForAssertion(() => Assert.Contains("Refreshed Spreadbet", cut.Find("[data-testid='account-details-panel']").TextContent, StringComparison.Ordinal));
    }

    /// <summary>Trace: FR account history. Verifies selected Account type survives older and newer retrieval navigation.</summary>
    [Fact]
    public void Selection_ShouldRetainNormalizedType_WhenOlderAndNewerPagesAreLoaded()
    {
        var initial = CreateRetrieval(Account("CFD-1", "Initial CFD", "One", "Enabled", "CFD", 1m), Account("SB-1", "Initial Spreadbet", "Spread", "Enabled", "Spreadbet", 2m));
        var older = CreateRetrieval(Account("CFD-2", "Older CFD", "Two", "Enabled", "CFD", 3m), Account("SB-2", "Older Spreadbet", "Spread", "Enabled", "spreadbet", 4m));
        var newer = CreateRetrieval(Account("CFD-3", "Newer CFD", "Three", "Enabled", "CFD", 5m), Account("SB-3", "Newer Spreadbet", "Spread", "Enabled", "SPREADBET", 6m));
        using var context = new PlatformComponentTestContext("local-viewer", null, _ => Response(initial, "older-token", "newer-token"), _ => Response(older, null, "newer-token"), _ => Response(newer, "older-token"));
        var cut = context.RenderComponent<AccountDetails>();
        cut.WaitForAssertion(() => cut.FindAll("[role='tab']").Single(tab => tab.TextContent.Trim() == "Spreadbet").Click());
        cut.Find("button[aria-label='View older account details']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Older Spreadbet", cut.Find("[data-testid='account-details-panel']").TextContent, StringComparison.Ordinal));
        cut.Find("button[aria-label='View newer account details']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Newer Spreadbet", cut.Find("[data-testid='account-details-panel']").TextContent, StringComparison.Ordinal));
    }

    /// <summary>Trace: FR account details. Verifies selection falls back to the first source-ordered type when the selected normalized key disappears after refresh.</summary>
    [Fact]
    public void Selection_ShouldFallBackToFirstType_WhenSelectedTypeDisappearsAfterRefresh()
    {
        var initial = CreateRetrieval(Account("CFD-1", "Initial CFD", "One", "Enabled", "CFD", 1m), Account("SB-1", "Initial Spreadbet", "Spread", "Enabled", "Spreadbet", 2m));
        var replacement = CreateRetrieval(Account("CFD-2", "Replacement CFD", "Two", "Enabled", "CFD", 3m));
        using var context = new PlatformComponentTestContext("local-operator", null, _ => Response(initial), _ => RefreshResponse(replacement));
        var cut = context.RenderComponent<AccountDetails>();
        cut.WaitForAssertion(() => cut.FindAll("[role='tab']").Single(tab => tab.TextContent.Trim() == "Spreadbet").Click());
        cut.Find("button.platform-button").Click();
        cut.WaitForAssertion(() => Assert.Contains("Replacement CFD", cut.Find("[data-testid='account-details-panel']").TextContent, StringComparison.Ordinal));
        Assert.Contains("CFD", cut.Find("[role='tab']").TextContent, StringComparison.Ordinal);
    }

    /// <summary>Trace: FR account refresh. Verifies a failed Operator refresh leaves the saved retrieval rendered and exposes exactly one user-visible error.</summary>
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
            Assert.Single(cut.FindAll("[data-testid='account-details-error']"));
        });
    }

    private static TestAccount Account(string id, string name, string? alias, string status, string type, decimal balance) =>
        new(id, name, alias, status, type, true, balance, 20m, 5m, 80m, "GBP", true, false);

    private static TestRetrieval CreateRetrieval(params TestAccount[] accounts) =>
        new(Guid.NewGuid(), DateTimeOffset.Parse("2026-08-08T12:00:00+00:00"), new DateOnly(2026, 8, 8), "Manual", accounts);

    private static HttpResponseMessage Response(TestRetrieval retrieval, string? older = null, string? newer = null) =>
        PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new
        {
            Retrieval = new
            {
                retrieval.RetrievalId,
                retrieval.RetrievedAtUtc,
                retrieval.TradingDay,
                retrieval.TriggerSource,
                Accounts = retrieval.Accounts.Select(account => new
                {
                    account.AccountId,
                    account.AccountName,
                    account.AccountAlias,
                    account.Status,
                    account.AccountType,
                    account.IsPreferred,
                    account.Balance,
                    account.Deposit,
                    account.ProfitLoss,
                    account.Available,
                    account.Currency,
                    account.CanTransferFrom,
                    account.CanTransferTo
                }).ToArray()
            },
            OlderCursor = older,
            NewerCursor = newer
        });

    private static HttpResponseMessage RefreshResponse(TestRetrieval retrieval) =>
        PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new
        {
            retrieval.RetrievalId,
            retrieval.RetrievedAtUtc,
            retrieval.TradingDay,
            retrieval.TriggerSource,
            Accounts = retrieval.Accounts.Select(account => new
            {
                account.AccountId,
                account.AccountName,
                account.AccountAlias,
                account.Status,
                account.AccountType,
                account.IsPreferred,
                account.Balance,
                account.Deposit,
                account.ProfitLoss,
                account.Available,
                account.Currency,
                account.CanTransferFrom,
                account.CanTransferTo
            }).ToArray()
        });

    private sealed record TestAccount(string AccountId, string AccountName, string? AccountAlias, string Status, string AccountType, bool IsPreferred, decimal Balance, decimal Deposit, decimal ProfitLoss, decimal Available, string Currency, bool CanTransferFrom, bool CanTransferTo);

    private sealed record TestRetrieval(Guid RetrievalId, DateTimeOffset RetrievedAtUtc, DateOnly TradingDay, string TriggerSource, TestAccount[] Accounts);
}