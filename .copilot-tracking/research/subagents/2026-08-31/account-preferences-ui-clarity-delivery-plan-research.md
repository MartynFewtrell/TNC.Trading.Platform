---
title: Account Preferences UI Clarity Delivery Plan Research
description: Implementation-ready repository evidence and delivery sequencing for the account-preferences UI clarity recommendations
ms.date: 2026-08-31
ms.topic: reference
---

## Research Status

Complete on 2026-08-31. Research was limited to repository inspection, the
three completed account-preferences evidence reports, and the current focused
bUnit baseline. No production, test, wiki, or work-package file was changed.

The existing focused baseline passed 10 of 10 tests in
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`.

## Research Questions

1. Which repository instructions govern the expected Razor, CSS, C#, test, wiki, and plan changes?
2. Which files and line ranges currently own account-preference presentation, state, transport, application behavior, tests, and documentation?
3. Does implementation remain four Web presentation files plus focused bUnit tests, with no API, application, or configuration changes?
4. Which additional documentation or plan files are required before repository completion?
5. Which ordered delivery sequence provides the quickest falsification and safest state-transition changes?
6. Which edge cases, acceptance criteria, rollback boundaries, and validation commands must the implementation cover?

## Working Hypothesis

The runtime implementation should remain confined to four Web presentation files and focused bUnit tests because the existing Boolean client contract and authoritative application readback already provide the required persistence semantics. Repository completion rules may additionally require updates to affected wiki pages and an existing numbered work-package plan.

The cheapest disconfirming check is to verify whether the current component or presenter lacks enough response state to distinguish confirmed persistence from history-refresh failure, or whether any API/application contract must change to represent that distinction.

## Instruction Evidence

The following rules control the proposed implementation.

* `.github/copilot-instructions.md:18-67` requires scoped repository guidance,
    focused tests for behavior changes, `MethodName_StateUnderTest_ExpectedResult`
    test names, traceability comments immediately above tests, and affected wiki
    updates before numbered plan completion. It also prohibits drafting a new
    work package unless explicitly requested.
* `.github/AGENTS.md:13-26,34-43` favors the Blazor UI specialist for delivery,
    file-and-line evidence, focused sessions, and durable lessons only when a new
    reusable repository rule emerges.
* `.github/lessons.md:1-9` favors narrow executable validation and
    file-and-line evidence. This UI change does not establish a new repository
    customization rule, so no `AGENTS.md` or `lessons.md` update is needed.
* `.github/instructions/architecture.instructions.md:24-71,103-139` classifies
    Web as an inbound adapter. Presentation state and feedback belong in Web;
    application policy and provider confirmation must remain inward and
    independent of UI types.
* `.github/instructions/csharp.instructions.md:14-73` requires existing C# and
    nullable conventions, intentional error handling, no `Arrange/Act/Assert`
    comments, and tests for changed behavior.
* `.github/instructions/radzen-blazor.instructions.md:20-94` prefers Radzen but
    explicitly rejects complex components when built-in Blazor or semantic HTML
    is sufficient. It requires visible labels, keyboard behavior, focus order,
    semantic headings, and interaction tests.
* `.github/instructions/tests.instructions.md:20-98` requires in-process,
    infrastructure-free unit tests where possible. Every changed or added test
    needs an immediately preceding comment that states traceability, verified
    behavior, expected result, and regression value. Names must follow the
    repository method/state/expectation convention.
* `.github/instructions/playwright.instructions.md:18-59` requires role- or
    label-first locators, web-first assertions, no fixed delays, and meaningful
    `Test.StepAsync` groups if a browser test is eventually added.
* `.github/instructions/refactoring.instructions.md:20-77` requires small,
    behavior-scoped steps, preservation of route/authorization/transport
    contracts, and validation after each structural change.
* `.github/instructions/configuration.instructions.md:18-51` keeps
    operator-managed configuration behind established application services. The
    proposed UI does not create or alter configuration storage, keys, or secret
    handling.
* `.github/instructions/docs.instructions.md:18-67` and
    `.github/instructions/work-packages.instructions.md:18-101` make
    `docs/wiki/` the implemented-solution source of truth. They prohibit a new
    `docs/00x-*` package without an explicit request and require affected wiki
    content before an existing numbered plan is complete.
* `.github/instructions/folders.instructions.md:15-46` places product code in
    `src/`, tests in `test/`, and documentation in the established `docs/`
    hierarchy. The new scoped stylesheet belongs beside its Razor component.
* `.github/instructions/dotnet-stack.instructions.md:17-87` retains the
    repository's configured .NET and Blazor stack. `global.json:1-6` pins SDK
    `10.0.200`; no framework or package change is needed.

The account-preferences task has no dedicated numbered work-package
requirements. New tests should not invent requirement identifiers. Their XML
comments should cite task items 1 through 4 from
`.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md:5-19`
and, where applicable, `004-ui-update-and-refactor` requirements `NF2`, `NF3`,
and `NF7` from `docs/004-ui-update-and-refactor/requirements.md:133-145`.

## Repository Evidence

### Owning Web presentation

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:1-7`
    owns the route, Operator authorization, presenter injection, and navigation
    scope check.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:9-25`
    owns all requested text and interaction markup. It currently says
    `Live trailing stops control for the configured Test account.`, labels one
    `InputCheckbox` as `Trailing stops enabled`, renders an explicit save button,
    and combines save confirmation with durable application status in one
    always-present `role="status"` paragraph.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:31-45`
    owns initial load, save/history event forwarding, and cancellation on
    disposal. It has no selection callback, dismiss callback, save-button
    element reference, or focus restoration.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:3-27`
    owns loading and maps current-preference or history failures to one load
    error. Because the component renders its editor after loading ends, a failed
    initial current-preference request currently exposes the view model's default
    `false` as an actionable unchecked setting.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:29-45`
    owns save state and confirmation. Update and history refresh share one `try`.
    It sets success before history refresh, so a history-only failure leaves both
    a success message and the generic save error.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:47-53`
    owns history paging. No change is needed to paging behavior.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-25`
    stores one editable Boolean and cannot distinguish no confirmed load,
    pending selection, or last confirmed state. `Apply` overwrites the editable
    value with a server response.
