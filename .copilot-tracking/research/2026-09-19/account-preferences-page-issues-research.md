<!-- markdownlint-disable-file -->
# Task Research: Account Preferences Page Issues

Research the reported account-preferences behavior and propose evidence-based resolutions for the trailing-stops status, account-mismatch warnings, and configuration layout.

## Task Implementation Requests

* Determine why Last confirmed shows `Disabled` and recommend a time value or an intentional blank state.
* Determine the source and user-facing wording for the IG-session-account mismatch shown during status checks and saves.
* Determine why trailing-stops choices and Save preferences are vertically stacked, and recommend a horizontal layout without large selection boxes.
* Recommend how save failures should be presented and handled.

## Scope and Success Criteria

* Scope: Account preferences UI, trailing-stops status/configuration, and the associated check/save validation path.
* Assumptions:
  * The supplied screenshots represent the current local web application behavior.
  * Research only; no product code or tests will be changed.
* Success Criteria:
  * Identify the owning UI and backend code paths with line-level evidence.
  * Evaluate viable resolutions and select one approach for each issue.
  * Provide implementation-ready guidance, including validation coverage and user-facing text.

## Outline

1. Verified display and layout causes.
2. Verified account-safety and error-contract causes.
3. Evaluated alternatives and selected resolution.
4. Staged implementation and validation plan.

## Potential Next Research

* Define an administrator-authorized account-rebinding workflow.
  * Reasoning: the existing safety guard deliberately prevents an authenticated
    IG account from silently inheriting preferences bound to another account.
  * Reference: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:28-31`
* Confirm the desired null-timestamp display policy.
  * Reasoning: the API intentionally permits `LastVerifiedAtUtc` to be null;
    the recommended behavior is to omit the row rather than show a Boolean or
    invent a synthetic date.
  * Reference: `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:15`

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
  * Displays `ConfirmedTrailingStopsEnabled` as `Enabled` or `Disabled` under
    the `Last confirmed` label at lines 27-30.
  * Places the trailing-stops fieldset and Save action in separate block
    siblings at lines 40-48.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`
  * Creates bordered radio tiles at lines 90-114 and explicitly stacks controls
    at narrow widths at lines 159-174.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs`
  * Already projects nullable `LastVerifiedAtUtc` at lines 25-40.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs`
  * Blocks a save when the latest authenticated IG account differs from the
    durable preference target at lines 18-19 and 28-31.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs`
  * Applies the same durable account-target guard before provider I/O at lines
    16-20.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs`
  * Repeats the comparison against a fresh IG session before observation and
    remediation I/O at lines 15-23 and 35-43, closing a session-change race.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs`
  * Maps Check's `AccountMismatch` to typed HTTP 409 Problem Details at lines
    57-61, 66-68, 81-83, and 94-97 but maps Save's generic conflict shortcut
    to an untyped conflict at lines 27-40.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`
  * Preserves Check detail but replaces Save failure detail with generic text at
    lines 54-82 and 97-103.

### Code Search Results

* `LastVerifiedAtUtc`
  * The API response, Web DTO, and page view model retain a nullable timestamp,
    but the page does not consume it.
* `IG session account did not match the configured target account.`
  * The text appears in Check preflight and both IG gateway preflights; Save
    repeats it while categorizing the result differently.

### Project Conventions

* Standards referenced: focused vertical slices, native semantics retained by
  existing bUnit tests, and Playwright for end-to-end viewport behavior.
* Instructions followed: `.github/copilot-instructions.md`, folder-structure
  guidance, and the Task Researcher research-only boundary.

### Project Conventions

* Tests use xUnit in focused Application, Infrastructure, API, Web unit, and
  Web E2E projects. The existing account-preferences split places handler
  policy in application unit tests, HTTP shape in API tests, Razor semantics in
  bUnit, and browser geometry in Playwright.
* The Web project already uses native `InputRadio` controls with semantic
  `fieldset`/`legend` markup. The selected approach preserves this instead of
  introducing a UI-library replacement for a narrow defect fix.

