---
title: Trailing Stops Remediation Options Research
description: Decision record for account-preferences CSS alignment and account-binding recovery alternatives.
ms.date: 2026-09-19
ms.topic: reference
---

## Research Scope

Evaluate alternatives identified in the following completed research artifacts:

* `.copilot-tracking/research/subagents/2026-09-19/trailing-stops-ui-layout-and-commands-research.md`
* `.copilot-tracking/research/subagents/2026-09-19/trailing-stops-account-binding-research.md`

Assess immediate user action versus future product work. This document does not
modify product code.

## Status

Complete. The selected approach is a small CSS realignment plus the existing
fail-closed reauthentication recovery procedure. Do not replace UI components,
manually update the database, implement administrator rebinding, or introduce
account-ID canonicalization in the immediate change.

## Decision Summary

| Decision area | Selected approach | User action now | Future implementation |
| --- | --- | --- | --- |
| CSS layout | Realign the existing flex row in place | Approve a two-rule CSS adjustment and a focused E2E expectation update | None unless product direction requires a different responsive arrangement |
| UI components | Retain native Blazor radios and HTML buttons | No component replacement | Reassess only if a broader design-system migration is approved |
| Account mismatch | Preserve fail-closed binding and recover through reauthentication | Authenticate the intended IG Demo account, then run Check status | Add an explicit, audited administrator rebind only for intentional account changes |
| Database recovery | Do not directly modify account binding rows | Escalate intentional account changes to an administrator | No SQL runbook; the rebind operation becomes the supported path |
| Account-ID comparison | Preserve ordinal raw-ID comparison | Treat a case or whitespace difference as a mismatch | Canonicalize only after IG documents the identifier semantics and all boundaries can change together |

## Evidence and Dependencies

### UI composition and user-visible behavior

The settings view uses a native `InputRadioGroup<bool>` with two native
`InputRadio<bool>` controls and a normal Save button. `RadzenTabs` divides the
Settings and Observed history views; Radzen does not own the affected controls.
The page is interactive-server rendered, so an otherwise healthy page supports
button events.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:1-8`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:21-59`
* `src/TNC.Trading.Platform.Web/Program.cs:12-14`
* `src/TNC.Trading.Platform.Web/Program.cs:70-73`
* `src/TNC.Trading.Platform.Web/Components/App.razor:13-16`

The Check status action begins at the left edge because its shared actions
container has no `justify-content` override. Save appears farther right because
its preceding fieldset consumes spare row width through `flex: 1 1 auto`; the
same row uses `align-items: end`.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:25-37`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:39-59`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:57-87`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:89-105`

The page shows the stable mismatch or provider reason in a live alert. A
mismatch does not disable controls after completion; controls are disabled only
while loading, saving, or checking. Check status is absent without a desired
revision and the editor is absent without a confirmed value.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16-20`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:35-37`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:39-55`

### Account binding and safety controls

The durable preference state is bound to the account ID in the most recent IG
authentication snapshot. Web-user claims are used only as an audit actor, not
as broker identity. The account ID is supplied by IG and is currently preserved
and compared as an ordinal string.

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditEventResolver.cs:28-37`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:15-31`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:15-21`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:16-27`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:36-49`

Save returns HTTP conflict for a durable-state versus snapshot mismatch and
makes no provider preference request. Check status returns the existing durable
state with a verification failure and likewise makes no provider preference
request. The stable, non-identifying operator message explicitly directs the
operator to reauthenticate with the intended account and then check status.

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesAccountMismatch.cs:4-9`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:32-58`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:22-42`
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:115-136`

The current state uses revision checks, a per-account reconciliation lease, and
transactional save finalization. It also writes desired-state audit rows and
preference observations. A raw database update would bypass these protections,
the operation journal, actor/correlation evidence, and the IG confirmation
step.

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:32-58`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:12-47`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:49-72`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260831130930_AddAccountPreferencesCurrentState.cs:15-82`

Administrator authorization already exists as a distinct policy and API
surface, while current account-preference commands require the less-privileged
Operator policy. This supports an administrator-only future rebind without
silently expanding ordinary operator authority.

* `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs:43-95`
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:46-58`

The feature is constrained to the Test platform and Demo broker environment;
any future rebind design must preserve that policy rather than generalize to a
live account.

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:5-9`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:19-21`

## Alternatives Assessment

### CSS realignment in place

**Selected.** Remove `flex: 1 1 auto` from
`.account-preferences-choice` and use `align-items: center` on
`.account-preferences-control-row`. Retain the existing wrap and gap.

This makes Save follow the natural-width choice group, visually aligns the
controls, retains one compact desktop workflow, and preserves native radio
semantics and keyboard behavior. It is limited to
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:68-105`.

### Separate action line

Rejected for the immediate change. A standalone, left-aligned Save line would
match Check status but turns a compact select-and-commit operation into a
taller two-step scan path. It is a legitimate future product-design option if
mobile-first stacking is expressly required, not a repair required by current
markup or accessibility.

### Component replacement