* No
    `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`
    exists. The proposed fourth production file is new, not an edit.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css:1-59`
    is the only current page-scoped stylesheet and demonstrates local class
    naming, platform CSS variables, `::deep` only for generated Radzen markup,
    and a narrow-width media query.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:235-291,438-459` supplies the
    existing panel, primary action, input font, error, and hint styles. The new
    stylesheet can reuse these tokens without global CSS changes.

### Transport and application contracts

* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-166` performs
    authenticated GET, PUT, and history GET calls. PUT serializes the existing
    `bool` as `TrailingStopsEnabled`; all non-success responses become
    `HttpRequestException` through `EnsureSuccessStatusCode`.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:183-185` defines the
    non-nullable Boolean current-preference and observation records. The response
    already carries application status and observed time.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:8-32`
    maps confirmed success to HTTP 200 and authoritative readback mismatch to
    HTTP 409 with requested and observed Boolean extension fields.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:51-54`
    keeps the HTTP update input nullable only so omission differs from explicit
    `false`. This remains correct.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:13-34`
    rejects unsupported environments, performs one PUT, and does not retry an
    indeterminate write.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:36-60`
    performs the authoritative GET. It returns success only when observed equals
    requested, records a differing known value, and returns `NotApplied` for the
    API's 409 mapping.

These contracts already distinguish confirmed success, mismatch, and provider
failure. The Web does not need new response data to implement pending versus
confirmed presentation or to classify a subsequent Web history refresh.

