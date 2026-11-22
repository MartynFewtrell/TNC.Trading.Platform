---
description: 'Implement evidence-led IG collection recovery and finish with a guarded local Demo database reset.'
applyTo: '.copilot-tracking/changes/2026-10-04/ig-market-data-recovery-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: IG market-data recovery

## Overview

Implement safe diagnosis and conservative, contract-gated IG collection, validate the changes, then reset scoped local Demo database state so a fresh market-data request can be admitted.

## Objectives

### User requirements

* UR-01: Follow the recommendations in `.copilot-tracking\research\2026-10-04\ig-category-page-count-session-denial-research.md`. Source: attached research and user request, 2026-10-04.
* UR-02: Make the last implementation stage reset database data to enable a fresh market-data request; save a reusable reset script in `infra\scripts`, run that exact saved script once against the intended local database, and record proof that it works. Source: user requests, 2026-10-04 (including 13:57 clarification).

### Derived objectives

* DO-01: Preserve complete-only publication and last-good snapshots on collection failures; diagnostics cannot establish undocumented provider semantics.
* DO-02: Use a fixed diagnostic page size of 25, five-second background request-start spacing and a 60-second inter-category pause, while preserving distributed quotas, daily budgets, cancellation and lease fencing.
* DO-03: Distinguish initial login, reauthentication and instruments failures, carry only allow-listed provider codes through persistence/API/UI, and account for every actual HTTP attempt.
* DO-04: Leave the final reset safe, auditable and repeatable without erasing consumption or provider restrictions.
* DO-05: Finish implementation documentation and full offline validation before the database reset; do not automatically start workers or issue a live request afterward.

## Context summary

### Evidence

* Primary research: `.copilot-tracking\research\2026-10-04\ig-category-page-count-session-denial-research.md` (lines 89-177, 196-240, 271-363, 386-440, 467-506).
* Reset and implementation supplement: `.copilot-tracking\research\subagents\2026-10-04\ig-market-data-recovery-planning-research.md` (lines 27-149).
* The 70/41 mismatch occurs before mapping. The observed session403 has no recovered provider error body. Fixed-size traversal is a diagnostic recommendation, not permission to accept an incomplete union.
* Existing `.copilot-tracking\plans\2026-10-04\ig-market-collection-failures-plan.instructions.md` is separate historical work. Do not overwrite it or reuse its completion claims.
* Source anchors describe the researched worktree. Reconcile current edits and schema at implementation time; no database state, live provider behavior or deployed binary was verified for this plan.

### Architecture and affected areas

* Application features own collection policy, retries, budgets, schedule decisions and lease-safe orchestration.
* IG adapters own bounded response parsing, provider pagination, actual sends and scoped pacing.
* EF stores own snapshots, diagnostics, fences and run-generation identity.
* Existing configuration contracts, dedicated API mappings and Radzen UI must carry new SQL-backed operator settings and safe diagnostics consistently.
* `infra\scripts\reset-ig-demo-market-data-recovery.sql` is a new maintenance asset, not an application startup hook.
* Planned exact file operations and existing anchors appear in the details file. New migrations receive generated names during implementation.

### Standards and external references

* `.github\copilot-instructions.md`; `.github\instructions\architecture.instructions.md`; `.github\instructions\configuration.instructions.md`; `.github\instructions\csharp.instructions.md`; `.github\instructions\refactoring.instructions.md`.
* `.github\instructions\tests.instructions.md`; `.github\instructions\aspire-tests.instructions.md`; `.github\instructions\auth.instructions.md`; `.github\instructions\radzen-blazor.instructions.md`; `.github\instructions\scalar.instructions.md`; `.github\instructions\docs.instructions.md`.
* Re-read applicable scoped standards before product edits. Keep existing .NET targeting, one top-level type per C# file and documented `MethodName_StateUnderTest_ExpectedResult` tests.
* IG category/session/allowance references are linked in the primary research (lines 75-85).
* Use https://aspire.dev/ as the primary reference for future lifecycle operations; retain Docker SQL Server and Keycloak when stopping collection writers.
* Use Microsoft Learn resilience references in primary research (lines 352-358) when verifying installed handler behavior.

## Implementation checklist

### [x] Phase 1: Safe diagnosis and exact regressions

<!-- parallelizable: false -->

