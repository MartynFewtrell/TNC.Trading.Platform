<!-- markdownlint-disable-file -->
# Task Research: Account Preferences Rationalisation

Research the requested account-preferences functional and UI rationalisation, including the failed account-preferences E2E test suite.

## Task Implementation Requests

* Rationalise the account-preferences display and terminology around trailing stops.
* Define reconciliation semantics between local desired state and the IG source of truth.
* Recommend focused automated-test updates, including resolution of current E2E failures.
* Recommend corresponding `docs/wiki` updates.

## Scope and Success Criteria

* Scope: Account-preferences Web UI, its operation contracts and implementation path, IG integration behavior, histories, and affected automated tests/documentation.
* Assumptions: The requested behavior applies to the existing account-preferences feature and its current target-account configuration.
* Success Criteria:
  * Identify the controlling implementation and test paths with verified evidence.
  * Evaluate viable reconciliation approaches and select one consistent with repository conventions.
  * Provide actionable implementation, test, and wiki recommendations.

## Outline

1. Inspect the existing feature, contracts, tests, and the failed E2E test.
2. Establish the current data flow and behavior gaps.
3. Evaluate reconciliation and UI implementation approaches.
4. Select an approach and specify test and wiki changes.

## Potential Next Research

* None.
  * Reasoning: The implementation paths, test failure, alternatives, and wiki impact were investigated.
  * Reference: Delegated research records under `.copilot-tracking/research/subagents/2026-09-18/`.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
  * Lines 14-61 render the current title, status, action controls, and settings section. Lines 64-106 render observed history.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`
  * Lines 54-122 save local desired state, report it as confirmed, and independently invoke retry/remediation.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`
  * Lines 149-176 currently send only the Boolean update value, despite the server durable path consuming account and actor values.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs`
  * Lines 14-29 commit SQL desired state without calling IG in the active durable branch.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs`
  * Lines 12-29 perform a revision-bound IG observation, but do not remediate detected drift.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesHandler.cs`
  * Lines 7-24 are a separate operator path that writes IG and returns a projection.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs`
  * Lines 15-55 reject a session account mismatch before any preference use; lines 105-148 perform read, conditional write, and readback.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs`
  * Lines 13-25 atomically write desired state and audit, but do not make it immediately due for reconciliation. Lines 41-50 conditionally persist a completed observation.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs`
  * Lines 28, 51, 76, 94, and 112 correspond to the five currently failing filtered browser tests.

### Code Search Results

* `AccountPreferences`
  * Found the vertical slice across Web, API, Application, Infrastructure, unit, functional, SQL integration, and browser E2E projects.
* `PendingVerification|Pending`
  * The Web page checks `PendingVerification`, while the Application status serializes as `Pending`.
* `AccountId|Actor`
  * The browser omits both values; current HTTP requests accept them from JSON; reliable server-side sources already exist in the IG login snapshot and claims resolver.

### External Research

* No external sources were needed. The requested behavior and existing implementation are repository-specific; the recommendation is grounded in exercised code paths and test output.

### Project Conventions

* Standards referenced: vertical-slice/CQRS operation contracts, operator-only API policy, SQL-backed durable state, Aspire closed-box tests, Playwright web-first assertions, and wiki as implementation documentation source of truth.
* Instructions followed: `.github/copilot-instructions.md`, `.github/instructions/architecture.instructions.md`, `.github/instructions/csharp.instructions.md`, `.github/instructions/tests.instructions.md`, `.github/instructions/playwright.instructions.md`, `.github/instructions/aspire-tests.instructions.md`, `.github/instructions/configuration.instructions.md`, and `.github/instructions/docs.instructions.md`.

## Key Discoveries

### Project Structure

Account Preferences is already a cohesive vertical slice. The active runtime is the durable SQL projection/reconciliation branch registered by `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:74-97`; the older direct IG branch exists only for compatibility. The page is protected by the Operator policy at `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-63`.

### Implementation Patterns

