---
title: Account Preferences Final Alternatives Review
description: Phase 2 adversarial analysis of the account-preferences state and verification recommendation
ms.date: 2026-08-31
ms.topic: concept
---

## Research Questions

* Evaluate the dedicated current-state aggregate and reconciler against the cited alternatives.
* Decide whether desired database state or last observed provider state should drive the primary display.
* Decide warning-only versus fail-closed behavior for future trading.
* Decide startup verification versus pre-order verification.
* Decide automatic repair versus explicit remediation.
* Define required session and account binding.
* Recommend an incremental implementation order that fixes the current UI failure without claiming trading exists.
* Identify corrections required in the primary report.

## Evidence Reviewed

The primary report explicitly links only one subagent report, not three. Its
failure diagnosis, IG verification analysis, and alternatives analysis align
with these three reports, which this review treated as the intended evidence
set:

* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-load-failure-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-ig-verification-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-workflow-alternatives-research.md`

The similarly named
`.copilot-tracking/research/subagents/2026-08-31/account-preferences-alternatives-research.md`
was also checked. It concerns the earlier Boolean-control and feedback design,
not the desired-versus-observed persistence alternatives in this review.

Current-checkout verification established these facts:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:19-36`
  still loads the live preference and observation history in one failure scope.
  A history failure can therefore erase the distinction between usable current
  data and unavailable history.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:13-38`
  still calls IG for every current read. Its expected gateway-failure paths
  still omit the injected event store when calling `RecordFailureAsync`, so the
  helper returns without recording those events.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:21-60`
  does not persist desired intent. It calls `UpdateAsync`, then `GetAsync`, and
  treats a matching Boolean as confirmed.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-89`
  creates a new v2 session inside every gateway operation. It does not parse or
  return `currentAccountId`. Consequently, the current update and confirming
  read use separate sessions and do not prove that both operations addressed
  the same active account.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/TrailingStopsPreferenceObservationEntity.cs:3-26`
  has a nullable `AccountId`, but
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:10-24`
  does not populate it. History is queried only by platform and broker
  environment.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs:3-13`
  has neither a target account ID nor a desired trailing-stops value.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:24-82`
  obtains `currentAccountId` and tokens for its own operation, but returns only
  non-secret evidence. It does not expose a reusable authenticated session to
  the account-preferences gateway.
* `src/TNC.Trading.Platform.Api/Program.cs:21-40` blocks API startup on SQL
  initialization and authentication reconciliation. Preference verification is
  not part of this path and should not be added as another API-availability
  prerequisite.
* Source search found no provider order or position write implementation.
  Current position behavior is read-only proof, and
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:11`
  explicitly says this feature does not authorize real orders or monetary
  exposure.

## Adversarial Findings

### Desired State Versus Last Observed State

Durable desired state should be the primary editable display because it is the
operator's intent and remains available without IG. The last observed IG value
must remain a separately labelled secondary fact. It must never become a
fallback presented as desired or current intent.

This choice needs one qualification absent from the primary recommendation:
there is no durable desired value today. A migration cannot safely reinterpret
the latest observation as operator intent because observations are not
account-bound, are subject to retention, and may represent an external change.
The aggregate therefore needs an explicit `Unconfigured` bootstrap state, such
as a nullable desired value plus revision metadata. An operator must initialize
intent explicitly, or a separately approved migration process must establish
it. Until then, the UI should say that desired state is not configured and may
show the last observation only as historical evidence.

The page should avoid the unqualified label `current preference`. The precise
labels are `Desired setting` and `Last observed at IG`, with verification status
and timestamps alongside them.

### Warning-Only Versus Fail-Closed Behavior

Warning-only is correct for the current UI because no order-producing behavior
exists. Drift, unknown provider state, stale evidence, wrong-account evidence,
or verification failure should remain visible without making the API or page
unavailable.

Fail-closed is correct at a future order boundary. Once order submission exists,
the application use case must reject a new order unless the required preference
evidence is fresh, in sync, and bound to the target account and active session
generation. That is a future contract, not a current feature claim. The current
work should not add text or status that implies trading has been implemented or
blocked by an existing gate.

The aggregate's reconciliation status should not itself contain a generic
`Blocked` state. `Unconfigured`, `PendingVerification`, `InSync`, `Drifted`,
`VerificationFailed`, and `Unsupported` describe preference facts more clearly.
A future trading-eligibility projection can derive a blocked decision from
those facts plus session, exposure, schedule, and risk controls.