* [x] Step 1.1: Add bounded shared provider-code parsing and distinguish authentication phase without changing existing operation meaning.
* [x] Step 1.2: Carry code, parse outcome, phase and paired pagination evidence through all relevant contracts, persistence, API, logs and UI.
* [x] Step 1.3: Test exact initial/restarted page-zero 70/41, initial session403 and page401/relogin403; preserve last-good data and attempt/HTTP distinctions.
* Details: `.copilot-tracking\details\2026-10-04\ig-market-data-recovery-details.md` (lines 13-39), section "Phase 1".

### [x] Phase 2: Wired conservative profile and lifecycle safety

<!-- parallelizable: false -->

* [x] Step 2.1: Add validated SQL-backed collection settings and wire page size 25, request interval five seconds and category pause 60 seconds through the existing settings read/update/API/UI/DI path.
* [x] Step 2.2: Select gateway-owned bounded retry accounting; remove competing standard resilience handlers only from affected background collection clients and retain explicit timeout/cancellation behavior.
* [x] Step 2.3: Pace actual sends, including session/relogin/retries; preserve shared quota reservations, daily accounting and time-sensitive trading behavior.
* [x] Step 2.4: Renew parent and child leases during requests and waits; align gateway/coordinator schedule continuation and detail capacity estimates.
* [x] Step 2.5: Validate measured spacing, cancellation, lease loss, quota exhaustion and schedule boundaries with fake time and production DI-built HTTP clients.
* Details: `.copilot-tracking\details\2026-10-04\ig-market-data-recovery-details.md` (lines 40-77), section "Phase 2".

### [ ] Phase 3: Bounded diagnostic traversal and provider-contract gate

<!-- parallelizable: false -->

* [x] Step 3.1: Add an explicit diagnostic-only fixed-size traversal that bypasses adaptive shrinking and cannot publish snapshots.
* [x] Step 3.2: Retain the 100-page/15,000-result bounds initially; report the size25 effective 2,500-row capacity explicitly and reject 2,501 rather than silently raising bounds.
* [ ] Step 3.3: After lifecycle/accounting tests pass, conduct one separately approved paced Demo diagnostic and obtain provider clarification; export only sanitized metadata/counts.
* [x] Step 3.4: Apply only the confirmed completeness contract. If unavailable, finish with explicit incomplete/unknown behavior and retain existing publication validation.
* [x] Step 3.5: Test twelve fixed-size page GETs for a coherent total290, sparse pages, short unions, duplicates, drift, repeated/wrong pages and bounded termination.
* Details: `.copilot-tracking\details\2026-10-04\ig-market-data-recovery-details.md` (lines 78-101), section "Phase 3".

### [x] Phase 4: Classified denial recovery and fresh detail generations

<!-- parallelizable: false -->

* [x] Step 4.1: Implement scope-appropriate cooldown for classified allowance failures; expose key/account/product configuration denials and keep unknown403 nonretryable.
* [x] Step 4.2: Reproduce same-slot provenance supersession. If reproduced, add an explicit new update generation through acquisition, uniqueness, sources, leases and readers without rewriting historical provenance.
* [x] Step 4.3: Verify later eligible collection can progress with new IDs and complete sources, while prior failures and provider-confirmed exclusions remain distinct.
* Details: `.copilot-tracking\details\2026-10-04\ig-market-data-recovery-details.md` (lines 102-124), section "Phase 4".

### [ ] Phase 5: Guarded reset tooling and isolated SQL tests

<!-- parallelizable: true -->

* [x] Step 5.1: Save reusable `infra\scripts\reset-ig-demo-market-data-recovery.sql` with parameterized inputs, documented future-use invocation, dry-run default, scoped apply confirmation, preservation assertions, locking and rollback.
* [ ] Step 5.2: Default to failed-slot rearm preserving snapshots/history/consumption; support explicit dependency-closed cleanup only where blockers require it.
* [x] Step 5.3: Test the actual SQL asset against fixture-owned SQL, including FKs, diagnostics, budget preservation, scope rejection, concurrency, rollback and fresh admission.
* Details: `.copilot-tracking\details\2026-10-04\ig-market-data-recovery-details.md` (lines 125-157), section "Phase 5".
* This phase can be authored alongside phases 1-4 because it owns a new script and a new test file. It must consume the final agreed schema and cannot execute shared builds/tests concurrently. Final integration validation is sequential.

### [x] Phase 6: Documentation and full pre-reset validation

<!-- parallelizable: false -->