## Key Discoveries

### Incorrect Last-Confirmed Binding

The page has the correct nullable confirmation time but displays the wrong
property. `LastVerifiedAtUtc` flows from the API through
`PlatformApiClient.cs:218-239` and the page view model, while the Razor
component uses `ConfirmedTrailingStopsEnabled` at
`AccountPreferences.razor:27-30`. The Boolean describes the last observed
setting, not when it was confirmed. This directly causes the `Disabled` text
shown in the supplied screenshot.

### Intentional, but Inadequate, Layout

The large blue boxes and vertical arrangement are intentional source CSS, not
a rendering malfunction. The radio labels are styled as tiles, and the Save
button is structurally outside the fieldset. The narrow-screen media query
further turns the radio grid into one column and makes action buttons
full-width. A control-row markup change and scoped CSS change are both needed.

### Account Mismatch Is a Safety Guard

`AccountPreferencesCurrentState.AccountId` is the durable target account. On
the initial successful Save it is established from the latest
`IgLoginSnapshot.CurrentAccountId`; later Check and Save calls refuse to act
when the current authenticated account differs. The gateway validates again
against the fresh IG `currentAccountId` immediately before provider I/O.

This two-layer guard prevents wrong-account preference reads and writes and
closes a time-of-check/time-of-use gap. It must remain in place. The user
experience is deficient because Check and Save classify and display the guard
inconsistently.

In plain language, an account mismatch means the application previously saved
these trailing-stops preferences for one IG account, but the IG session now
reports a different account. This can happen after signing in with another IG
account, switching accounts in IG, or retaining preferences after the active
IG account changed. The application stops rather than checking or changing
trailing-stops settings for the wrong account. It does not mean that trailing
stops are necessarily disabled, nor does it indicate a failed IG credential;
it indicates that the authenticated account identity is not the one to which
these preferences are bound.

The comparison is not based on the number of configured login credentials. It
uses the IG account identifier at three points:

* The durable target: `AccountPreferencesCurrentState.AccountId`, written when
  a preference is first successfully saved.
* The latest captured login identity: `IgLoginSnapshot.CurrentAccountId`.
* The fresh provider-session identity: IG's `currentAccountId` from a new
  session request immediately before account-preferences I/O.

The application compares the durable target with the latest login snapshot
before contacting IG. The gateway then compares the durable target with the
fresh IG-session value. Both comparisons are exact and case- and
whitespace-sensitive. A single set of IG login details can still result in a
warning when any one of those persisted or newly retrieved values is absent,
stale, or different.

For a first successful Save there is no previous durable target: the latest
snapshot account ID becomes the target. Therefore a target mismatch is more
likely to be retained prior state, a missing or stale latest-login snapshot,
the wrong active environment or database, or a fresh IG session returning a
different `currentAccountId`; it is not normally a first-save condition.

### Save And Check Reconcile IG To The Desired State

The intended behavior is verified: the operator selects the desired
trailing-stops value, then Save makes IG match that value before committing the
local desired state. Save is IG-first and confirmation-gated, not a local
write followed by eventual correction. It creates an operation, verifies the
account, reads the IG preference, writes it only if it differs, reads it back,
and then atomically persists the desired state, audit, confirmed observation,
and completed operation. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:17-63`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:35-49`.

Check status is also an explicit repair action. It observes IG, remediates the
provider value when it differs from the saved desired value, and persists the
confirmed observation. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:15-38`.

The scheduled background reconciliation is intentionally different: it
observes and records `InSync`, `Drifted`, or a failure, but does not change IG.
Therefore Save and Check status are the confirmed remediation paths; the
background service is detection and reporting only. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:13-31`.

A new Save with the existing desired value is still a new local operation,
revision, and desired-state audit. If IG already matches, the gateway confirms
by reading it but skips the IG `PUT`; if IG has drifted, it repairs IG first.
Only replaying the same idempotency key with the same account, actor, and
selected value bypasses gateway I/O. This distinction preserves an audit trail
for explicit operator confirmation while preventing duplicate provider work on
retried requests.

