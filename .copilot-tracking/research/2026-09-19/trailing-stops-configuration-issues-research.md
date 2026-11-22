<!-- markdownlint-disable-file -->
# Task Research: Trailing Stops Configuration Issues

Research the trailing-stops configuration page issues shown in the supplied screenshot:
the enabled/disabled controls and save command are misaligned, and the page reports an
incorrect IG-account mismatch that leaves the controls unable to work.

## Task Implementation Requests

* Identify why the configuration controls are vertically arranged and not aligned with
  the trailing-stops status section.
* Identify why the account-mismatch message appears when only one IG account exists and
  why the status and save commands do not work.
* Propose suitable, evidence-backed remedies rather than implement product changes.

## Scope and Success Criteria

* Scope: The Web UI, API/application configuration ownership, authenticated IG identity
  flow, and relevant tests and documentation.
* Assumptions: The screenshot represents the current local runtime on 2026-09-19, and
  "single IG account" means only one intended external IG identity is configured.
* Success criteria:
  * Establish the code paths that determine the UI layout and account-binding state.
  * Explain the conditions under which the mismatch warning and disabled commands occur.
  * Select one implementation approach for each issue, with tests and operational steps.

## Outline

1. Verified page composition, CSS layout, command wiring, and state gates.
2. Verified IG account-binding and defensive mismatch behavior.
3. Evaluated immediate remedies and future intentional-rebind support.

## Potential Next Research

* Verify IG's authoritative `currentAccountId` identity contract.
  * Reasoning: Canonicalization is safe only if IG documents case and whitespace
    semantics.
  * Reference: IG provider documentation or support confirmation.
* Capture the affected browser session's network response if recovery fails.
  * Reasoning: It distinguishes an identity mismatch from authorization, stale
    revision, lease, or provider failure.
  * Reference: Browser Network and Console panels during Check status.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
  * The page uses native Blazor radio inputs, normal HTML command buttons, and
    Radzen tabs; it is not a Radzen input-layout defect.
  * Save and status actions delegate to the scoped presenter, which sends
    authorized API requests and renders returned failures.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`
  * The configuration fieldset expands with `flex: 1 1 auto`, moving Save to
    the far right, while the row bottom-aligns its children.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs`
  * Save rejects an account mismatch before preference I/O and returns conflict.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs`
  * Check status returns a verification failure before provider I/O when the
    latest authenticated account does not match the durable binding.

### Code Search Results

* `AccountPreferences`
  * Identified the page, presenter, API routes, application handlers,
    persistence store, IG gateway, and coverage at unit, functional, and
    Playwright layers.
* `AccountMismatch`
  * Identified the stable warning constant and both independent application and
    IG-gateway account comparisons.

### External Research

* Microsoft Learn: [Blazor input components](https://learn.microsoft.com/aspnet/core/blazor/forms/input-components?view=aspnetcore-10.0)
  * Confirms the native `InputRadioGroup` and `InputRadio` composition is
    supported; it is not necessary to replace it to resolve the layout issue.
* Radzen: [Tabs](https://blazor.radzen.com/tabs)
  * Confirms the page's tab use is independent of the input-and-action layout.

### Project Conventions

* The repository uses Blazor and Radzen for UI work, with configuration owned through
  defined application and infrastructure boundaries.
* Instructions followed: `.github/copilot-instructions.md`,
  `.github/instructions/folders.instructions.md`, and research-mode requirements.
* Existing test conventions use xUnit, bUnit, functional tests, and Playwright;
  test methods require traceability comments and descriptive names.

## Key Discoveries

### Project Structure

* The screen belongs to the Web project, while the safety policy is enforced in
  the Application layer and repeated at the Infrastructure IG boundary.
* Current durable state is scoped to platform and broker environment and saves
  the raw IG account identifier as its binding.

### Implementation Patterns

* Status has a standalone action container which defaults to left alignment.
  The configuration panel instead puts a growing fieldset before its action
  container, causing the screenshot's large gap and right-shifted Save button.
  Evidence: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:25-59`;
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:57-105`.
* Account preferences bind at the first confirmed save to the most recent IG
  authentication snapshot's raw `currentAccountId`; web-user identity is only
  an audit actor. Evidence:
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:15-58`;
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:55-73`.
* Account comparison is ordinal and unnormalized both at the application guard
  and the fresh IG preference-session gateway. A case/whitespace variation or
  different IG default/current account therefore mismatches even when one
  credential configuration exists. Evidence:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:16-49`.

### Complete Examples

The selected UI adjustment retains the existing semantic controls and changes
only the responsible flex rules:

```css
.account-preferences-choice {
  flex: 0 1 auto;
}

.account-preferences-control-row {
  align-items: center;
}
```

The final patch should preserve existing wrapping and gap rules. The exact
declaration choice should follow the surrounding stylesheet style; removing
the `flex` declaration is also sufficient if the default sizing is desired.

### API and Schema Documentation