Rejected. No interaction defect was found in the component choice. Native
Blazor radio controls already provide semantic grouping, focus behavior, and
arrow-key interaction, which is browser-tested at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:127-143`.
Replacing them with Radzen inputs would add an interaction migration while
leaving the responsible flex growth rule unchanged.

### Preserve binding fail-closed with reauthentication

**Selected for operations now.** It stops a potentially destructive write to a
different broker account before gateway preference I/O. The handler and
gateway independently enforce the comparison, which is defense in depth.

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:15-24`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:16-27`

### Manual database update

Rejected. It cannot prove the target IG session account, omit an expected
revision check, leave the current audit change type misleading, and bypass the
lease, provider observation, and transaction boundaries. It would turn a
deliberate fail-closed control into an unaudited cross-account write path.

### Explicit audited administrator rebind

**Selected for future implementation, not required for the present mismatch.**
It is the only evaluated approach that can safely support an intentional change
of configured account. The current audit store and administrator policy are
useful dependencies, but neither represents an account-binding transition yet.
Implementing it requires new command, API, persistence, audit, and UI work.

### Conditional canonicalization

Deferred. The current ordinal comparison is conservative and protects against
ambiguous identifiers. Do not trim or change case until IG documents that
`currentAccountId` is case-insensitive and whitespace-insignificant. If that
contract is verified, apply canonicalization at every identity boundary:
authentication capture, durable bind, snapshot versus state comparison,
gateway session comparison, lease key, reconciliation query, idempotency
comparison, and migration/backfill. A one-location comparison change would
split identity domains and could reintroduce cross-account risk.

## Recommended End-to-End Action

### Action now

1. Apply the narrowly scoped CSS realignment described above and update the
   desktop geometry assertion. Do not change markup, presenter, API, or
   component library.
2. For the active mismatch, restore or select the intended IG Demo account,
   authenticate successfully so the latest snapshot has the existing durable
   raw account ID, and select **Check status**. A successful check observes or
   remediates drift to the stored desired setting and clears the failure
   summary.
3. If reauthentication continues to report a different account ID, stop
   retries. Treat it as an intentional-account-change request or credential/
   IG-default-account incident; do not use Save to override the binding and do
   not edit the database.

The operational recovery sequence is supported by the existing stable warning
and Check handler behavior:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesAccountMismatch.cs:4-9`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:26-42`

### Future implementation sequence

1. Confirm business ownership, approval requirements, and whether the existing
   Administrator policy is sufficient for rebind authority.
2. Obtain and retain IG documentation for `currentAccountId` identity rules.
   Decide whether canonicalization is needed before defining rebind semantics.
3. Define a dedicated administrator-authorized rebind command with target
   account confirmation from a fresh IG session, expected durable revision,
   correlation ID, and explicit operator confirmation.
4. Acquire the existing account reconciliation lease before mutation; reject a
   stale revision or inability to verify the target account.
5. Persist a distinct binding-transition audit record containing old and new
   account identifiers, actor, time, correlation, expected and resulting
   revisions, and confirmation evidence. Preserve old observations and
   operations; do not rewrite their account identity.
6. Set the new binding only within a transaction that creates the audit record,
   then require Check status to confirm or remediate the retained desired value
   for the newly bound account.
7. Add an administrator-only API endpoint and explicit UI flow separate from
   the ordinary Operator settings page. Do not infer rebind intent from Save.
8. Update the operator guide and API reference with approval, recovery, audit,
   and non-retry behavior.

## Test Plan

### Immediate CSS change

* Update the desktop Playwright geometry assertion so Save starts shortly after
  the natural-width radio group rather than after an expanded fieldset. Preserve
  the no-horizontal-overflow assertion at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:87-108`.
* Add a desktop vertical-center tolerance check for the first choice label and
  Save after `align-items: center` is applied.
* Retain and strengthen narrow viewport coverage: both choices and Save remain
  visible and keyboard reachable without horizontal overflow at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:110-125`.
* Retain the keyboard native-radio regression at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:127-143`.

### Current operational safety

* Retain the unit test proving Check mismatch performs no observation at
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:18-27`.
* Retain the unit test proving Save mismatch performs no remediation or durable
  finalization at
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs:30-44`.
* Retain functional coverage that both commands preserve the old account and
  do not issue preference GET or PUT after account change at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:105-145`.

### Future rebind and canonicalization

* An authorized rebind must require a matching fresh IG session and expected
  revision, write one auditable binding transition, preserve prior history, and
  make subsequent Check and Save target only the new account.
* Authorization failure, stale revision, lease contention, target-session
  mismatch, provider timeout, and transaction failure must preserve the old
  binding and must not issue a preference update against the proposed account.
* Browser coverage must display the safe mismatch message, direct the operator
  through the approved administrator workflow, and show post-rebind check
  convergence.
* If provider documentation approves canonicalization, test equal normalized
  variants at every boundary and verify genuinely different accounts remain
  fail-closed. If the provider does not approve it, test case and whitespace
  variants as mismatches.

## Remaining Decisions

* Confirm the product owner accepts one-row natural wrapping with Save directly
  after the radio group, rather than a deliberate stacked action layout.
* Confirm whether an intentional account change is a supported business
  workflow. If it is not, do not build rebind; retain reauthentication-only
  recovery and document the escalation boundary.
* Confirm the administrator role's approval model and whether segregation of
  duties requires a separate policy or a two-person approval workflow.
* Obtain IG's authoritative `currentAccountId` case and whitespace semantics
  before scheduling canonicalization.