### Current Coverage Gaps

* Existing bUnit and E2E tests assert `Enabled` or `Disabled` after `Last
  confirmed`, encoding the reported defect. See
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:102-138`
  and
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:42-72`.
* There is no Save durable-target mismatch test, no gateway observation-mismatch
  test, and no account-switch scenario covering both actions.
* No browser test asserts this page's desktop control-row geometry or narrow
  viewport wrapping.

## Technical Scenarios

### Trailing-Stops Status And Control Layout

**Requirements:**

* Show a confirmation time when one exists and never substitute a Boolean.
* Arrange Enabled, Disabled, and Save preferences left-to-right on sufficient
  viewport width, with compact spacing and no tile treatment.
* Retain accessible native radio semantics, visible focus, 44px targets, and
  usable narrow-screen behavior.

**Preferred Approach:**

Keep native Blazor controls. Render a semantic `<time>` from
`LastVerifiedAtUtc` and omit the entire `Last confirmed` row when the value is
null. Put the radio group and existing Save button in one flex-wrapping row,
then replace the tile styling with compact inline-label styling.

```text
src/TNC.Trading.Platform.Web/
  Components/Pages/AccountPreferences/
    AccountPreferences.razor          # timestamp binding and control-row markup
    AccountPreferences.razor.css      # compact responsive control-row styles
```

```razor
@if (Presenter.State.LastVerifiedAtUtc is { } lastVerifiedAtUtc)
{
    <div>
        <dt>Last confirmed</dt>
        <dd>
            <time datetime="@lastVerifiedAtUtc.ToString(&quot;O&quot;)">
                @lastVerifiedAtUtc.ToLocalTime().ToString("g")
            </time>
        </dd>
    </div>
}

<div class="account-preferences-control-row">
    <InputRadioGroup TValue="bool" ...>
        <label><InputRadio Value="true" /> Enabled</label>
        <label><InputRadio Value="false" /> Disabled</label>
    </InputRadioGroup>
    <button class="platform-primary-action" type="button" ...>
        Save preferences
    </button>
</div>
```

```css
.account-preferences-control-row,
.account-preferences-control-row > div {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.75rem;
}

.account-preferences-control-row label {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    min-block-size: 44px;
    padding: 0;
    border: 0;
    background: transparent;
}
```

The actual patch should preserve all existing binding, save-disabled, test-id,
and focus-return attributes. On narrow screens, the row may wrap, but controls
must not overflow horizontally or become forced full-width tiles.

#### Considered Alternatives

* Replace the controls with `RadzenRadioButtonList`: rejected for this focused
  fix because native radios already provide the needed semantics and locators;
  replacement broadens markup and disabled-state risk.
* Change only the CSS media query: rejected because separate Razor siblings
  would still prevent Save from sharing the desktop row.
* Always show a blank row: viable only if product requires a persistent label;
  omission is recommended because it matches the current conditional status
  pattern and avoids an ambiguous empty value.

### Account-Mismatch Warning And Save Failure

**Requirements:**

* Explain what operators can do without disclosing configured or current
  account identifiers.
* Give Check and Save the same stable, typed account-mismatch contract.
* Preserve the durable-target and fresh-session guards; never silently rebind
  preferences to a newly authenticated account.
* Preserve the IG-first, confirmation-gated save sequence. Account mismatch
  must stop before any provider preference read or write, leaving the saved
  desired state unchanged.

**Preferred Approach:**

Create one feature-local, safe account-mismatch reason. Use it from both
handlers and both gateway guard branches. Change Save's durable-target
mismatch to `AccountMismatch` rather than the generic `Rejected` conflict;
route it through the existing `ToProblemResult` mapper. The Web presenter
should display detailed Save guidance only when the response has both the
stable account-mismatch problem type and category. Other failures stay generic.