### Startup Verification Versus Pre-Order Verification

Startup-only verification is insufficient. Session identity, external
preference state, and desired revision can change after startup. Conversely,
pre-order-only verification would leave operators without early diagnostics
and could add provider latency to every command.

Use two stages when trading exists:

1. Run best-effort verification after a usable account-bound session is
   established, on due-work retries, and on explicit operator request. This
   primes persisted status but must not block API startup.
2. At the future order application boundary, check a fresh persisted
   verification whose desired revision, account ID, and session generation
   still match. Perform a new verification when policy requires it. Reject the
   order if the evidence is absent, stale, or mismatched.

The present repository can implement stage 1 for truthful operator status.
Stage 2 belongs in the future order slice and should remain an acceptance
criterion or design contract until that slice exists.

### Automatic Repair Versus Explicit Remediation

The default reconciler should observe and classify, not mutate IG merely because
it detected drift. Autonomous repair can overwrite a deliberate external change
and is unsafe while account selection and business semantics remain unresolved.

Use a separate, explicit remediation command. The operator action must clearly
authorize applying desired state to IG. One acceptable UX is `Save and apply`,
which atomically commits desired intent and creates an authorized remediation
request. Another is a separate `Apply desired setting to IG` action after drift
is displayed. In both cases:

* The worker starts every attempt with `GET`.
* It sends at most one `PUT` when the verified account is drifted.
* It confirms with `GET` through the same account/session context.
* A later retry starts with `GET`, never a blind replay of an uncertain write.
* Revision checks prevent an old authorization from applying a superseded
  desired value.

This corrects the workflow-alternatives report, whose Alternative 1 description
has the reconciler automatically issue `PUT` after drift while its gaps section
still calls drift policy unresolved. The primary report's explicit-remediation
default is safer and should be retained consistently.

### Session and Account Binding

The current account-preferences gateway cannot support the proposed safety
claim. It neither returns `currentAccountId` nor shares a session between PUT
and GET. A Boolean match across two fresh sessions is not sufficient evidence
when one login may own multiple accounts.

Before account-bound verification is described as implemented, introduce an
IG session context that supplies:

* Explicit `currentAccountId`
* A non-secret session-generation identifier
* Tokens held only in the infrastructure session boundary
* A compound preference read or apply-and-confirm operation using that context

Key desired state by broker environment and explicit target account ID. Include
platform environment if the repository can host more than one platform mode in
the same database partition. Persist the observed account ID, session generation,
desired revision verified, observed value, and observation time with each
verification result. Reject completion if any binding differs from current
state.

The existing login snapshot ID can identify historical login evidence, but the
current adapters do not expose a shared live session corresponding to that
snapshot. Persisted login evidence alone must not be treated as proof that a
later independently authenticated preferences call or future order used the
same account token.

### Alternatives Reassessment

Alternative 1 remains the strongest foundation, but only in narrowed form: a
dedicated current-state aggregate, an observe-only reconciler by default, and a
separate explicitly authorized remediation path.

Alternative 2, deriving state from the event log, still fails the retention and
query-safety tests. Adding a projection, revisions, and concurrency controls
would recreate Alternative 1 with more moving parts.

Alternative 3, extending platform configuration and authentication
reconciliation, can store target account selection but should not own preference
convergence. It conflates authentication health with account-specific desired
state and risks adding IG work to the one-second auth loop and blocking startup
path.

Alternative 4 is useful only as interaction semantics for explicit remediation.
The HTTP request may request an immediate attempt, but durable state and one
reconciliation implementation are still required. A synchronous provider call
must not be the sole completion path.

### Incremental Implementation Order

1. Fix the current failure presentation without changing authority semantics.
   Load current preference and history independently, preserve any successful
   current result when history fails, surface the request-specific Problem
   Details, and pass `eventStore` on expected GET failures. Capture the two
   browser responses before asserting a concrete runtime root cause.
2. Decide and persist the explicit target IG account ID. Confirm whether one
   login can address multiple linked accounts and define account switching.
   This decision precedes a trustworthy current-state key.