* The current-state row separates desired and observed trailing-stops values, target account, desired revision, verification status, retry metadata, bounded failure text, correlation, and row version: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesCurrentState.cs:5-25`.
* Desired-state audit is already transactionally persisted with each desired-state revision: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:13-25`.
* The IG gateway already enforces account binding and supports a read-conditional-write-readback remediation sequence. It is therefore the correct application port to retain for explicit commands: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-55` and `:105-148`.
* Current durable reconciliation/remediation does not append normal observed history; only the legacy compatibility path does. This leaves the existing history tab without ordinary verification/remediation evidence.

### Current Gaps

* The current Save commits desired SQL state, does not call IG, returns `Pending`, and the page incorrectly says it was "saved and confirmed." This conflicts with the requested immediate IG update before local confirmation.
* Committed desired state is not immediately due for background verification. It requires a later login nudge or the current Retry verification action.
* A browser update omits account and actor while the durable persistence path requires both. This can reach audit persistence as a database failure instead of a typed validation/identity problem.
* Account selection and audit identity must not be browser authority. The latest successful IG login snapshot contains the authenticated account, and `PlatformAuthAuditEventResolver` resolves the authenticated user.
* The page renders target account redundantly, labels desired/observed/status in terms the request removes, and uses `PendingVerification` although the API returns `Pending`.
* The active E2E filter has five failures: four stem from a browser tab never activating through the tested keyboard sequence; one assumes a fresh `Unconfigured` SQL state although its fixture defaults to persistent SQL. The controlled IG provider also exposes the wrong adapter route/payload for successful closed-box verification.

### Complete Examples

The selected API shape keeps reads pure and makes external effects explicit:

```text
GET  /api/platform/account-preferences
PUT  /api/platform/account-preferences
     Idempotency-Key: <client-generated key>
     { "trailingStopsEnabled": true, "expectedRevision": 12 }
POST /api/platform/account-preferences/check-status
```

The browser does not supply account or actor. The API derives them from authoritative server-side context:

```csharp
var targetAccountId = await loginSnapshotStore.GetLatestSuccessfulAsync(
    configuration.BrokerEnvironment,
    cancellationToken);
var actor = PlatformAuthAuditEventResolver.ResolveUserName(user);
```

### API and Schema Documentation

* Current API contract: `docs/wiki/api-reference.md:551-574`.
* Current runtime authority and reconciliation description: `docs/wiki/runtime-behavior.md:728-747`.
* Current database entities include desired audit and append-only observations: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:153-191`.

### Configuration Examples

No new operator-entered configuration is recommended. Use the current successful IG login snapshot as the target account authority. A Check status command without a successful snapshot returns a safe durable `VerificationFailed` warning such as `No authenticated IG account`; it must not accept an account ID from the page.

## Technical Scenarios

### Trailing Stops Page Rationalisation

The page should present one concise state surface and one configuration surface. This is a terminology and interaction redesign, not a simple label substitution, because action semantics must match IG authority.

**Requirements:**

* Rename `Account preference state` to `Trailing stops`.
* Keep the current red warning treatment directly beneath that title as the exclusive warnings/errors area.
* Rename `Desired setting` to `Trailing stops`; remove target account and the separate Status display; render `VerificationFailed` as `Verification failed` in the warning area.
* Rename `Last observed at IG` to `Last confirmed` and update it only after IG has confirmed the value.
* Rename `Retry verification` to `Check status`, with compare-and-converge behavior.
* Rename the second section to `Trailing stops configuration`; lay out Enabled, Disabled, and Save preferences horizontally; remove `Last confirmed: Disabled`.

**Preferred Approach:**

* Keep the upper surface projection-only: desired `Trailing stops`, `Last confirmed`, and its timestamp. Do not display account or status separately.
* Render durable safe warnings in the existing red warning component directly beneath the `Trailing stops` heading. Convert enum display strings to human-readable text, e.g. `VerificationFailed` to `Verification failed`.
* Save and Check status both apply their returned full projection through the existing presenter/view-model path, so the upper values refresh without a page reload.
* Replace separate Retry verification and remediation controls with one `Check status` command. Preserve observed history as a diagnostic tab and add successful command observations to it.