```text
src/TNC.Trading.Platform.Application/Features/AccountPreferences/
  AccountPreferencesAccountMismatch.cs       # shared safe reason/factory
  CheckAccountPreferencesStatusHandler.cs    # shared category/reason
  SaveAccountPreferencesHandler.cs           # shared category/reason
src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/
  IgAccountPreferencesGateway.cs             # shared safe reason
src/TNC.Trading.Platform.Api/Features/Platform/
  AccountPreferencesEndpointMapping.cs       # map Save categorized outcome
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/
  AccountPreferencesPagePresenter.cs         # recognized safe Save presentation
```

Use the following exact detail for the account-mismatch category:

> The authenticated IG account has changed. Trailing-stops preferences remain
> bound to the previously configured account. Reauthenticate with the intended
> account, then check status. Contact an administrator to change the configured
> account.

Keep the stable Problem Details title as `IG session account does not match the
configured account.` Both Check and Save should return HTTP 409, type
`/problems/account-preferences/account-mismatch`, and
`failureCategory: "AccountMismatch"` for this condition.

Do not put account IDs in the page, response payload, browser logs,
notifications, or new structured logs. A support correlation ID may be used if
it follows existing platform policy.

#### Considered Alternatives

* UI-only correction: rejected because the reported Save failure remains
  generic and the Check/Save contract stays inconsistent.
* Display current/configured account IDs, including masked forms: rejected
  because the values would spread through screenshots, browser alerts, tickets,
  and client-side logs.
* Automatically rebind on Check or Save: rejected because it lets a newly
  authenticated account inherit settings without explicit authorization and
  defeats the safety invariant.

## Selected Approach

Implement one focused native-control and stable-contract package:

1. Render `Last confirmed` from nullable `LastVerifiedAtUtc`, omitting the row
   when no time exists.
2. Place native Enabled and Disabled radios plus Save preferences in one
   compact flex-wrapping row and remove tile/full-width styling for that row.
3. Normalize the existing account-safety guard to one typed `AccountMismatch`
   HTTP 409 response with safe action guidance, used by both Check and Save.
4. Preserve application and gateway preflight checks, and require a separately
   authorized workflow for deliberate account rebinding.

This changes neither request JSON, success-response schema, persistence schema,
IG request contracts, nor the account-binding invariant.

The existing reconciliation intent is retained: Save makes a newly selected
desired value match IG and Check status repairs observed drift to the saved
desired value. The separate background reconciliation process continues to
detect and record drift without changing IG.

## Implementation And Validation Plan

1. Establish the contract in Application, Infrastructure, and API.
   * Add the feature-local safe-reason source.
   * Align Check, Save, and gateway guard categorization and wording.
   * Make categorized Save mismatch reach `ToProblemResult`; retain generic
     conflict treatment for stale revisions, idempotency reuse, and contention.
   * Add handler, gateway, and endpoint tests. Assert zero provider I/O on
     mismatch, matching status/type/category/detail across actions, and no
     account identifiers in public Problem Details.
2. Present the known safe Save response in Web.
   * Recognize the stable type and category together before rendering the safe
     detail in `AccountPreferencesPagePresenter.SaveAsync`.
   * Retain generic failure text for unknown failures, including arbitrary 409s.
   * Add bUnit tests for actionable text and account-ID non-disclosure.
3. Correct the status and layout in the Razor component and scoped CSS.
   * Add bUnit assertions for the ISO-8601 `datetime` attribute, localized
     visible time, and selected null-time policy.
   * Update old E2E assertions that expect a Boolean after `Last confirmed`.
   * Add Playwright desktop bounding-box checks proving radios and Save share a
     row. At a narrow viewport, assert wrapping without horizontal overflow and
     retain keyboard usability.
4. Prove account-switch safety end to end.
   * Use fixture-owned state to save as account A, switch latest durable session
     and provider identity to B, and invoke both actions.
   * Assert stable safe warning, no provider `accounts/preferences` GET or PUT,
     and a durable target that remains A.
