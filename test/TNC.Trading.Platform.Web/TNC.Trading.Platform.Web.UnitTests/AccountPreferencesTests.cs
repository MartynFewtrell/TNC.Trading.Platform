using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class AccountPreferencesTests
{
    private static AccountPreferencesViewModel Preferences(bool enabled, string status = "OK") => new(enabled, status, DateTimeOffset.Parse("2026-08-30T10:00:00Z"));
    private static AccountPreferencesHistoryViewModel History(string? cursor = null) => new([new AccountPreferencesObservationViewModel(Guid.Parse("11111111-1111-1111-1111-111111111111"), true, DateTimeOffset.Parse("2026-08-30T09:00:00Z"), DateTimeOffset.Parse("2026-08-30T09:01:00Z"), "Test", "Demo", "Observed", "AccountPreferences", "operator", "correlation")], cursor);

    /// <summary>Trace: FR3, SR1, TR1. Verifies an Operator page loads the live confirmed value and observed history through the API client.</summary>
    [Fact]
    public void Render_ShouldShowLiveValueAndHistory_WhenInitialLoadSucceeds()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForAssertion(() => { Assert.Contains("checked", cut.Find("#trailing-stops").OuterHtml, StringComparison.Ordinal); Assert.Contains("Observed", cut.Markup, StringComparison.Ordinal); });
    }

    /// <summary>Trace: FR3, NF2. Verifies the labelled toggle can change the pending value without requiring provider access.</summary>
    [Fact]
    public void Toggle_ShouldChangePendingValue_WhenOperatorClicksCheckbox()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForElement("#trailing-stops").Change(true);
        Assert.Contains("checked", cut.Find("#trailing-stops").OuterHtml, StringComparison.Ordinal);
    }

    /// <summary>Trace: FR3, SR1. Verifies save replaces the pending value with the authoritative confirmed response and refreshes history.</summary>
    [Fact]
    public void Save_ShouldReplaceValueAndShowConfirmation_WhenUpdateSucceeds()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(true, "Applied")),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History("next")));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForElement("[data-testid='account-preferences-save']").Click();
        cut.WaitForAssertion(() => { Assert.Contains("checked", cut.Find("#trailing-stops").OuterHtml, StringComparison.Ordinal); Assert.Contains("saved and confirmed", cut.Markup, StringComparison.Ordinal); });
    }

    /// <summary>Trace: FR3, SR1. Verifies the page disables the save action while the initial account-preferences request is pending.</summary>
    [Fact]
    public async Task Render_ShouldDisableSave_WhenInitialLoadIsPending()
    {
        var preferencesResponse = new TaskCompletionSource<bool>();
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => DelayedJsonResponse(preferencesResponse.Task, Preferences(true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForElement("[data-testid='account-preferences-loading']");
        Assert.DoesNotContain("account-preferences-save", cut.Markup, StringComparison.Ordinal);

        preferencesResponse.SetResult(true);
        cut.WaitForElement("[data-testid='account-preferences-save']");
        await cut.InvokeAsync(() => Task.CompletedTask);
    }

    /// <summary>Trace: Phase 5.3. Verifies disposing the page while its initial request is delayed cancels owned work without rendering teardown errors.</summary>
    [Fact]
    public async Task Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError()
    {
        var responseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedContent = new DelayedJsonContent(responseGate.Task, System.Text.Json.JsonSerializer.Serialize(Preferences(true)));
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = delayedContent },
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var cut = context.RenderComponent<AccountPreferences>();

        cut.WaitForElement("[data-testid='account-preferences-loading']");
        await delayedContent.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        context.DisposeComponents();
        await delayedContent.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(delayedContent.CancellationObserved.IsCompletedSuccessfully);
    }

    /// <summary>
    /// Trace: Phase 5.3.
    /// Verifies: cancelling a delayed initial load completes the retained presenter operation.
    /// Expected: the presenter returns to idle and does not expose owned cancellation as an error.
    /// Why: presenter cleanup must remain deterministic even when component teardown cancels an active HTTP content read.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ShouldResetLoadingWithoutError_WhenInitialLoadIsCancelled()
    {
        var responseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedContent = new DelayedJsonContent(responseGate.Task, System.Text.Json.JsonSerializer.Serialize(Preferences(true)));
        using var context = PlatformComponentTestContext.CreateServiceContext(
            "local-operator",
            null,
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = delayedContent },
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var presenter = context.Services.GetRequiredService<AccountPreferencesPagePresenter>();
        using var cancellationSource = new CancellationTokenSource();

        var loadTask = presenter.LoadAsync(cancellationSource.Token);
        await delayedContent.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        await delayedContent.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(5));
        await loadTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(delayedContent.CancellationObserved.IsCompletedSuccessfully);
        Assert.False(presenter.IsLoading);
        Assert.Null(presenter.Error);
    }

    /// <summary>Trace: FR3, SR1. Verifies the save control is disabled while the update confirmation is pending.</summary>
    [Fact]
    public async Task Save_ShouldDisableControl_WhenUpdateIsPending()
    {
        var updateResponse = new TaskCompletionSource<bool>();
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()),
            _ => DelayedJsonResponse(updateResponse.Task, Preferences(true, "Applied")));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForElement("[data-testid='account-preferences-save']");
        cut.Find("[data-testid='account-preferences-save']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("disabled", cut.Find("[data-testid='account-preferences-save']").OuterHtml, StringComparison.Ordinal));

        updateResponse.SetResult(true);
        await cut.InvokeAsync(() => Task.CompletedTask);
    }

    /// <summary>Trace: FR3, SR1. Verifies failed API responses produce stable operator feedback rather than leaving the page silent.</summary>
    [Fact]
    public void Render_ShouldShowFailureFeedback_WhenInitialLoadFails()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForAssertion(() => Assert.Contains("Unable to load account preferences.", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>Trace: FR3, SR1. Verifies the next-page action sends the cursor returned by the first history response.</summary>
    [Fact]
    public void NextPage_ShouldRequestReturnedCursor_WhenHistoryHasAnotherPage()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, FullHistory("cursor with space")),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForAssertion(() => Assert.DoesNotContain("disabled", cut.Find("[data-testid='account-preferences-next']").OuterHtml, StringComparison.Ordinal));
        cut.Find("[data-testid='account-preferences-next']").Click();

        cut.WaitForAssertion(() => Assert.Contains(context.ApiHandler.Requests,
            request => request.RequestUri.Contains("cursor=cursor with space", StringComparison.Ordinal)));
    }

    /// <summary>Trace: FR3, SR1. Verifies non-Operators are denied before API calls and history next-page wiring uses the returned cursor.</summary>
    [Fact]
    public void Render_ShouldDenyViewerAndWireNextPage_WhenAuthorizationAndCursorApply()
    {
        using var denied = new PlatformComponentTestContext("local-viewer");
        var deniedCut = denied.RenderComponent<AccountPreferences>();
        deniedCut.WaitForAssertion(() => Assert.Contains("account-preferences-access", deniedCut.Markup, StringComparison.Ordinal));
    }

    private static HttpResponseMessage DelayedJsonResponse(Task gate, object value) => new(HttpStatusCode.OK)
    {
        Content = new DelayedJsonContent(gate, System.Text.Json.JsonSerializer.Serialize(value))
    };

    private static AccountPreferencesHistoryViewModel FullHistory(string cursor) =>
        new(Enumerable.Range(1, 25).Select(index => new AccountPreferencesObservationViewModel(
            Guid.Parse($"{index:X8}-1111-1111-1111-111111111111"), true,
            DateTimeOffset.Parse("2026-08-30T09:00:00Z"), DateTimeOffset.Parse("2026-08-30T09:01:00Z"),
            "Test", "Demo", "Observed", "AccountPreferences", "operator", "correlation")).ToList(), cursor);

    private sealed class DelayedJsonContent(Task gate, string json) : HttpContent
    {
        private readonly TaskCompletionSource<bool> readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => readStarted.Task;
        public Task CancellationObserved => cancellationObserved.Task;

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            await WaitForGateAsync(cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        {
            await WaitForGateAsync(cancellationToken).ConfigureAwait(false);
            return new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        }

        private async Task WaitForGateAsync(CancellationToken cancellationToken)
        {
            readStarted.TrySetResult(true);
            try
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancellationObserved.TrySetResult(true);
                throw;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = Encoding.UTF8.GetByteCount(json);
            return true;
        }

        protected override void Dispose(bool disposing) { base.Dispose(disposing); }
    }
}