```text
Trailing stops
<red warning/error region when durable warning exists>
Trailing stops: Enabled | Disabled
Last confirmed: 18 Sep 2026 10:31 UTC

Trailing stops configuration
(Enabled) (Disabled) [Save preferences]
```

**Implementation Details:**

* Update `AccountPreferences.razor`, its view model, presenter, and `PlatformApiClient` together; this avoids a UI that uses a new label while retaining old asynchronous semantics.
* Use a real command state to disable Save preferences and Check status while each command is active; preserve pagination/history controls independently.
* Retain transient command feedback separately from the durable warning projection. A completed operation can be HTTP-successful but still return `VerificationFailed` with a safe convergence warning.

#### Considered Alternatives

* Cosmetic-only label/action rename: rejected because current Retry only observes and current remediation is separate; it cannot meet the required compare-and-update behavior.
* Make page load call IG and auto-remediate: rejected because a GET would write an external broker setting, fail the page during an IG outage, and violate the existing SQL-only availability model.

### Check Status Compare And Converge

`Check status` must make IG the observed source of truth while treating SQL desired state as the correction target. The operation should be body-free, account-bound, revision-safe, and return the persisted projection.

**Requirements:**

* Compare current local desired state with IG.
* If equal, update Last confirmed date/time.
* If different, attempt to update IG to desired state before returning page values/warnings.
* If update succeeds, record history and return no warning.
* If observation/remediation fails, retain local desired state, retain the last confirmed value, and show a red safe warning. A session account mismatch specifically displays `IG session account did not match the configured target account`.

**Preferred Approach:**

Add `POST /api/platform/account-preferences/check-status`, implemented by a new application operation such as `CheckAccountPreferencesStatusHandler`. Resolve the target account from the latest successful `IPlatformIgLoginSnapshotStore` entry and actor from authenticated claims. Never take either value from JSON.

```mermaid
sequenceDiagram
    participant Web as Account Preferences page
    participant Api as Check status command
    participant Sql as SQL state/history
    participant Ig as IG gateway
    Web->>Api: POST check-status
    Api->>Sql: Resolve configured state and acquire account lease
    Api->>Ig: Observe trailing stops
    alt observed equals desired
        Api->>Sql: Append observation; complete InSync
    else observed differs
        Api->>Ig: Read, conditional PUT desired, readback
        alt readback equals desired
            Api->>Sql: Append observation/remediation; complete InSync
        else provider or account error
            Api->>Sql: Record VerificationFailed and retry metadata
        end
    end
    Api-->>Web: Fresh persisted projection and safe warning when needed
```

**Implementation Details:**

* With no configured desired state, return the `Unconfigured` SQL projection without IG I/O or a write.
* Before observing, ensure that the latest successful login account matches the state account. On mismatch, do not call IG; persist/return `VerificationFailed` with the exact user-facing warning requested.
* Acquire the existing account-scoped SQL lease, re-read state, and require the same account/revision before provider I/O. Map held lease or stale revision to `409`; do not call IG in either case.
* Append history only for successful Boolean observations. Record the initial drift observation and the confirmed post-remediation observation where available. Extend the remediation result if necessary to preserve both values.
* Preserve category-level safe failure codes and bounded summaries rather than provider response bodies. Return the projection with `200` for a processed but unconfirmed check; reserve Problem Details for malformed input, conflict, and unavailable/unknown save cases.

#### Considered Alternatives

* Retain separate Retry verification and Apply desired setting routes: rejected because it leaves operators to perform the requested automatic correction manually and continues to trust browser-supplied account IDs.
* Persist a `Drifted` terminal state before every remediation: rejected for the normal user flow because a successful check should return an InSync projection with no warning. Persist Drifted only when remediation cannot confirm convergence.

### Save Preferences IG-First Durability

Save preferences must change IG first, then persist the desired local value only after IG confirms the requested value. This reverses the present durable implementation and creates a recoverability boundary because IG and SQL have no shared transaction.