5. Preserve reconciliation semantics in focused coverage.
   * Add a Save test where the selected value matches the durable desired value
     and IG already matches: assert a new desired revision and audit but no IG
     `PUT`.
   * Add a Save test where the selected value matches durable desired state but
     IG has drifted: assert IG is remediated and read back before local commit.
   * Preserve the existing Check drift-correction test and explicitly verify
     that background reconciliation only records `Drifted` rather than calling
     remediation.

  ## Single-Account Diagnostic Workflow

  Use this workflow in the same deployment, active environment, and configuration
  database that produced the warning. Do not include IG passwords, API keys,
  session tokens, CST, or `X-SECURITY-TOKEN` values in diagnostic records.

  1. Record the active platform and broker environments, operation time, HTTP
    status/problem type, safe reason, and correlation ID.
  2. Use the authorized Status view to confirm that a latest successful IG login
    snapshot exists. Record its capture time and environment, not its raw
    account ID. If no snapshot exists, the Check warning is explained without
    any evidence of a second IG account.
  3. In a controlled support session, compare the durable target account ID with
    the latest snapshot account ID. Record only equality, length, or a secure
    SHA-256 hash, never the raw IDs in tickets.
  4. If those stored values match, run one Check and correlate the gateway
    attempt. A subsequent mismatch identifies a fresh IG `currentAccountId`
    difference at request time.
  5. Verify that the selected database and platform/broker environments are the
    intended ones. Look for an earlier successful save, retained test data, a
    missing `Latest` snapshot, or an account-ID hash from a prior setup.

  The following controlled SQL Server query compares hashes only. It cannot prove
  the fresh IG gateway comparison because that value is created per request.

  ```sql
  SELECT
     state.PlatformEnvironment,
     state.BrokerEnvironment,
     state.DesiredChangedAtUtc,
     state.LastVerifiedAtUtc,
     CONVERT(varchar(64), HASHBYTES('SHA2_256', state.AccountId), 2) AS TargetAccountHash,
     snapshot.CapturedAtUtc AS LatestSnapshotCapturedAtUtc,
     CONVERT(varchar(64), HASHBYTES('SHA2_256', snapshot.CurrentAccountId), 2) AS LatestSnapshotAccountHash,
     CASE WHEN state.AccountId = snapshot.CurrentAccountId THEN 'Match' ELSE 'DifferentOrMissing' END AS SnapshotComparison
  FROM AccountPreferencesCurrentStates AS state
  OUTER APPLY (
     SELECT TOP (1) CurrentAccountId, CapturedAtUtc
     FROM IgLoginSnapshots
     WHERE BrokerEnvironment = state.BrokerEnvironment
        AND SnapshotKind = 'Latest'
     ORDER BY CapturedAtUtc DESC
  ) AS snapshot
  WHERE state.PlatformEnvironment = @PlatformEnvironment
     AND state.BrokerEnvironment = @BrokerEnvironment;
  ```

## Risks And Mitigations

| Risk | Mitigation |
| --- | --- |
| Error-text consumers break | Preserve the existing title and stable problem type; add guidance in the documented safe detail |
| A generic 409 is misidentified | Show detailed Save guidance only when both problem type and category identify `AccountMismatch` |
| CSS harms keyboard usability | Retain fieldset, legend, native radios, 44px targets, visible focus, and DOM order; verify bUnit and Playwright |
| Session change reaches IG I/O | Preserve and test both guards, then prove the account-switch scenario end to end |
| Rebinding is conflated with save/remediation | Do not add automatic rebinding; define an authorized and audited workflow separately |

## Evidence Sources

* `.copilot-tracking/research/subagents/2026-09-19/account-preferences-ui-research.md`
* `.copilot-tracking/research/subagents/2026-09-19/account-preferences-account-validation-research.md`
* `.copilot-tracking/research/subagents/2026-09-19/account-preferences-resolution-options-research.md`