### Focused tests and test setup

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:10-172`
    contains 10 current tests for initial checked state/history, checkbox change,
    success, load/save pending states, cancellation, load failure, paging, and
    authorization. The baseline run on 2026-08-31 passed all 10.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:14-50`
    is checkbox-oriented and does not assert both labels, radio semantics,
    pending versus confirmed state, or outbound PUT content.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:123-136`
    verifies only save-button disabling during update, not selector disabling.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:138-172`
    verifies load failure, paging, and denial, but not safe suppression of the
    editor after an unconfirmed initial load.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:1-27`
    targets `net10.0` and already references bUnit 1.40.0, xUnit 2.9.3, the ASP.NET
    Core framework, and the Web project. No package or project edit is required.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:27-133`
    supplies in-memory rendering, loose JavaScript interop, authenticated
    Operator state, Radzen services, a sequenced HTTP handler, API client, and
    scoped presenter. No test-context edit is required.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/SequencedHttpMessageHandler.cs:5-39`
    captures HTTP method, URI, authorization, and serialized content. It already
    supports exact outbound `true` and `false` body assertions.
* No account-preferences source test exists in the Web FunctionalTests or
    E2ETests projects. Those projects currently cover authentication. Their
    AppHost process cannot replace server-side API calls through browser request
    interception, so a deterministic provider fixture is a prerequisite for a
    reliable account-preferences Playwright test.

### Affected wiki documentation

* `docs/wiki/operator-guide.md:565-580` says the page controls the configured IG
    Test account, calls the editor a toggle, and states that the confirmed value
    replaces it after readback. It must describe the explicit Enabled/Disabled
    selector, pending and last-confirmed distinction, dismissible confirmation,
    qualified history-refresh warning, and disabled behavior.
* `docs/wiki/testing-and-quality.md:553-566` enumerates current Web bUnit
    coverage. It must list radio semantics, pending/confirmed transitions,
    request Boolean, dismissal, mismatch/failure, partial success, and load
    failure safety after those tests land.
* `docs/wiki/application-overview.md:195-208`,
    `docs/wiki/runtime-behavior.md:728-748`,
    `docs/wiki/api-reference.md:551-586`,
    `docs/wiki/architecture.md:600-615`, and
    `docs/wiki/local-development.md:144-157` remain accurate. They describe the
    unchanged authority, Boolean API, PUT-to-GET confirmation, environment guard,
    observation history, and layer ownership. They need no edit for this slice.
* `docs/004-ui-update-and-refactor/plans/001-delivery-plan.md:1-47` is already
    complete and did not target Account Preferences as a prioritized page. Do
    not reopen it or create a new work package. If another numbered execution
    plan adopts this research, that plan should record the two wiki updates in
    its completion checklist.

## Hypothesis Verdict

Verified with one correction and one added safety requirement.

The runtime implementation remains exactly four Web presentation files:

1. `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs`
2. `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`
3. `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
4. New `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`

Focused automated changes remain in one bUnit file:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`

No `PlatformApiClient`, API, application, infrastructure, persistence,
configuration, project, or database change is justified. The application
already rejects an authoritative mismatch as HTTP 409. The presenter can
distinguish update confirmation from its own later history GET by splitting
those awaits into separate expected-failure regions.

The correction is that the scoped CSS file does not currently exist. The
additional safety requirement is explicit unconfirmed-load state: a failed
initial live read must not render a default false value as editable truth.

Repository completion also requires two wiki edits:

* `docs/wiki/operator-guide.md`
* `docs/wiki/testing-and-quality.md`

No new numbered plan file is required or allowed without an explicit work
package request.

## Delivery Alternatives

### Alternative A: Presentation first

Change Razor and CSS first, then add pending/confirmed state and tests.

* Fastest route to a visual prototype.
* Poor early signal for stale confirmation, authoritative mismatch, partial
    history failure, and failed-load default state.
* Razor handlers would either bind directly to mutable state temporarily or
    reference presenter methods that do not exist yet.
* A visually correct intermediate state could still misrepresent provider
    authority.

Reject this order. It optimizes appearance before proving the state machine
that gives the wording its meaning.

### Alternative B: Production state first

Add view-model and presenter behavior, compile, then update component, CSS, and
tests.

* Keeps each production edit compilable and establishes coherent state before
    markup.
* Existing tests are too shallow to falsify stale-message, partial-success, or
    load-failure regressions immediately.
* A mistaken state contract could survive until the final test phase and force
    rework across component and presenter.

This is viable but not preferred because validation lags the riskiest decisions.

### Alternative C: Focused test-first vertical slices

Add one small group of failing behavior tests, implement only that state slice,
and rerun the same filtered test class before proceeding to markup and docs.

* The first red checks falsify the pending/confirmed model before CSS or focus
    work begins.
* Presenter outcomes become explicit before UI wording depends on them.
* Component semantics are tested through rendered roles, labels, state, and
    feedback rather than CSS internals.
* Each phase has a small rollback boundary and leaves the focused class green.

Select Alternative C. It provides the quickest falsification, keeps state
transitions coherent, follows repository unit-first conventions, and postpones
visual styling until the behavior contract is executable.

## Recommended Delivery Plan

### Phase 0: Establish baseline

Files changed: none.

Actions:

1. Run the focused bUnit class.
2. Record unrelated baseline failures without repairing them in this slice.

Acceptance criteria:

* Current tests execute independently of Docker, SQL Server, and live IG.
* The baseline result is known before behavior changes.

Focused validation:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"
```

