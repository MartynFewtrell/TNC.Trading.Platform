---
title: Account Preferences Save Reconciliation Intent Research
description: Evidence-backed analysis of account-preference save and IG reconciliation behavior.
author: GitHub Copilot
ms.date: 2026-09-19
ms.topic: reference
status: complete
keywords:
  - account preferences
  - reconciliation
  - trailing stops
  - IG
---

## Status

Complete. Product code and tests were not changed.

## Research Scope

Verify whether saving a selected trailing-stops preference establishes local desired
state and remediates IG drift. Compare the save handler, check handler, gateway,
state model, operation and audit records, endpoints, UI or presenter, and tests.

## Verified Findings

### Save establishes desired state only after IG confirmation

The stated intent is substantially correct: Save accepts the operator's
selected trailing-stops value as the value to converge to, and a successful
Save immediately remediates IG before persisting the new local desired state.
It is not a local-only desired-state write followed by eventual remediation.

1. `PUT /api/platform/account-preferences` requires a Boolean
  `trailingStopsEnabled` value and an `Idempotency-Key`, resolves the actor
  from the authenticated principal, and sends the selected value and expected
  revision to `SaveAccountPreferencesHandler`. See
  `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:8-19`.
2. Save obtains the latest successful login snapshot. It uses
  `snapshot.CurrentAccountId` as the first-save target, creates an operation,
  acquires the per-account reconciliation lease, and calls
  `gateway.RemediateAsync(targetAccountId, selectedValue)`. See
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:17-46`.
3. The gateway creates a fresh IG session, verifies its `currentAccountId`
  equals the target account, reads the preference, issues an IG `PUT` only
  when the observed value differs, then reads back to confirm the result. See
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:35-49`.
4. Only a confirmed matching IG value reaches `FinalizeConfirmedSaveAsync`.
  That transaction writes local desired state, a `DesiredStateChanged` audit,
  the confirmed observation, and the operation's `Completed` phase. See
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:46-63`
  and
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:28-39`.
5. The API returns the resulting state, and the Web presenter reports
  `Trailing stops preference saved and confirmed.` See
  `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:32-42`
  and
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:43-64`.

The focused unit test proves that a normal Save calls remediation once before
finalization. The functional test proves the returned projection has equal
desired and observed values and an `InSync` status after IG confirms it. See
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs:9-20`
and
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:14-37`.

### Check detects and remediates drift

`POST /api/platform/account-preferences/check-status` is the explicit drift
repair operation. It loads durable state, validates that the latest login
snapshot remains bound to the same account, observes IG, and calls
`RemediateAsync` if the observed Boolean differs from the persisted desired
Boolean. It then writes the confirmed observation and projects `InSync`. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:15-38`.

The direct handler test and browser E2E test cover this behavior: when IG is
different, Check calls remediation and leaves the UI with the desired value as
the last confirmed setting. See
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:35-48`
and
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:53-64`.

The scheduled reconciliation path is narrower. The one-second supervisor
invokes `ReconcileAccountPreferencesHandler`; that handler observes IG and
projects `InSync`, `Drifted`, or `VerificationFailed`, but does not call
`RemediateAsync`. Thus periodic reconciliation identifies drift, while Save
and Check are the confirmed remediation paths. See
`src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs:12-27`
and
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:13-31`.

### Same-value saves and idempotency are distinct

A new Save request for a value already stored as desired is not a local no-op.
Save has no equality short-circuit for `state.DesiredTrailingStopsEnabled`.
It still starts an operation, calls gateway remediation, and, on confirmation,
finalizes a new desired-state commit. `CommitDesiredStateAsync` unconditionally
increments the desired revision and writes a `DesiredStateChanged` audit row,
even when `PreviousValue` and `NewValue` are equal. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:37-63`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:13-26`.

At IG, the same-value path is externally write-idempotent: the gateway reads
the current preference and returns without issuing a provider `PUT` when it
already equals the requested value. It still treats the read as confirmation.
See
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:40-46`.

If the desired value is unchanged locally but IG has drifted, the new Save
repairs the drift before producing the new local revision. This is a direct
consequence of the gateway's read-difference-write-read sequence.

Request idempotency is separate. The operation store looks up a key by
platform and broker environment before a new operation begins. Reusing the
same key with the same account, requested value, and actor returns the current
state and original operation phase without another gateway call. Reusing it
with any of those values different returns a conflict before provider I/O. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:21-27`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesOperationStore.cs:8-13`.

The handler test and functional test prove matching-key replay makes no second
gateway remediation and returns HTTP 200. See
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs:22-40`
and
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:41-57`.

### State, operation, observation, and audit records

Current state contains separately durable desired and observed values,
account identities, desired revision, verification status, retry data, and
correlation information. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesCurrentState.cs:5-25`.

Each Save operation records its idempotency key, platform and broker
environments, target account, baseline revision, requested value, actor,
correlation ID, and phase. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesOperation.cs:5-17`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountPreferencesOperationEntity.cs:3-17`.

Each successful local desired-state commit emits an audit containing previous
and new values, revisions, actor, account, correlation ID, and change type.
See
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountPreferencesDesiredStateAuditEntity.cs:3-18`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:18-26`.

Confirmed Save and Check observations are separately persisted. Check labels
an observation `Checked` or `Recovered` depending on whether a prior
`RemoteApplied` operation exists. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:25-38`.