* The account mismatch warning is defined in
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesAccountMismatch.cs:4-9`.
* Save maps account mismatch to HTTP 409 and typed problem details; status
  returns a durable verification-failed response for display. Evidence:
  `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:35-67`;
  `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:115-156`.
* The schema records bound and observed account identifiers, verification state,
  and failure summary. Evidence:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260831130930_AddAccountPreferencesCurrentState.cs:15-43`.

### Configuration Examples

No operator configuration change can safely override a mismatch. The supported
current recovery is:

```text
1. Authenticate the intended IG Demo account.
2. Verify that authentication succeeds.
3. Select Check status.
4. Stop and escalate if the warning remains; do not use Save or edit SQL.
```

## Technical Scenarios

### Configuration Layout

**Requirements:**

* Enabled, Disabled, and Save preferences must be horizontally level on desktop.
* The control group and Save must start at the same left edge as the status
  action, with Save immediately following the natural width of the radio group.
* Native radio semantics, keyboard behavior, and responsive wrapping must remain.

**Preferred Approach:**

Apply the two-rule flexbox adjustment in
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:68-105`:
remove the fieldset's expanding flex behavior and set the control row to
`align-items: center`. Do not change the markup, presenter, API contract, or
component library.

```text
AccountPreferences.razor.css       CSS sizing and cross-axis alignment
AccountPreferencesE2ETests.cs      Desktop geometry expectation
```

**Implementation Details:**

* The current status action is already left aligned; no change is required
  there.
* Retain the row's gap and flex wrapping so narrow layouts remain usable.
* Update the current E2E test which deliberately asserts Save is right of the
  expanded fieldset at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:87-108`.
* Add a vertical-center tolerance assertion and retain no-overflow and
  keyboard-radio coverage.

#### Considered Alternatives

* A separate save row would left-align Save but unnecessarily makes the compact
  setting-and-commit workflow taller. Reserve it for an explicit product-design
  decision.
* Replacing native inputs with Radzen controls does not address the flex growth
  rule and risks an unnecessary interaction regression.

### Account Binding and Command Recovery

**Requirements:**

* Never read or change broker preferences against an account different from the
  account originally bound to the durable setting.
* Give operators a workable recovery route when the same intended account can
  be authenticated again.
* Support intentional account changes only through an explicit, audited,
  authorized process.

**Preferred Approach:**

Preserve the fail-closed comparison and recover the current incident by
authenticating the intended IG Demo account, then using Check status. Do not
manually edit the database and do not use Save to attempt a rebind. If the
account intentionally changed, implement an administrator-only audited rebind
as a separate future vertical slice.

```text
Current behavior
IG authentication snapshot --different raw account ID--> mismatch guard
                                                       --> no IG preference I/O

Recovery
intended IG Demo authentication --matching raw account ID--> Check status
                                                      --> observe/remediate desired state
```

**Implementation Details:**

* A single credential configuration is not proof that the raw account ID is
  stable. IG can return a different default/current account on a later session,
  and raw case/whitespace differences are currently distinct.
* Both Save and Check status deliberately stop before IG preference I/O on a
  mismatch, as covered by unit and functional tests:
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:18-27`;
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs:30-44`;
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:105-145`.
* The page itself does not permanently disable either command for a mismatch;
  it reports a save problem or a verification-failed status. Persistent
  inability to act after reauthentication needs Network/Console evidence to
  distinguish mismatch from authorization, concurrency, or provider failure.
* A future rebind must require a fresh target-account confirmation, expected
  revision, lease, transaction, distinct audit event, and an administrator-only
  API/UI workflow. Preserve old observations and operation history.
* Do not normalize account IDs without authoritative IG documentation. If
  approved, canonicalization must be implemented consistently at all identity
  capture, comparison, persistence, lease, query, and migration boundaries.

#### Considered Alternatives

* Manual SQL update is rejected because it bypasses target-account verification,
  revision and lease checks, auditing, operation history, and confirmation.
* Permitting ordinary Save to silently rebind converts a protective mismatch
  barrier into a cross-account write path.
* Case-insensitive or trimming comparisons are deferred because provider
  identifier semantics are unknown.

## Recommended Implementation Sequence

1. Make the CSS-only alignment change and update the focused desktop Playwright
   geometry assertion; run the affected E2E tests at desktop and narrow widths.
2. Resolve the active incident through intended-account reauthentication and
   Check status. Inspect the browser request/response if it remains blocked.
3. Decide whether intentional account rebinding is a business workflow. If it
   is, deliver a separately authorized, audited rebind slice rather than
   modifying Save.
4. Obtain IG's `currentAccountId` contract before considering canonicalization.

## Remaining Decisions

* Confirm that natural wrapping below the available width is the intended mobile
  behavior once Save follows the radio group.
* Confirm whether intentional account changes need product support and who
  authorizes them.
* Confirm IG `currentAccountId` case and whitespace semantics before changing
  identity comparison.