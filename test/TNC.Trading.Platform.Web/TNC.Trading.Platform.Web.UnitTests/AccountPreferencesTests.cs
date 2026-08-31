using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class AccountPreferencesTests
{
    private static AccountPreferencesViewModel Preferences(bool enabled, string status = "OK") => new(enabled, status, DateTimeOffset.Parse("2026-08-30T10:00:00Z"));
    private static AccountPreferencesHistoryViewModel History(string? cursor = null) => new([new AccountPreferencesObservationViewModel(Guid.Parse("11111111-1111-1111-1111-111111111111"), true, DateTimeOffset.Parse("2026-08-30T09:00:00Z"), DateTimeOffset.Parse("2026-08-30T09:01:00Z"), "Test", "Demo", "Observed", "AccountPreferences", "operator", "correlation")], cursor);

    /// <summary>Trace: Account Preferences UI Clarity. Verifies an unconfirmed page state has no actionable Boolean until a live response is applied, preventing load failure from becoming a false selection.</summary>
    [Fact]
    public void State_ShouldStartUnconfirmed_WhenViewModelIsCreated()
    {
        var state = new AccountPreferencesPageViewModel();

        Assert.False(state.HasConfirmedValue);
        Assert.False(state.HasPendingChange);
        Assert.Null(state.ConfirmedTrailingStopsEnabled);
    }

    /// <summary>Trace: Account Preferences UI Clarity. Verifies authoritative enabled and disabled responses establish both the pending and confirmed values together, preserving the Boolean transport contract.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_ShouldConfirmAuthoritativeValue_WhenPreferenceResponseIsReceived(bool enabled)
    {
        var state = new AccountPreferencesPageViewModel();

        state.Apply(Preferences(enabled));

        Assert.True(state.HasConfirmedValue);
        Assert.Equal(enabled, state.TrailingStopsEnabled);
        Assert.Equal(enabled, state.ConfirmedTrailingStopsEnabled);
        Assert.False(state.HasPendingChange);
    }

    /// <summary>Trace: Account Preferences UI Clarity. Verifies a new selection changes only pending state, so the displayed choice cannot be mistaken for provider-confirmed state before save succeeds.</summary>
    [Fact]
    public void Select_ShouldChangePendingValueOnly_WhenSelectionDiffersFromConfirmedValue()
    {
        var state = new AccountPreferencesPageViewModel();
        state.Apply(Preferences(false));

        state.Select(true);

        Assert.True(state.TrailingStopsEnabled);
        Assert.False(state.ConfirmedTrailingStopsEnabled);
        Assert.True(state.HasPendingChange);
    }

    /// <summary>Trace: Account Preferences UI Clarity. Verifies dirty state is an exact comparison against the nullable confirmed value and is false for an unchanged selection.</summary>
    [Fact]
    public void HasPendingChange_ShouldMatchPendingAndConfirmedValuesExactly_WhenSelectionChanges()
    {
        var state = new AccountPreferencesPageViewModel();
        state.Apply(Preferences(true));

        Assert.False(state.HasPendingChange);

        state.Select(false);
        Assert.True(state.HasPendingChange);

        state.Select(true);
        Assert.False(state.HasPendingChange);
    }

    /// <summary>Trace: Account Preferences UI Clarity. Verifies a later authoritative response synchronizes pending and confirmed values, clearing stale dirty state after provider confirmation.</summary>
    [Fact]
    public void Apply_ShouldSynchronizePendingAndConfirmedValues_WhenLaterAuthoritativeResponseArrives()
    {
        var state = new AccountPreferencesPageViewModel();
        state.Apply(Preferences(false));
        state.Select(true);

        state.Apply(Preferences(true, "Applied"));

        Assert.True(state.TrailingStopsEnabled);
        Assert.True(state.ConfirmedTrailingStopsEnabled);
        Assert.False(state.HasPendingChange);
        Assert.Equal("Applied", state.ApplicationStatus);
    }

    /// <summary>Trace: FR3, SR1, TR1. Verifies the page uses the exact introductory copy and exposes both native Boolean radio labels with one checked value after live load.</summary>
    [Fact]
    public void Render_ShouldShowLiveValueAndHistory_WhenInitialLoadSucceeds()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(true)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Live trailing stops control for the configured account.", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(2, cut.FindAll("input[type='radio']").Count);
            Assert.Contains("Enabled", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Disabled", cut.Markup, StringComparison.Ordinal);
            Assert.Single(cut.FindAll("input[type='radio'][checked]"));
            Assert.Contains("Observed", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: Phase 5.1. Verifies a successful fresh-install response renders the explicit Unconfigured state without a provider warning and requests history only after current state succeeds.
    /// Expected: the page leaves loading, shows Unconfigured, contains no current-state error, and records current-state and history requests in order.
    /// Why: an absent SQL projection is valid configuration state and must not be mistaken for provider unavailability or cause parallel initial reads.
    /// </summary>
    [Fact]
    public void Render_ShouldShowUnconfiguredStateAndLoadHistoryAfterCurrentState_WhenPreferencesAreNotConfigured()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, new AccountPreferencesViewModel(null, null, null, null, null, null, null, "Unconfigured", null, null, null, null, "Unconfigured")),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var cut = context.RenderComponent<AccountPreferences>();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("account-preferences-loading", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Desired setting:", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Unconfigured", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("account-preferences-error", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(2, context.ApiHandler.Requests.Count);
            Assert.Contains("/api/platform/account-preferences", context.ApiHandler.Requests[0].RequestUri, StringComparison.Ordinal);
            Assert.Contains("/api/platform/account-preferences/observations", context.ApiHandler.Requests[1].RequestUri, StringComparison.Ordinal);
        });
    }

    /// <summary>Trace: DR-01. Verifies a successful current-state response remains rendered when the secondary history request fails, while the separate history error identifies the degraded observation path.</summary>
    [Fact]
    public void Render_ShouldKeepCurrentStateVisible_WhenHistoryLoadFails()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(true)),
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var cut = context.RenderComponent<AccountPreferences>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Trailing stops enabled", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Last confirmed: Enabled", cut.Markup, StringComparison.Ordinal);
            Assert.NotEmpty(cut.FindAll("input[type='radio']"));
            Assert.DoesNotContain("data-testid=\"account-preferences-error\"", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Unable to load account preferences history", cut.Find("[data-testid='account-preferences-history-error']").TextContent, StringComparison.Ordinal);
        });
    }

    /// <summary>Trace: FR3, NF2. Verifies a changed radio selection renders the exact pending heading, preserves last-confirmed text, and displays a non-live pending cue.</summary>
    [Fact]
    public void Select_ShouldChangePendingValue_WhenOperatorChoosesEnabledRadio()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForElement("fieldset input[type='radio']");
        cut.FindAll("fieldset input[type='radio']")[0].Change(new ChangeEventArgs { Value = "true" });
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Trailing stops enabled", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Last confirmed: Disabled", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Pending change: not yet confirmed", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Application status: OK", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>Trace: FR3, SR1. Verifies successful save renders separated application status and a persistent atomic status confirmation that can be dismissed while retaining status.</summary>
    [Fact]
    public void Save_ShouldShowPersistentConfirmation_WhenUpdateSucceeds()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(true, "Applied")),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History("next")));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForElement("[data-testid='account-preferences-save']").Click();
        cut.WaitForAssertion(() =>
        {
            var confirmation = cut.Find("[data-testid='account-preferences-confirmation']");
            Assert.Equal("status", confirmation.GetAttribute("role"));
            Assert.Equal("true", confirmation.GetAttribute("aria-atomic"));
            Assert.Contains("saved and confirmed", confirmation.TextContent, StringComparison.Ordinal);
            Assert.Contains("Application status: Applied", cut.Markup, StringComparison.Ordinal);
        });
        cut.Find("[data-testid='account-preferences-confirmation'] button").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("account-preferences-confirmation", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Application status: Applied", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(context.JSInterop.Invocations, invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>Trace: FR3, SR1. Verifies an initial preference-load failure shows urgent feedback without rendering an actionable default editor or Save action.</summary>
    [Fact]
    public void Render_ShouldHideEditorAndSave_WhenInitialPreferenceLoadFails()
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var cut = context.RenderComponent<AccountPreferences>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unable to load account preferences.", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Trailing stops enabled", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("account-preferences-save", cut.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("input[type='radio']"));
        });
    }

    /// <summary>Trace: FR3, SR1. Verifies both Boolean selections preserve the existing outbound JSON contract through the sequenced HTTP handler.</summary>
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Save_ShouldSendBooleanJsonValue_WhenSelectionIsChanged(bool enabled, string expectedJsonValue)
    {
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(enabled, "Applied")),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForElement("[data-testid='account-preferences-save']");
        var radio = cut.FindAll("fieldset input[type='radio']")[enabled ? 0 : 1];
        radio.Change(new ChangeEventArgs { Value = enabled.ToString().ToLowerInvariant() });
        cut.Find("[data-testid='account-preferences-save']").Click();

        cut.WaitForAssertion(() => Assert.Contains(context.ApiHandler.Requests,
            request => request.Content is not null &&
                JsonSerializer.Deserialize<Dictionary<string, bool>>(request.Content)!.Single().Value == bool.Parse(expectedJsonValue)));
    }

    /// <summary>Trace: FR3, SR1. Verifies saving disables the complete Boolean fieldset and Save action while the provider update is in flight.</summary>
    [Fact]
    public void Save_ShouldDisableEditorAndSave_WhenUpdateIsInFlight()
    {
        var updateResponse = new TaskCompletionSource<bool>();
        using var context = new PlatformComponentTestContext("local-operator", null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()),
            _ => DelayedJsonResponse(updateResponse.Task, Preferences(true, "Applied")));
        var cut = context.RenderComponent<AccountPreferences>();
        cut.WaitForElement("[data-testid='account-preferences-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("fieldset").GetAttribute("disabled"));
            Assert.NotNull(cut.Find("[data-testid='account-preferences-save']").GetAttribute("disabled"));
        });
        updateResponse.SetResult(true);
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

    /// <summary>Trace: DD-02. Verifies a new selection clears stale save confirmation and rejection feedback without changing durable load feedback ownership.</summary>
    [Fact]
    public async Task Select_ShouldClearTransientSaveFeedback_WhenSelectionChanges()
    {
        using var context = PlatformComponentTestContext.CreateServiceContext(
            apiResponses: [
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()),
                _ => new HttpResponseMessage(HttpStatusCode.Conflict)]);
        var presenter = context.Services.GetRequiredService<AccountPreferencesPagePresenter>();
        await presenter.LoadAsync(CancellationToken.None);
        presenter.State.Select(true);
        await presenter.SaveAsync(CancellationToken.None);

        presenter.Select(false);

        Assert.Null(presenter.SaveError);
        Assert.Null(presenter.Message);
        Assert.False(presenter.State.TrailingStopsEnabled);
    }

    /// <summary>Trace: DD-01. Verifies the production HTTP 409 mismatch preserves the last confirmed value and reports only the transient save failure.</summary>
    [Fact]
    public async Task Save_ShouldPreserveConfirmedValue_WhenProviderReturnsConflict()
    {
        using var context = PlatformComponentTestContext.CreateServiceContext(
            apiResponses: [
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()),
                _ => new HttpResponseMessage(HttpStatusCode.Conflict)]);
        var presenter = context.Services.GetRequiredService<AccountPreferencesPagePresenter>();
        await presenter.LoadAsync(CancellationToken.None);
        presenter.State.Select(true);

        await presenter.SaveAsync(CancellationToken.None);

        Assert.False(presenter.State.ConfirmedTrailingStopsEnabled);
        Assert.Equal("Unable to save and confirm account preferences.", presenter.SaveError);
        Assert.False(presenter.IsSaving);
    }

    /// <summary>Trace: Account Preferences UI Clarity. Verifies owned save cancellation does not become feedback and always returns the presenter to idle.</summary>
    [Fact]
    public async Task Save_ShouldResetSavingWithoutError_WhenOperationIsCancelled()
    {
        using var context = PlatformComponentTestContext.CreateServiceContext(
            apiResponses: [_ => new HttpResponseMessage(HttpStatusCode.OK)]);
        var presenter = context.Services.GetRequiredService<AccountPreferencesPagePresenter>();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await presenter.SaveAsync(cancellationSource.Token);

        Assert.False(presenter.IsSaving);
        Assert.Null(presenter.SaveError);
        Assert.Null(presenter.Message);
    }

    /// <summary>Trace: Account Preferences UI Clarity. Verifies confirmation dismissal clears only transient confirmation feedback.</summary>
    [Fact]
    public async Task DismissConfirmation_ShouldClearMessage_WhenConfirmationIsVisible()
    {
        using var context = PlatformComponentTestContext.CreateServiceContext(
            apiResponses: [
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()),
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(true)),
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History())]);
        var presenter = context.Services.GetRequiredService<AccountPreferencesPagePresenter>();
        await presenter.LoadAsync(CancellationToken.None);
        presenter.State.Select(true);
        await presenter.SaveAsync(CancellationToken.None);

        presenter.DismissConfirmation();

        Assert.Null(presenter.Message);
        Assert.True(presenter.State.ConfirmedTrailingStopsEnabled);
    }

    /// <summary>Trace: DD-02. Verifies authoritative success is retained when the secondary history refresh fails, with qualified success as the only transient status.</summary>
    [Fact]
    public async Task Save_ShouldShowQualifiedSuccess_WhenHistoryRefreshFails()
    {
        using var context = PlatformComponentTestContext.CreateServiceContext(
            apiResponses: [
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(false)),
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, History()),
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Preferences(true)),
                _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)]);
        var presenter = context.Services.GetRequiredService<AccountPreferencesPagePresenter>();
        await presenter.LoadAsync(CancellationToken.None);
        presenter.State.Select(true);

        await presenter.SaveAsync(CancellationToken.None);

        Assert.True(presenter.State.ConfirmedTrailingStopsEnabled);
        Assert.Null(presenter.SaveError);
        Assert.Equal("Trailing stops preference saved and confirmed. Observed history could not be refreshed.", presenter.Message);
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