Verified baseline: 10 passed, 0 failed on 2026-08-31.

Dependencies: none.

Rollback boundary: none because this phase is read-only.

### Phase 1: Specify and implement page state

Files:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs`

Edits:

1. Add focused tests for unconfirmed initial state, applying an authoritative
    value, selecting a pending value, dirty comparison, and applying a later
    authoritative value.
2. Immediately run the focused class and expect the new tests to fail or not
    compile because the state contract is absent.
3. Add nullable confirmed state or an equivalent explicit `HasConfirmedValue`
    flag, retain the editable Boolean, derive `HasPendingChange`, and add a
    selection method that changes only pending state.
4. Make `Apply` set pending, confirmed, application status, and observation
    time together from an authoritative response.
5. Rerun the same focused class immediately after the production edit.

Acceptance criteria:

* Before live GET success, no Boolean is represented as confirmed.
* After GET or successful update response, pending and confirmed values match.
* Selecting another option changes pending only and makes
  `HasPendingChange` true.
* Selecting the confirmed option makes `HasPendingChange` false.
* Applying a later authoritative response resets both values to server truth.

Test naming and comments:

* Use names such as
  `SelectTrailingStops_ShouldRetainConfirmedValue_WhenSelectionChanges` and
  `Apply_ShouldResetPendingAndConfirmedValues_WhenAuthoritativeResponseArrives`.
* Put an XML comment immediately above every test. Cite the source task and
  `NF2/NF3`, state the expected transition, and explain that it prevents local
  selection from being presented as provider-confirmed truth.

Dependencies: Phase 0.

Rollback boundary: revert the view-model contract and only its new direct
state tests. No component or transport file depends on it yet.

### Phase 2: Specify and implement presenter transitions

Files:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`

Edits:

1. Add presenter tests for selection after success, update failure, 409
    authoritative mismatch, confirmed update plus history failure, cancellation,
    and dismissal.
2. Run the focused class and confirm the new cases fail for the expected state
    or feedback reason.
3. Add `SelectTrailingStops(bool)` to delegate to the view model and clear stale
    save message and save error.
4. Add `DismissMessage()` that clears only transient confirmation.
5. Split update confirmation from history refresh. Apply the authoritative
    response and establish success before a nested history refresh. Translate a
    history-only expected failure into qualified polite feedback, not the generic
    save error.
6. Preserve owned cancellation and `finally` reset of `IsSaving`.
7. Rerun the focused class immediately after the presenter edit.

Acceptance criteria:

* A new selection clears prior success and prior save error.
* PUT/confirmation failure shows only
  `Unable to save and confirm account preferences.` and preserves prior
  confirmed state and application status.
* HTTP 409 is treated as unconfirmed failure. It does not show success or move
  confirmed state to the requested value. The existing generic safe message is
  acceptable in this no-client-contract slice.
* HTTP 200 applies the returned authoritative Boolean even if a controlled test
  response differs from the local selection. Pending and confirmed state then
  match that response.
* Confirmed update plus history GET failure shows one message:
  `Trailing stops preference saved and confirmed. Observed history could not be refreshed.`
  It shows no generic save error.
* Dismissal removes only confirmation. Application status and history remain.
* The presenter always returns `IsSaving` to false after success, expected
  failure, or owned cancellation.

Suggested test names include:

