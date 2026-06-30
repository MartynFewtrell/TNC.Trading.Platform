using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class StatusTests
{
    [Fact]
    public async Task LoadAsync_ShouldReturnStatusAndEvents_WhenProtectedStatusLoads()
    {
        using var context = PlatformComponentTestContext.CreateServiceContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));
        var presenter = context.Services.GetRequiredService<StatusPagePresenter>();

        var result = await presenter.LoadAsync(CancellationToken.None);

        Assert.NotNull(result.Status);
        Assert.NotNull(result.Events);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task TriggerManualRetryAsync_ShouldReturnStatusMessage_WhenRetryStartsSuccessfully()
    {
        var retry = PlatformWebTestData.CreateManualRetry();
        using var context = PlatformComponentTestContext.CreateServiceContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.Accepted, retry));
        var presenter = context.Services.GetRequiredService<StatusPagePresenter>();

        var result = await presenter.TriggerManualRetryAsync(CancellationToken.None);

        Assert.Equal($"Manual retry started with cycle id {retry.RetryCycleId}.", result.Message);
    }

    [Fact]
    public void GetCurrentIgLoginState_ShouldReturnRetrying_WhenRetryCycleIsActive()
    {
        var status = PlatformWebTestData.CreateStatus(isDegraded: true, retryPhase: "InitialAutomatic");

        var result = StatusPagePresenter.GetCurrentIgLoginState(status);

        Assert.Equal("Retrying", result);
    }

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

    /// <summary>
    /// Trace: FR5, FR6, NF2, NF5, TR4, TR8.
    /// Verifies: the status page renders the current IG login state together with the expandable latest successful non-secret payload details.
    /// Expected: the page shows the active current-state label, the latest account summary fields, and the formatted stored payload content.
    /// Why: operators need a clear current-state view and an on-page path to inspect the latest successful login payload without another read flow.
    /// </summary>
    [Fact]
    public void Render_ShouldShowLatestIgLoginPayloadDetails_WhenStatusIncludesLatestSnapshot()
    {
        var latestSnapshot = PlatformWebTestData.CreateLatestSnapshot();
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus(latestSnapshot: latestSnapshot)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));

        var cut = context.RenderComponent<Status>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Active", cut.Find("[data-testid='ig-current-state-value']").TextContent.Trim());
            Assert.Contains(latestSnapshot.CurrentAccountId, cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Latest successful IG login payload details", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("configured-demo-session", cut.Find("[data-testid='ig-latest-payload-json']").TextContent, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: FR6, FR10, NF5, TR8, TR10.
    /// Verifies: the status page labels an in-schedule degraded state with an active retry cycle as retrying instead of failed or out of schedule.
    /// Expected: the rendered IG current-state label is `Retrying` and the retry context explains the scheduled retry attempt.
    /// Why: the operator must be able to distinguish transient retry behavior from a hard failure when watching `/status`.
    /// </summary>
    [Fact]
    public void Render_ShouldShowRetryingState_WhenRetryCycleIsActive()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus(isDegraded: true, retryPhase: "InitialAutomatic")),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));

        var cut = context.RenderComponent<Status>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Retrying", cut.Find("[data-testid='ig-current-state-value']").TextContent.Trim());
            Assert.Contains("InitialAutomatic", cut.Find("[data-testid='ig-retry-context-value']").TextContent, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: FR6, FR10, NF5, TR8, TR10.
    /// Verifies: the status page labels an inactive schedule state distinctly from failed login handling.
    /// Expected: the rendered IG current-state label is `Out of schedule` and the schedule context is shown as out of schedule.
    /// Why: the operator must not confuse intentional schedule inactivity with an authentication failure.
    /// </summary>
    [Fact]
    public void Render_ShouldShowOutOfScheduleState_WhenTradingScheduleIsInactive()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus(isScheduleActive: false, sessionStatus: "OutOfSchedule")),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));

        var cut = context.RenderComponent<Status>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Out of schedule", cut.Find("[data-testid='ig-current-state-value']").TextContent.Trim());
            Assert.Equal("Out of schedule", cut.Find("[data-testid='ig-schedule-context-value']").TextContent.Trim());
        });
    }
}