3. Add the dedicated aggregate and migration with `Unconfigured` bootstrap,
   desired revision, nullable observed state, verification metadata, actor,
   correlation ID, and optimistic concurrency. Populate account identity on new
   observations. Do not backfill desired intent from retained observations.
4. Change the page query to a SQL-only desired/observed projection. Display
   desired state as primary only after explicit initialization. Keep history
   independently degradable.
5. Add the account-bound, observe-only reconciler and due-work scheduling.
   Trigger it after usable authentication, desired changes, and manual retry,
   but do not put it in the fatal API startup scope.
6. Add explicit remediation with same-session `GET`, optional `PUT`, and
   confirming `GET`. Preserve revision and lease protections. Decide whether
   `Save and apply` creates durable authorization or whether apply remains a
   separate action.
7. When an order-producing slice is introduced, add the pre-order fail-closed
   policy there. Do not create a placeholder that claims trading safety before
   order, exposure, strategy, and risk boundaries exist.

## Final Recommendation

Select Alternative 1 with three constraints: desired state has an explicit
unconfigured bootstrap, reconciliation is observe-only by default, and all
verification or remediation evidence is bound to an explicit account and one
session generation. Use Alternative 4 only as the operator-facing trigger for
an explicitly authorized apply attempt delegated to the same durable workflow.

The resulting product contract is:

* SQL desired state is primary operator intent.
* Last observed IG state is secondary evidence, never a fallback intent value.
* The current page warns on drift, unknown, stale, or failed verification.
* Preference verification does not control API startup availability.
* Future order submission fails closed on absent or invalid account-bound
  evidence.
* External drift does not cause an automatic provider write without explicit,
  revision-bound operator authorization.

This approach addresses the present UI failure first and introduces truthful
state semantics before any trading-safety claims.

## Corrections to the Primary Report

* Add explicit references to the intended three subagent reports. The primary
  currently links only the workflow-alternatives report.
* Recast missing or undecryptable Demo credentials as the leading code-supported
  hypothesis, not the established cause. No retained response or request trace
  proves the failing browser session's status.
* Add `Unconfigured` bootstrap semantics. The conceptual contract's non-nullable
  desired Boolean and account ID incorrectly imply that trustworthy desired
  state already exists or can be safely backfilled.
* State that desired database state is authoritative for operator intent, while
  IG remains authoritative for observed external state. Avoid calling either
  one the unqualified authoritative `current preference`.
* Replace generic reconciliation `Blocked` status with factual preference
  states. Reserve trading-block decisions for a future eligibility policy.
* Make every trading gate and pre-order check explicitly future-facing. No order
  submission implementation exists in the current checkout.
* Clarify that startup verification is best effort and non-fatal to API
  readiness. The current fatal startup scope should not gain preference
  verification.
* Correct the session-binding claim. The present PUT and confirming GET use
  separate newly created sessions, and the preference gateway does not return
  `currentAccountId`. Same-account, same-session confirmation requires an
  adapter or session-boundary change before it can be claimed.
* Make the reconciler observe-only on unrequested drift. Put provider mutation
  behind an explicit remediation authorization, and preserve read-before-write
  retry semantics.
* Treat `202 Accepted` and an expanded PUT response as an API compatibility
  decision, not an unconditional default. Existing clients expect confirmed
  provider state, so versioning or a coordinated contract migration may be
  required.
* Retain the identified event-store fix, but classify it as observability work,
  not a cause of the load failure.

## Remaining Follow-Ups

* [ ] Capture the current and history request status codes and Problem Details
  from a failing browser session.
* [ ] Confirm the target account and desired trailing-stops business rule with
  the product owner.
* [ ] Exercise two linked IG Demo accounts to prove login selection, preference
  scope, and account-switch behavior.
* [ ] Confirm the exact IG business effect and account-type support of
  `trailingStopsEnabled`.
* [ ] Audit any API consumers before replacing the existing live GET and
  confirmed PUT contracts.
* [ ] Define verification freshness, retry exhaustion, notification, and
  operator override policy.
* [ ] Define deal-level exposure reconciliation before implementing strategy
  startup or order submission.
* [ ] Prototype aggregate concurrency so stale verification and remediation
  completions cannot overwrite a newer desired revision.

## Clarifying Questions

No clarification is required to select the architecture. Product input remains
required for the target account, initial desired value, whether `Save and apply`
constitutes durable remediation authorization, and the future verification
freshness threshold.