* `SelectTrailingStops_ShouldClearStaleFeedback_WhenSelectionChangesAfterSuccess`
* `SaveAsync_ShouldPreserveConfirmedStateAndShowError_WhenUpdateIsRejected`
* `SaveAsync_ShouldShowQualifiedSuccess_WhenHistoryRefreshFailsAfterConfirmation`
* `DismissMessage_ShouldPreserveApplicationStatus_WhenConfirmationIsCleared`

Dependencies: Phase 1 state contract.

Rollback boundary: revert presenter transition methods and their tests while
retaining the independently green view-model contract.

### Phase 3: Specify and implement component semantics

Files:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`

Edits:

1. Replace checkbox-oriented assertions with semantic checks for visible
    `Enabled` and `Disabled` radios, exact heading text, last-confirmed text,
    pending cue, separate application status, polite confirmation, named dismiss
    action, and safe failed-load rendering.
2. Add outbound JSON assertions against the existing captured request content
    for both `true` and `false` selections.
3. Add delayed-update assertions that the fieldset and save action are disabled
    while saving.
4. Run the focused class and verify failures correspond to old markup.
5. Replace the introductory text with exactly
    `Live trailing stops control for the configured account.`
6. Render an H2 with exactly `Trailing stops enabled` or
    `Trailing stops disabled` from pending selection. Keep the H1
    `Account preferences` unchanged.
7. Replace `InputCheckbox` with `InputRadioGroup<bool>` containing labelled
    `Enabled=true` and `Disabled=false` radios inside a `fieldset` and
    `legend` named `Trailing stops`.
8. Render `Last confirmed: Enabled/Disabled` and a non-live pending cue when
    pending differs. Do not render the editor until confirmed state exists.
9. Keep the explicit save command. Preserve unchanged-save behavior because
    repeated equal confirmed writes currently create valid observations and no
    product decision authorizes disabling them.
10. Render application status in its own persistent element. Render success only
     when `Message` exists, using `role="status"`, `aria-atomic="true"`, and a
     button named `Dismiss save confirmation`.
11. Add an `ElementReference` for Save. On dismissal, clear the message and
     restore focus to Save because the activated dismiss button is removed.
12. Rerun the focused class immediately after the Razor edit.

Acceptance criteria:

* Both choices are always visible when a confirmed value is available; exactly
  one is selected.
* Heading uses the exact requested lowercase state wording and follows pending
  selection immediately.
* Last-confirmed text does not change until an authoritative success response.
* Save emits the selected Boolean without API contract changes.
* While saving, neither radio nor Save can initiate another update.
* Initial load failure shows urgent error feedback and no editable default
  Boolean, state heading, or save action.
* Update failure and 409 show only urgent failure feedback.
* Confirmation is polite, persistent until dismissed or invalidated by a new
  selection, and independent of application status.
* Dismiss removes confirmation and focus returns to Save in a real browser.

Dependencies: Phases 1 and 2.

Rollback boundary: revert Razor and component-level test assertions together;
the presenter and view model remain independently valid and compilable.

### Phase 4: Add scoped responsive and focus styling

File:

* New `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`

Edits:

1. Add two equal flexible selector tracks, connected option styling, selected
    state, and `:focus-visible` or `:focus-within` treatment using existing
    platform CSS variables.
2. Give each option at least a 44 by 44 CSS pixel activation area and allow
    labels to wrap.
3. Lay out confirmation text and dismiss action with flexible tracks and no
    fixed width.
4. At a narrow breakpoint, stack selector choices and the dismiss action when
    needed.
5. Avoid `::deep`, generated Radzen classes, global selectors, negative letter
    spacing, and viewport-scaled fonts.
6. Immediately rerun the focused bUnit class and build the Web project. Then
    perform the narrow-width manual checks because bUnit cannot validate layout.

Acceptance criteria:

* Selector, confirmation, and controls do not clip or overlap at 320 CSS
  pixels or 400 percent zoom.
* No horizontal document scrolling is introduced by this component.
* Selected and focused states are distinguishable without color alone.
* Dynamic text does not resize or shift the control geometry unexpectedly.

Dependencies: Phase 3 markup classes.

Rollback boundary: delete the new scoped stylesheet. Semantic controls remain
usable as native radios without the segmented visual treatment.

### Phase 5: Align wiki and complete broader validation

Files:

* `docs/wiki/operator-guide.md`
* `docs/wiki/testing-and-quality.md`

Edits:

1. Update `docs/wiki/operator-guide.md:565-580` with explicit selector,
    pending/confirmed, save, dismissal, qualified history-refresh, and failed
    initial-load behavior.
2. Update `docs/wiki/testing-and-quality.md:553-566` with the delivered bUnit
    scenarios and retain the statement that default automation does not call
    real IG.
3. Check existing wiki links and headings; no navigation file change is needed
    because no page is added or renamed.
4. Run focused tests after each wiki edit only if code changed concurrently;
    otherwise use editor diagnostics and a link review, then run the broader Web
    tests and build as the final executable gate.

Acceptance criteria:

* Operator guidance matches the delivered page and does not call the control a
  checkbox or ambiguous toggle.
* Testing guidance accurately lists the new state and failure coverage.
* API, runtime, architecture, application-overview, and local-development pages
  remain unchanged because their contracts remain true.
* The adopting numbered plan, if one exists, is not marked complete until both
  wiki files are updated.

Dependencies: Phases 1 through 4 are green and manually inspected.

Rollback boundary: revert the two wiki paragraphs if implementation is backed
out. Do not leave documentation describing behavior that is not delivered.

## Contract Sketches

These sketches are implementation guidance, not production edits.

### View-model state

```csharp
internal sealed class AccountPreferencesPageViewModel
{
    public bool TrailingStopsEnabled { get; private set; }
    public bool? ConfirmedTrailingStopsEnabled { get; private set; }
    public bool HasConfirmedValue => ConfirmedTrailingStopsEnabled.HasValue;
    public bool HasPendingChange => ConfirmedTrailingStopsEnabled is bool confirmed
        && TrailingStopsEnabled != confirmed;