* [x] Step 6.1: Update affected wiki runtime, operator, local-development and testing pages to describe implemented behavior, diagnostic limits and the reset retention contract.
* [x] Step 6.2: Run targeted tests, full solution build and serialized full suite using repository tooling; include migration and API/UI regression checks.
* [x] Step 6.3: Fix isolated regressions; record broader blockers without performing unrelated refactoring.
* [x] Step 6.4: Approve reset readiness only after code/schema/docs and offline checks pass. Record any unresolved provider evidence separately from implementation defects.
* Details: `.copilot-tracking\details\2026-10-04\ig-market-data-recovery-details.md` (lines 158-183), section "Phase 6".

### [ ] Phase 7: Reset local Demo database data and prove fresh-request eligibility

<!-- parallelizable: false -->

* [ ] Step 7.1: Resolve exact local database/environment/day/slot and reset scope; dry-run counts and preserve a verified, access-controlled recovery backup.
* [ ] Step 7.2: Stop all collection writers, verify no in-flight work, and wait for lease expiry or use tested fenced cancellation. Leave infrastructure available.
* [ ] Step 7.3: Execute the exact saved `infra\scripts\reset-ig-demo-market-data-recovery.sql` once in apply mode against the verified local database, clearing stale attempts/diagnostics, prerequisite state, blocking slot coverage/intents/follow-ups and only required dependent artifacts; do not substitute ad hoc SQL.
* [ ] Step 7.4: Assert preserved credentials/settings/interests/revisions/history, consumed budget, shared reservations and provider-confirmed exclusions; assert pending work, no stale blocking run and valid detail-generation readiness.
* [ ] Step 7.5: Record script path/SHA-256, sanitized invocation inputs, one successful apply execution/transaction result, before/after counts, preservation checks, baseline IDs/timestamps and next eligible schedule slot with remaining allowance; leave workers stopped and retain the script for future use.
* Details: `.copilot-tracking\details\2026-10-04\ig-market-data-recovery-details.md` (lines 184-214), section "Phase 7".
* This is deliberately the LAST stage. Its postconditions are read-only validation inside the reset stage, not a later startup or live-collection stage.
* Do not claim fresh request readiness if the schedule is closed, the preserved allowance is insufficient or provider access is denied. Identify the genuine next eligible window or blocking condition without manufacturing capacity.
* Current preflight: no local reset was attempted. SQL is reachable, and the persisted-profile migration is now applied; the detail-generation migration is still missing. The active Demo target's registered SQL Server name differs from its machine name, so the saved script's local-target guard cannot currently pass. Resolve and revalidate these conditions before backup, dry-run or mutation. See planning-log discrepancy DD-08.

## Planning log

`.copilot-tracking\plans\logs\2026-10-04\ig-market-data-recovery-log.md` records decisions, evidence gaps, alternatives and validator findings.

## Dependencies

* Existing SDK/global.json/solution targets; existing tests and quality tooling.
* Docker SQL Server for fixture-owned integration tests and local recovery; Keycloak remains the local authentication standard.
* Schema-compatible EF migrations if settings, diagnostics or generation identity require them.
* An approved Demo diagnostic/provider response for changing completeness semantics, not for adding safe diagnostics or retaining fail-closed behavior.
* An identified safe local reset target, recoverable backup, stopped writers, valid schedule and sufficient preserved allowance for final execution.

## Success criteria

* Safe session code/phase and original/restarted metadata survive persistence and UI without secrets or raw provider text. Traces to UR-01, DO-03.
* Fixed25 diagnostic traversal requests only bounded advertised pages, and an exact290 fixture yields twelve page GETs and 290 unique valid rows; no diagnostic run publishes. Traces to UR-01, DO-01.
* Fake-time/DI tests show starts at least five seconds apart, 60 seconds between category completion and next category start, each actual attempt budgeted once, and lease loss/cancellation stops sends. Traces to UR-01, DO-02.
* Production accepts only a confirmed completeness rule; missing evidence remains incomplete/unknown and last-good data survives failures. Traces to UR-01, DO-01.
* Same-slot new work cannot impersonate historical superseded provenance. Traces to DO-04.
* Build/tests pass and affected wiki pages are current before reset. Traces to DO-05.
* The reusable script remains saved in `infra\scripts`; the final stage executes that exact file once in apply mode on the intended local database and records successful transaction plus before/after/preservation evidence, not merely fixture tests or a dry-run. It proves nonmutating prerequisites for a new Categories-first full run; consumption and genuine exclusions are unchanged. Traces to UR-02, DO-04.
* No automatic provider request, worker restart, trading request, Live reset or quota erasure follows the reset. Traces to UR-02, DO-05.