**Requirements:**

* Save the selected value to IG first.
* On a successful IG confirmation, persist the same desired local value, desired-state audit, observed history, and refreshed upper-page projection.
* Do not treat local intent as confirmed until IG readback confirms it.
* Avoid duplicate/boundary writes when a request times out or SQL persistence fails after IG changes.

**Preferred Approach:**

Retain `PUT /api/platform/account-preferences`, with only `trailingStopsEnabled` and `expectedRevision` in its body plus a required `Idempotency-Key` request header. Implement it as an IG-first `SaveAccountPreferences` command backed by a small operation journal.

```text
1. Resolve target account and actor server-side; validate revision and idempotency key.
2. Insert/find operation journal row before remote I/O.
3. Acquire account lease; use IG read, conditional PUT, then confirmed readback.
4. Mark journal RemoteApplied.
5. In one SQL transaction, write desired state/audit, complete InSync projection,
   append a Saved observation, and finalize the journal.
6. Return that persisted projection to update the page immediately.
```

**Implementation Details:**

* Add an `AccountPreferencesOperations` persistence entity/table with unique idempotency key, account/environment/baseline revision, requested value, actor, correlation, and phase. It records `Started`, `RemoteApplied`, `Completed`, and indeterminate/failure states.
* A repeated matching key returns/resolves the original operation; a reused key with incompatible request data returns `409` without IG I/O.
* If IG outcome is unknown, or SQL fails after confirmed IG application, retain `RemoteApplied` and return `503` with `Save outcome unknown`; do not blindly issue another PUT. A later Check status first resolves that active journal entry through IG observation before it compares or corrects against the prior desired state.
* When current desired value and confirmed IG value already equal the selection, do not advance the desired revision or audit; return InSync and optionally retain an observed command record.
* Retire the browser-facing retry/remediation endpoints after migration to Check status. API callers should not be able to nominate an arbitrary target account or audit actor.

#### Considered Alternatives

* SQL-first desired save followed by background reconciliation: rejected because it contradicts the required IG-first save and causes the current false "confirmed" presentation.
* IG-first without an operation journal: rejected because a timeout or SQL failure after IG confirmation can leave the remote value changed while local desired state remains old; the next automated reconciliation could silently undo the intended operator action.

### Automated Test And Current Failure Resolution

**Requirements:**

* Capture the renamed UI and consolidated action behavior.
* Prove IG-first save, Check status convergence, warnings, history, concurrency, and secure ownership.
* Resolve the current filtered `AccountPreferencesE2ETests` failures with real browser behavior and isolated state.

**Preferred Approach:**

Test commands at their ownership boundaries: application unit tests for policy/control flow, SQL integration tests for durability and concurrency, functional tests for API/provider sequences, bUnit for projection rendering, and Playwright for the operator workflow.

**Implementation Details:**

* Correct `test/Shared/AccountPreferences/ControllableIgProvider.cs` to the production adapter contract: `GET`/`PUT /gateway/deal/accounts/preferences` and `trailingStopsEnabled`. Add a provider contract test before adding convergence tests.
* Add `CheckAccountPreferencesStatusHandlerTests.cs` with fixed time, fake store/gateway/lease/snapshot source. Cover no state/no IG call, account mismatch/no IG call, lease conflict/no IG call, equal observation/InSync and timestamp/history, drift/one remediation/readback/InSync, provider failure/VerificationFailed/safe warning/retry, stale revision, and history order.
* Replace durable-path assumptions in `AccountPreferencesHandlerTests.cs` with IG-first save cases: successful readback persists desired/audit/history, stale revision/lease makes no IG call, equal setting remains idempotent, and unknown remote outcome creates a recoverable journal.
* Extend SQL integration tests for journal uniqueness/replay, `RemoteApplied` recovery, atomic desired-audit-history finalization, and concurrent account writes.
* Update API tests for body-free Check status; server-derived target/actor; `Idempotency-Key`; projection responses for verification failure; `409` conflict; `503` unknown save; and removal of client-provided account/actor.
* Update bUnit tests to assert the new headings/labels, warning placement, space-separated verification text, horizontal configuration controls, no target/status/last-confirmed-value text, updated projection after both commands, and canonical `Pending` behavior only for unresolved journal state.
* Fix the tab E2E tests deliberately. Use `ClickAsync` for basic tab activation. Retain one real keyboard test only for the documented Radzen key behavior after binding/fixing it; current `Space` and `ArrowRight` leave `aria-selected="false"`, making three history failures consequential. Assert selected state before table/pagination content.
* Give each E2E scenario a fixture-owned/reset SQL state or explicit API setup. The existing fixture defaults to persistent SQL and thus cannot assume `Unconfigured`. Capture the post-fix rendered state for the previous `Unconfigured` failure.
* Add browser flows for Check status equality, drift corrected automatically, account mismatch exact warning, failed remediation warning, IG-first Save projection refresh, and read-only observed-history paging.