    public void SelectTrailingStops(bool enabled) =>
        TrailingStopsEnabled = enabled;

    public void Apply(AccountPreferencesViewModel preferences)
    {
        TrailingStopsEnabled = preferences.TrailingStopsEnabled;
        ConfirmedTrailingStopsEnabled = preferences.TrailingStopsEnabled;
        ApplicationStatus = preferences.ApplicationStatus;
        ObservedAtUtc = preferences.ObservedAtUtc;
    }
}
```

The nullable confirmed value prevents default `false` from masquerading as a
loaded provider value. Existing history properties and `ApplyHistory` remain
unchanged.

### Presenter transitions

```csharp
public void SelectTrailingStops(bool enabled)
{
    State.SelectTrailingStops(enabled);
    Error = null;
    Message = null;
}

public void DismissMessage() => Message = null;

public async Task SaveAsync(CancellationToken cancellationToken)
{
    IsSaving = true;
    Error = null;
    Message = null;

    try
    {
        State.Apply(await platformApiClient.UpdateAccountPreferencesAsync(
            State.TrailingStopsEnabled,
            cancellationToken));
        Message = "Trailing stops preference saved and confirmed.";

        try
        {
            await LoadHistoryAsync(null, cancellationToken);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or InvalidOperationException)
        {
            Message = "Trailing stops preference saved and confirmed. "
                + "Observed history could not be refreshed.";
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception exception)
        when (exception is HttpRequestException or InvalidOperationException)
    {
        Error = "Unable to save and confirm account preferences.";
    }
    finally
    {
        IsSaving = false;
    }
}
```

The nested catch must not catch cancellation. Cancellation reaches the outer
owned-cancellation branch. The variable name may be discarded if analyzers
flag the unused filtered exception.

### Component contract

```razor
<h2>@(Presenter.State.TrailingStopsEnabled
    ? "Trailing stops enabled"
    : "Trailing stops disabled")</h2>

<fieldset class="account-preferences-choice" disabled="@Presenter.IsSaving">
    <legend>Trailing stops</legend>
    <InputRadioGroup TValue="bool"
                     Value="Presenter.State.TrailingStopsEnabled"
                     ValueChanged="SelectionChanged"
                     ValueExpression="@(() => Presenter.State.TrailingStopsEnabled)">
        <label class="account-preferences-choice__option">
            <InputRadio TValue="bool" Value="true" />
            <span>Enabled</span>
        </label>
        <label class="account-preferences-choice__option">
            <InputRadio TValue="bool" Value="false" />
            <span>Disabled</span>
        </label>
    </InputRadioGroup>
</fieldset>

<p data-testid="account-preferences-application-status">
    Application status: @Presenter.State.ApplicationStatus
</p>

@if (!string.IsNullOrWhiteSpace(Presenter.Message))
{
    <div role="status" aria-atomic="true"
         data-testid="account-preferences-confirmation">
        <span>@Presenter.Message</span>
        <button type="button" aria-label="Dismiss save confirmation"
                @onclick="DismissMessageAsync">Dismiss</button>
    </div>
}
```

The final component should conditionally render this editor only when
`HasConfirmedValue` is true and should retain `@ref="saveButton"` on Save. The
dismiss handler clears presenter message and calls `saveButton.FocusAsync()`.

## Edge-Case Matrix

* Pending versus confirmed state: heading and selected radio follow pending;
    `Last confirmed` follows the latest authoritative response; a pending cue
    appears only when they differ.
* Selection after success: selecting either option clears stale confirmation
    and prior save error before rendering the new pending state.
* Update failure: preserve prior confirmed value, current pending selection,
    application status, and history; show only urgent generic save failure.
* Authoritative response mismatch: the application returns HTTP 409, the Web
    shows no success, and confirmed state remains the prior value. Parsing 409
    extensions for richer wording would require a separate `PlatformApiClient`
    error contract and is intentionally outside this four-file slice.
* Controlled HTTP 200 value differing from pending: treat the response as
    authoritative and apply it to both pending and confirmed state. This guards
    the Web boundary even though the current application only returns 200 for a
    matching readback.
* Confirmed update plus history-refresh failure: retain applied confirmed
    state and application status; show one qualified polite message and no save
    error.
* Saving disabled state: disable the whole fieldset and Save while update and
    authoritative confirmation are in flight. Keep unchanged saves available
    because repeated equal confirmed updates currently create legitimate
    observations.
* Dismissal and focus restoration: confirmation remains until dismissal or a
    new selection. Dismiss removes only transient feedback and returns focus to
    Save because the focused dismiss button leaves the DOM.
* Application status persistence: render application status separately at all
    times after a confirmed load. Selection, save failure, partial history
    failure, and dismissal must not clear it.
* Load failure: show `Unable to load account preferences.` with alert semantics;
    do not expose the default false editor, state heading, or save command without
    a confirmed live value.
* History failure during initial load: current preference may have loaded, but
    the existing aggregate load error remains. The editor can render only if
    confirmed state exists; history remains empty. A separate history-load
    warning would be a follow-on behavior change, not required here.
* Narrow viewport: options and confirmation stack without horizontal overflow
    at 320 CSS pixels and 400 percent zoom. Labels wrap and each target remains at
    least 44 by 44 CSS pixels.
* Keyboard and accessibility: fieldset/legend names the radio group; Tab enters
    and exits, Arrow keys change selection, and Space selects using native radio
    behavior. Errors remain assertive, success remains polite and atomic, the
    dismiss button has an explicit name, and visible focus is never obscured.

## Validation Commands

Run commands from the repository root.

### Focused tests after every behavior edit

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"
```

### Broader Web regression and build

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj
```

### Final solution gates

```powershell
dotnet build TNC.Trading.Platform.slnx
dotnet test TNC.Trading.Platform.slnx -m:1
```

The serialized test command is the repository-documented fallback for MSBuild
child-node instability. It is broader than this UI slice and belongs at final
completion, not after every small edit.

### Formatting and static checks

```powershell
dotnet format src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --verify-no-changes --no-restore
dotnet format test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --verify-no-changes --no-restore
```

No source-controlled Razor or CSS linter script was found. `dotnet format`
checks C# formatting but is not proof of Razor semantics or responsive CSS.
Use editor diagnostics for changed Razor/CSS/Markdown files and manually review
the two existing wiki links after documentation edits.

### Local manual browser and accessibility validation

Start the supported distributed application:

```powershell
dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj
```

Use the Web URL assigned by Aspire, sign in as an Operator or Administrator,
and use a configured IG Test account. Do not add live credentials to source,
test data, screenshots, or the report.

Validate these scenarios:

1. At desktop width, confirm both options are visible and exactly one is
    selected for loaded `true` and loaded `false` states.
2. Tab into `Trailing stops`, use Left/Right or Up/Down Arrow and Space, and
    verify visible focus, selected radio, exact state heading, unchanged last
    confirmed value, and pending cue.
3. Save. Confirm the selector and Save disable while pending; success is
    announced without moving focus; application status remains visible.
4. Activate `Dismiss save confirmation`. Confirm only the box disappears and
    focus returns to `Save preference`.
5. Exercise a safe controlled failure or unavailable provider. Confirm urgent
    error feedback and no false success. Do not depend on a real provider failure
    in CI.
6. Set the viewport to 320 CSS pixels and repeat at 400 percent browser zoom.
    Confirm no horizontal page overflow, clipping, overlap, or obscured focus.
7. Use NVDA with Firefox or Chrome and Narrator with Edge. Confirm group name,
    option names and checked state, polite atomic success, urgent error, named
    dismissal, and focus restoration.

A new Playwright test is not part of this delivery. Add one only after a
supported deterministic server-side account-preferences fixture exists. At
that point use `GetByRole`/`GetByLabel`, web-first assertions,
`Test.StepAsync`, and no fixed waits.

## Documentation Impact

Wiki updates are required before implementation is complete.

* `docs/wiki/operator-guide.md`, section `Account Preferences page`, must
    describe the new operator-visible workflow and failure/partial-success
    behavior.
* `docs/wiki/testing-and-quality.md`, section `Account Preferences coverage`,
    must describe the expanded bUnit proof and retain the deterministic, no-real-
    IG boundary.

No new wiki page, navigation entry, API documentation change, architecture
change, configuration documentation, migration note, or local-development
instruction is needed. No existing completed plan should be rewritten. If the
implementation is attached to a future numbered plan, add these two wiki edits
to that plan's final phase before marking it complete.

## Unresolved Gaps

* Native segmented-radio visuals, focus treatment, 320 CSS pixel reflow, and
    400 percent zoom have not been rendered in the current theme.
* Screen-reader behavior has not been exercised in the running application.
* Focus restoration can be verified fully only in a browser. bUnit can verify
    message removal and JavaScript invocation, but an assertion against Blazor's
    internal focus interop identifier would be brittle.
* The current Web client intentionally collapses HTTP 409 details into a generic
    message. Rich requested-versus-observed wording would require an additional
    Web client error contract and would falsify the four-file runtime boundary.
    It is not required for accurate failure semantics.
* Product intent for disabling unchanged saves remains unspecified. Preserve
    current behavior because repeated equal writes are supported and observed.
* Deterministic Playwright coverage remains blocked by the absence of a
    supported server-side account-preferences provider fixture.

## Suggested Next Research

* [ ] Prototype only the new scoped CSS against the current theme and inspect
    desktop, 320 CSS pixel, and 400 percent zoom layouts before final styling.
* [ ] Verify focus restoration and announcements with NVDA/Firefox or Chrome
    and Narrator/Edge after implementation.
* [ ] Design a deterministic AppHost account-preferences provider fixture as a
    separate test-infrastructure task before proposing Playwright CI coverage.
* [ ] Revisit richer HTTP 409 Problem Details presentation only if operators
    need requested and observed values in the failure message.

## References

Verified local research inputs:

* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-repository-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-accessibility-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-alternatives-research.md`

Primary external evidence retained from those reports:

* [ASP.NET Core Blazor input components](https://learn.microsoft.com/aspnet/core/blazor/forms/input-components)
* [Radzen 10.3.2 Switch source](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenSwitch.razor)
* [Radzen 10.3.2 SelectBar source](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenSelectBar.razor)
* [Radzen 10.3.2 Notification source](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenNotificationMessage.razor)
* [Radzen 10.3.2 Alert source](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenAlert.razor)
* [WAI-ARIA Radio Group Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/radio/)
* [WCAG Status Messages](https://www.w3.org/WAI/WCAG22/Understanding/status-messages)
* [WCAG Reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow)
* [WCAG Focus Not Obscured](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum)