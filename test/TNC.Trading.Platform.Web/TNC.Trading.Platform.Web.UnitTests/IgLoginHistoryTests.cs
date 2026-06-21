using System.Net;
using Bunit;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class IgLoginHistoryTests
{
    /// <summary>
    /// Trace: FR7, NF5, TR5, TR7.
    /// Verifies: the IG login history page shows an empty-state message when the API returns an empty retained snapshot list.
    /// Expected: the rendered page contains the empty-state element and does not render any history entry elements.
    /// Why: operators need a clear signal that no daily history has been retained yet rather than an ambiguous blank surface.
    /// </summary>
    [Fact]
    public void Render_ShouldShowEmptyState_WhenNoRetainedSnapshotsAreAvailable()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new { RetainedSnapshots = Array.Empty<object>() }));

        var cut = context.RenderComponent<IgLoginHistory>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='ig-login-history-empty']"));
            Assert.Empty(cut.FindAll("[data-testid='ig-login-history-entry']"));
        });
    }

    /// <summary>
    /// Trace: FR7, NF2, NF5, SR2, TR5, TR7.
    /// Verifies: the IG login history page renders a retained snapshot entry with trading day and account information when the API returns history.
    /// Expected: the page shows one or more entry elements containing the snapshot account identifier and trading day.
    /// Why: the history page is the operator's primary surface for reviewing retained daily login payloads and must accurately present the stored data.
    /// </summary>
    [Fact]
    public void Render_ShouldShowRetainedSnapshots_WhenApiReturnsHistory()
    {
        var snapshot = PlatformWebTestData.CreateHistorySnapshot();
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new { RetainedSnapshots = new[] { snapshot } }));

        var cut = context.RenderComponent<IgLoginHistory>();

        cut.WaitForAssertion(() =>
        {
            var entries = cut.FindAll("[data-testid='ig-login-history-entry']");
            Assert.NotEmpty(entries);
            Assert.Contains(snapshot.CurrentAccountId, cut.Markup, StringComparison.Ordinal);
            Assert.Contains(snapshot.TradingDay.ToString(), cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: FR7, NF2, SR2, SR4, TR5.
    /// Verifies: the IG login history page renders an error message when the API call fails.
    /// Expected: the page shows an error callout and does not render the empty-state or entry panels.
    /// Why: API failures should surface clearly to operators so they know the displayed data may be incomplete.
    /// </summary>
    [Fact]
    public void Render_ShouldShowLoadError_WhenApiRequestFails()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.InternalServerError, new { }));

        var cut = context.RenderComponent<IgLoginHistory>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unable to load IG login history", cut.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("[data-testid='ig-login-history-panel']"));
        });
    }

    /// <summary>
    /// Trace: FR7, NF2, NF5, SR2, TR5, TR7.
    /// Verifies: retained snapshot entries expose expandable details including response headers and formatted raw payload JSON.
    /// Expected: the rendered entry contains the Lightstreamer endpoint, the response header key, and the formatted JSON payload.
    /// Why: operators use the history page to inspect previous broker responses for troubleshooting, so the non-secret fields must be present and legible.
    /// </summary>
    [Fact]
    public void Render_ShouldIncludeExpandablePayloadDetails_InRetainedSnapshotEntry()
    {
        var snapshot = PlatformWebTestData.CreateHistorySnapshot();
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new { RetainedSnapshots = new[] { snapshot } }));

        var cut = context.RenderComponent<IgLoginHistory>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Version", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(snapshot.LightstreamerEndpoint, cut.Markup, StringComparison.Ordinal);
            Assert.Contains("ig-history-entry-payload-json", cut.Markup, StringComparison.Ordinal);
        });
    }
}