#### Considered Alternatives

* Convert the failing keyboard tests to click-only: rejected because it masks a real browser accessibility interaction gap. Keep one appropriately specified keyboard regression test.
* Rely on shared persistent SQL fixture state: rejected because tests become order-dependent and the current `Unconfigured` assertion demonstrates that failure mode.

### Wiki Updates

**Preferred Approach:**

Update the wiki in the same implementation change, as required by repository guidance. Describe the user-visible model rather than exposing credentials or provider protocol details.

**Implementation Details:**

* `docs/wiki/operator-guide.md:565-589`: replace save-confirmed language with the two actions, all user-visible statuses/warnings, automatic Check status correction, and final layout terminology.
* `docs/wiki/runtime-behavior.md:728-747`: document SQL desired state versus IG observed authority, pure GET, Check status convergence, IG-first idempotent save, journal recovery, and state transitions.
* `docs/wiki/api-reference.md:551-574`: update request/response shapes, body-free Check status, required idempotency header, revision conflicts, no client account/actor, and safe error outcomes.
* `docs/wiki/architecture.md:600-619`: document operation ownership, account lease/revision guard, observation history, remediation, and journal boundary.
* `docs/wiki/application-overview.md:195-212`: document the page as a SQL projection with explicit broker commands, not a synchronous page-load editor.
* `docs/wiki/local-development.md:146-164`: align the controllable provider route/payload and document reproducible status/mismatch/recovery exercises.
* `docs/wiki/testing-and-quality.md:559-605`: record the new unit/SQL/functional/E2E responsibilities, browser keyboard tab requirement, provider contract double, and fixture isolation.

## Selected Approach

Implement a projection-only page with two explicit commands: an IG-first, idempotent Save preferences command and a body-free Check status compare-and-converge command. Derive target account from the latest successful IG login snapshot and actor from authenticated claims. Keep all warnings/errors in the red region under the `Trailing stops` title, update Last confirmed only from confirmed IG reads, and record successful observations/remediations in append-only history.

This is selected because it directly satisfies the requested behavior while preserving a reliable SQL-only page load, prevents browser authority over broker account/audit identity, and handles the unavoidable IG/SQL transaction boundary explicitly. It uses existing gateway, lease, revision, audit, and projection patterns rather than adding a parallel feature path.

## Planning Consolidation

The implementation-path research at
`.copilot-tracking/research/subagents/2026-09-18/account-preferences-implementation-path-research.md`
verified the referenced paths and identified three design questions. The plan
adopts the following defaults so implementation can proceed without a further
product decision:

* A processed Check status command that cannot confirm convergence returns
  `200` with the persisted `VerificationFailed` projection and its safe warning.
* A login snapshot account that differs from the persisted preference account
  produces the prescribed account-mismatch warning. It does not create or
  switch preference state implicitly.
* A matching idempotency replay resolves its existing operation without a
  further IG write. An incomplete remote-applied operation is reconciled by
  Check status before any corrective action.

The operation-journal retention period remains an operator data-retention
decision. Implement the schema and query boundary so retention can be applied
by the existing database maintenance policy without changing command semantics.