### Account mismatch boundaries

The target account is durable account-preferences state, not a browser input.
State is selected by platform and broker environment. Save first requires a
latest login snapshot. On first Save it binds the target to that snapshot; on
later Save it rejects before provider I/O when durable target and snapshot
account differ. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:17-33`.

Check rejects before preference I/O when the snapshot is absent or has a
different account ID. The gateway repeats the account comparison against a
fresh IG session immediately before observation or remediation. Neither guard
silently rebinds desired state to a different account. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:17-20`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-23,35-43`.

The existing account-validation and single-account mismatch research correctly
documents these identity boundaries and the need for an explicit rebind
workflow. See
`.copilot-tracking/research/subagents/2026-09-19/account-preferences-account-validation-research.md`
and
`.copilot-tracking/research/subagents/2026-09-19/account-preferences-single-account-mismatch-research.md`.

## Corrections to the Stated Intent

* Correct: the selected trailing-stops value is the desired state that both
  Save and Check use to converge IG, provided the account checks pass.
* Correct with an important timing clarification: a successful Save remediates
  and confirms IG immediately, then persists local desired state. It does not
  persist a desired state first and rely on background reconciliation to apply
  it later.
* Correction: automatic remediation after drift is performed by Check status
  and by a new Save. The background reconciliation supervisor detects and
  records drift but does not itself repair IG.
* Correction: selecting the existing desired value is not a local no-op. A
  new idempotency key produces a new operation, revision, desired-state audit,
  and confirmed observation. IG avoids a `PUT` only when its already-read
  value matches.
* Correction: a same-key retry is the idempotent replay. It bypasses provider
  I/O only when account, actor, and requested Boolean match the earlier
  operation. It is not a generic equality check.
* Correction: account identity is a hard boundary. Neither Save nor Check may
  move desired state to a newly authenticated IG account; an intentional
  account change needs a separate authorized, audited rebind workflow.

## Recommended User-Facing Wording

Use these distinct messages so the UI describes the action that actually
completed.

* Save success: `Trailing-stops preference saved and confirmed with IG.`
* Check success after matching IG: `Trailing-stops status matches the saved preference.`
* Check success after drift correction: `Trailing-stops drift was corrected and confirmed with IG.`
* Save or Check account mismatch: `The authenticated IG account does not match the account these preferences are bound to. Reauthenticate with the intended account, then check status. Contact an administrator to change the configured account.`
* Unknown provider outcome: `IG did not confirm the preference change. The saved state may be unchanged. Check status before trying again.`

Do not show account IDs, credentials, session tokens, or provider response
bodies in browser messages. The account-mismatch recommendation aligns with
the prior research, while the drift-specific Check wording reflects the
currently verified remediation behavior.

## Required Additions to Primary Research

Add the following to the primary account-preferences research, especially
`.copilot-tracking/research/subagents/2026-09-19/account-preferences-account-validation-research.md`.

1. State that Save is IG-first and confirmation-gated: remote remediation and
  readback precede the atomic local desired-state, observation, audit, and
  completed-operation commit.
2. Document that Check status automatically remediates a detected Boolean
  drift, but the periodic reconciliation supervisor only observes and projects
  drift. Do not describe all reconciliation as automatically corrective.
3. Add the same-value matrix: same desired plus matching IG creates a new
  local revision/audit but no IG `PUT`; same desired plus drifted IG corrects
  IG then creates a new local revision/audit; matching idempotency-key replay
  performs no gateway call.
4. Distinguish operation records from desired-state audits and observations:
  operation phase tracks the request, the audit tracks each desired-state
  commit, and observations track confirmed provider values.
5. Add the idempotency scope and invariant: key lookup is partitioned by
  platform and broker environment, then replay is allowed only for matching
  account, requested Boolean, and actor. Expected revision does not prevent a
  matching-key replay because the replay branch precedes stale-revision
  validation.
6. Retain the prior account-mismatch conclusion: application and fresh-session
  gateway guards stop wrong-account reads or writes, and no automatic rebind
  exists.

The implementation has tests for normal Save confirmation, matching-key
replay, Check drift correction, and UI Check correction. It does not have a
focused test that asserts same-value Save increments the revision and audit
while suppressing the IG `PUT`; add that coverage if implementation work is
opened.

## Evidence References

* Save handler: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:15-63`
* Check handler: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:15-46`
* Background reconcile handler: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:13-31`
* IG gateway: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-49`
* Current-state store: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:8-72`
* Operation store: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesOperationStore.cs:8-51`
* HTTP request boundary: `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:8-19`
* UI presenter: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:43-76`
* Save tests: `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs:9-40`
* Check tests: `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:35-48`
* Functional tests: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:14-57`
* E2E drift correction: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:53-64`

## Open Questions

* The current Check response does not expose whether it made an IG correction.
  The persisted observation kind is `Checked` or `Recovered`, but the page
  confirmation does not distinguish matching status from corrected drift.
* Product policy still needs to define the authorization, confirmation, and
  audit requirements for intentional rebinding to a different IG account.