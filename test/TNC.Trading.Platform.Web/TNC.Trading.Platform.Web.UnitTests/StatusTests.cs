using System.Net;
using Bunit;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class StatusTests
{
    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the status page surfaces the degraded authentication warning when the protected status payload reports degraded auth state.
    /// Expected: the warning callout is rendered with the documented degraded-auth message.
    /// Why: operator-visible degraded state should be covered directly in component tests before relying on slower functional or E2E flows.
    /// </summary>
    [Fact]
    public void Render_ShouldShowDegradedWarning_WhenAuthenticationStateIsDegraded()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus(isDegraded: true, blockedReason: "Retry limit reached")),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));

        var cut = context.RenderComponent<Status>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Auth-dependent actions are blocked until the platform can restore an IG demo session.", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("data-testid=\"auth-degraded-warning\"", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the status page hides the operator-only manual retry action when the current operator only holds viewer access.
    /// Expected: the rendered page does not include the manual retry button for a viewer session.
    /// Why: lower-level coverage should prove the role-boundary behavior of the refreshed status page without another distributed auth run.
    /// </summary>
    [Fact]
    public void Render_ShouldHideManualRetryButton_WhenCurrentOperatorIsViewerOnly()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));

        var cut = context.RenderComponent<Status>();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='manual-retry-button']")));
    }

    /// <summary>
    /// Trace: FR3, NF2, TR1, OR1.
    /// Verifies: the status page disables the manual retry action when the protected retry state reports that manual retry is unavailable.
    /// Expected: the operator-visible retry button renders in the disabled state.
    /// Why: the operator UI must reflect retry availability directly from the protected status contract.
    /// </summary>
    [Fact]
    public void Render_ShouldDisableManualRetryButton_WhenManualRetryIsUnavailable()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus(manualRetryAvailable: false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));

        var cut = context.RenderComponent<Status>();

        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid='manual-retry-button']").HasAttribute("disabled")));
    }

    /// <summary>
    /// Trace: FR3, NF2, TR1, OR1.
    /// Verifies: the status page shows the operator-facing retry message after the protected manual-retry endpoint accepts the request.
    /// Expected: selecting the retry button renders the returned retry cycle identifier in the page status message.
    /// Why: the refreshed status experience should be validated directly at the component level before trimming higher-level auth duplication.
    /// </summary>
    [Fact]
    public void TriggerManualRetry_ShouldShowStatusMessage_WhenRetryStartsSuccessfully()
    {
        var retry = PlatformWebTestData.CreateManualRetry();
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.Accepted, retry),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));

        var cut = context.RenderComponent<Status>();
        cut.WaitForElement("[data-testid='manual-retry-button']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(retry.RetryCycleId.ToString(), cut.Find("[data-testid='manual-retry-message']").TextContent, StringComparison.Ordinal));
    }
}
