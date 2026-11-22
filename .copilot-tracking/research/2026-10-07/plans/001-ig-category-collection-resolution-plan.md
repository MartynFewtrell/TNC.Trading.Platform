---
title: IG category collection resolution plan
description: Phased implementation and evidence gates for category completeness diagnostics and coherent UI coverage.
date: 2026-10-07
status: proposed
---
<!-- markdownlint-disable-file -->

## Objective

Resolve the COMMODITIES category-collection failure without publishing incomplete membership, and correct the independently identified mixed-freshness UI behavior.

This is a standalone tracking plan based on .copilot-tracking\research\2026-10-07\ig-category-collection-completeness-research.md:8-211. No new work package is introduced. All tasks below are planned, not implemented or validated.

There are two delivery milestones:

* Milestone A: exact regression coverage, safe operator diagnostics and coherent refreshed UI coverage. This can proceed without assuming undocumented IG semantics.
* Milestone B: production collection recovery, only after an authoritative completion contract is established. If that evidence is unavailable, retain explicit incomplete results and last-good data; do not claim recovery.

## Constraints and non-goals

* Preserve existing user changes. The research inspected a dirty gateway; confirm the implementation baseline before editing.
* Keep scheduled collection fail-closed until the provider-contract gate passes.
* Do not accept 14 or seven rows as complete merely because they were returned.
* Do not overwrite provider total 294 with observed rows, union restarted baselines, recursively shrink, or stop on a short/empty nonfinal page.
* Preserve request allowance, throttling, cooldown, execution fencing, applied-profile checks, cancellation and atomic publication.
* Diagnostic traversal must never create a production snapshot or mark a category successfully collected.
* Do not introduce a new discovery endpoint, broad retry policy, authentication change or database migration without evidence that the selected resolution requires it.
* No live broker probes are part of ordinary automated validation. A diagnostic run requires explicit operator authorization and an approved budget.
* Do not change duplicate-name identity rules or unrelated market-detail collection behavior.

## Evidence anchors

Line references below describe the researched workspace and must be re-established against the implementation baseline.

| Concern | Source evidence |
| --- | --- |
| Page-zero restart and production completeness | src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:138-407 |
| Existing fixed-size diagnostic traversal | src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:462-501,603-729 |
| Request construction, raw parsing and mapping | src\TNC.Trading.Platform.Infrastructure\Infrastructure\Integrations\Ig\IgMarketCategoryInstrumentsGateway.cs:781-910,1053-1158,1315-1345 |
| Attempts, category independence and publication | src\TNC.Trading.Platform.Application\Features\MarketCategoryInstruments\MarketCategoryInstrumentCycleCoordinator.cs:416-510,647-779 |
| Complete-only persistence | src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentSnapshotStore.cs:19-232,291-329 |
| Persisted failure diagnostics | src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketCategoryInstrumentCycleStore.cs:1029-1096 |
| UI refresh and coverage sources | src\TNC.Trading.Platform.Web\Components\Pages\MarketCategories\MarketCategories.razor:73-204; MarketCategoriesPagePresenter.cs in the same directory:36-43,116-140 |
| Current-listing detail coverage | src\TNC.Trading.Platform.Infrastructure\Infrastructure\Persistence\EntityFramework\EfMarketDetailReadStore.cs:86-149,373-410 |
| Existing operational explanation | docs\wiki\runtime-behavior.md:278-301 |

## Execution order and dependencies

| Task | Work | Prerequisites | Completion gate |
| --- | --- | --- | --- |
| P0 | Establish baseline and implementation contracts | None | Applicable instructions loaded; existing changes and validation entry points identified |
| P1 | Add exact incident and safety regressions | P0 | Targeted gateway/coordinator/persistence tests pass |
| P2 | Extend bounded diagnostic evidence | P1 | Evidence and isolation tests pass without relaxing production checks |
| P3 | Wire authorized operator diagnosis | P2 | Invocation enforces shared admission/budget and cannot publish |
| P4 | Correct coherent UI refresh | P0 | Targeted UI/read-projection tests pass |
| P5 | Collect authorized evidence and settle provider semantics | P3 | Written contract decision with supporting evidence, or explicit blocker |
| P6 | Implement contract-backed production resolution | P1 and approved P5 decision | No-loss, termination and complete-only publication tests pass |
| P7A | Document and release Milestone A | P1, P2, P3, P4 | Focused validation and affected wiki updates complete |
| P7B | Document and release Milestone B | P6 and P7A | Contract-specific validation, wiki updates and authorized operational verification complete |

P4 is independent of provider investigation and can proceed alongside P1-P3. P7A must not wait for IG clarification. P6 and P7B remain blocked if P5 cannot establish an authoritative completion rule.

## P0: establish the implementation baseline

* Record active branch, HEAD and worktree status without stashing, reverting or committing unrelated changes.
* Load applicable architecture, C#, refactoring, auth, configuration, API, Blazor, test and documentation instructions for the files actually changed.
* Compare current gateway and diagnostics with the research evidence; identify already implemented portions and avoid duplicating them.
* Locate existing test projects, test platform, validation scripts, diagnostic invocation patterns and execution-admission helpers.
* Identify current DTOs for collection status and detail coverage, including whether refreshed status already exposes coverage and collection/version identity.
* Record the deployment fingerprint separately from source baseline before interpreting an eventual runtime probe.

Acceptance: a bounded implementation inventory maps each task to existing types/helpers and exact validation selectors. No product behavior changes occur in P0.

## P1: lock down the observed incident

Add controlled-handler tests in test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.UnitTests\Integrations\Ig\IgMarketCategoryInstrumentsGatewayTests.cs, extending existing fixtures at lines 351-403 and 536-717.

```text
Response 1: page 0, size 25, totalPages 12, totalResults 294, raw rows 14
Response 2: page 0, size 14, totalPages 21, totalResults 294, raw rows 7
Expected: IncompleteCollection / PageRowCountMismatch / instruments
Expected evidence: both page-zero responses, request ordinals 1 and 2
Expected final comparison: 14 rows expected, 7 actual
Expected calls in fresh-session fixture: session + initial + restart = 3
Expected: no page 1 request and no complete result
```

The total-page fixture values follow current formula validation; they were not captured in the user's log. Document that distinction in test traceability.

Extend coordinator and persistence coverage to prove one category attempt, no failed-result publication, last-good retention and success of a later selected category. Test raw counts before mapping to rule out accidental local-filter attribution.

Acceptance: exact diagnostic fields and counters match the incident; collection failure remains explicit and cannot mutate successful membership.

## P2: extend nonpublishing diagnostic traversal

Reuse the existing fixed-size implementation. Keep size 25, current 100-page/2,500-row limits and any stricter configured request allowance unless a separately justified change is approved.

* Add typed per-page evidence and a typed diagnostic outcome distinct from production `Complete`.
* Capture requested/returned page and size, metadata presence/type/value, raw JSON array count, DTO count, null count, validation failures, unique count and overlap/duplicate counts.
* Use one response body and the existing deserialization path; avoid introducing a second request solely to count rows.
* Keep identities local to the run. Emit counts rather than bodies, EPICs, instrument names, account IDs or arbitrary headers. Only allow-listed, reviewed correlation metadata may leave the process.
* Follow one fixed-size, zero-based provider-advertised page sequence under hard bounds. Never mix a restarted baseline or stop merely because a nonfinal page is short.
* Record formula/count disagreement as diagnostic evidence. Exploratory continuation is allowed only with structurally valid, usable and bounded page metadata; malformed or unsafe paging must terminate explicitly.
* Distinguish exhausted advertised sequence, incomplete membership, unusable metadata, budget exhaustion, cancellation and execution invalidation. Advertised-sequence exhaustion alone is not proof of completeness.
* Keep every outbound call behind existing throttling, reservation and execution-validity checks.

Tests must include a synthetic 294-candidate universe with exactly 14 visible rows in its first 25 candidates and seven in its first 14. Diagnose the visible union without promoting it to a complete production collection.

Acceptance: per-page discrepancies remain observable; all exits are bounded; no diagnostic branch reaches snapshot publication or reports production success.

## P3: authorized operator workflow

Prefer an existing repository task/script or diagnostic command surface. If none provides in-process admission and shared budget accounting, introduce the smallest explicit application diagnostic use case and reuse the established operator authorization pattern; do not create an unprotected API or bypass the coordinator.

* Require deliberate invocation for one category and environment with execution fencing and concurrency exclusion.
* Precompute and enforce a call ceiling including session acquisition and permitted reauthentication. Validate that the provider-advertised pages fit remaining allowance before traversal.
* Refuse unavailable allowance, schedule/context mismatch or conflicting execution explicitly through repository-standard error reporting.
* Return sanitized evidence and an explicit nonpublishing outcome.
* Do not run automatically after scheduled failures, reset saved success/failure state, alter interest or expose credentials in command arguments.
* Document how to invoke, interpret, cancel and retain the evidence. The operator must authorize any broker calls separately from implementation/testing.

Acceptance: authorized fake-handler workflow exercises admission and budget enforcement; denied or conflicting requests make no broker call; diagnostic execution cannot replace current rows.

## P4: coherent UI coverage

Implement in the existing page/presenter/read projection, choosing the smallest change supported by P0's DTO inspection.

* Prefer refreshed status coverage when already available and tied to the current listing identity.
* Otherwise refresh catalogue and status as a coherent projection, or extend the established read DTO so they carry matching collection/version provenance.
* Do not overwrite a newer projection with a late older refresh. Respect component cancellation/disposal and preserve operator interest edits.
* Never silently use old catalogue coverage as current. Represent unavailable coverage explicitly; label intentionally retained historical completion as historical.
* Preserve `NeverCollected`, `NotFollowed`, saved-attempt diagnostics and attempts-versus-HTTP counters.
* Make changed diagnostic dates unambiguous and zones explicit using existing formatting/localization helpers. Do not change stored trading-day semantics.

Add bUnit/presenter tests in test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\MarketCategoriesTests.cs:46-123 for initial 5 October coverage followed by a 7 October listing refresh, missing coverage, failed refresh, concurrent/out-of-order refresh and saved-read behavior. Extend read-store tests only if projection behavior changes.

Acceptance: the displayed collection result and current detail coverage share provenance, or historical/unavailable coverage is explicit. Reading saved status never triggers broker collection.

## P5: evidence and provider-contract gate

After explicit operator authorization, collect one bounded Demo COMMODITIES diagnostic with stable applied environment/account context. Record deployment fingerprint, UTC timing, category, paging metadata and sanitized per-page counts. Do not export raw responses or instrument identities.

Seek authoritative clarification from IG about:

* What category `totalResults` and `totalPages` count.
* Whether filtering occurs before or after paging, including account/product availability.
* Whether short or empty nonfinal pages are valid.
* Whether ordering/membership is stable throughout a traversal.
* What proves category exhaustion and account-visible completeness.
* Whether Demo differs from live environments.

Record evidence, the applicable environment/version, contradictions and the selected completion rule. Page arithmetic, third-party client behavior or one successful run alone cannot establish the contract.

Acceptance: either approve one of the production branches below with authoritative support, or record an explicit unresolved blocker. Lack of evidence does not authorize relaxing checks.

P5 status after Milestone A (2026-10-07): **BLOCKED pending authorized
evidence and an authoritative IG completion rule.** No broker call was made
for this implementation. The diagnostic route is available only to an
explicitly authorized Operator; P5 evidence collection has not been performed.

## P6: gated production resolution

| Confirmed semantics | Required implementation |
| --- | --- |
| Totals are exact visible unique instruments; sparse pages are permitted | Fixed-size traversal of authoritative pages; stable metadata, valid identities and exact final unique total. Remove only unsupported page/formula checks |
| Totals count candidates; authoritative visible exhaustion is provided | Represent candidate/visible counts and completeness proof separately; update adapter, inward metadata, publication validation and diagnostics together |
| Full visible pages are guaranteed but responses violate that contract | Keep incomplete failure; escalate evidence. Add bounded retry only if transience is established and allowance supports it |
| Semantics remain unknown or unstable | Do not implement a success path; retain last-good data and explicit incomplete state |

Any fixed-size production traversal must abandon the unsupported adaptive restart rather than union its baselines. No count relabeling, silent row dropping or inferred empty-page completion is allowed.

If metadata changes require persistence/API evolution, preserve nullable historical diagnostic compatibility and introduce migrations only when required. Keep provider payload types out of UI/application contracts.

Acceptance: confirmed completion proof governs the result; no missing or duplicate membership is published; late lease/profile/catalogue/full-run invalidation prevents publication atomically.

## Validation matrix

| Area | Required cases | Smallest validation scope |
| --- | --- | --- |
| Gateway | Exact incident, valid pages, sparse candidates, capacity contradiction, malformed metadata, null/invalid rows, duplicate/replayed pages | Targeted infrastructure gateway unit tests |
| Diagnostics | Raw/DTO counts, safe evidence, fixed-size paging, all bounded exits, no writes or production success | Diagnostic unit tests and invocation fake-handler tests |
| Coordinator | One attempt/three fresh-session calls, later-category success, last-good retention | Targeted coordinator unit tests |
| Publication | Incomplete rejection, immutable history, rollback, stale fences, changed metadata contract | Targeted persistence unit tests; Docker-backed SQL integration tests when affected |
| UI | Coherent refresh, missing/historical coverage, refresh failure/order, culture/zone, no broker calls on reads | Targeted web bUnit/presenter tests |
| Contract branch | Authoritative sparse/visible or candidate-exhaustion fixtures; zero results; complete-set and termination guarantees | Focused adapter/application tests, then affected integration tests |

Before executing .NET tests, load the `run-tests` skill and use the detected platform's selectors and existing scripts. Build the changed projects and their affected dependents. Escalate test scope only for shared-contract changes or targeted failures. Do not add test tooling or restore dependencies preemptively.

Tests must follow repository method naming and document requirement, expected outcome and risk. Passing fabricated fixtures verifies implementation against a selected contract, not IG's actual contract.

## Documentation, release and rollback

For Milestone A, update affected wiki pages for diagnostic invocation, safe evidence, retained last-good data, saved status, coherent coverage and known provider uncertainty. Locate the existing operator/testing pages rather than creating parallel documentation.

For Milestone B, update runtime, architecture/API and testing documentation with the confirmed completeness definition, metadata/provenance changes and environment limitations. Wiki updates are required before marking implementation complete.

Release diagnostic/UI changes independently of collection-policy changes. Disable or withdraw the explicit diagnostic entry point if allowance/admission behavior is unsafe. If the new production rule produces contradictory evidence, revert that rule to explicit incomplete rejection; retain prior successful snapshots/history and do not delete membership to manufacture recovery.

Authorized operational verification must confirm a coherent UI projection and, for Milestone B, the contract-backed complete result with consistent persisted membership/provenance. If IG cannot establish completeness, report the blocker rather than declaring successful collection.

## Deferred scope and completion checklist

Deferred pending P5: production sparse-page acceptance, candidate/visible schema changes, contract-specific retry behavior and any required migrations. Deferred outside this plan: alternate discovery endpoints, broad rate tuning, duplicate-name policy and unrelated detail-collection changes.

* [x] P0 baseline and applicable instructions confirmed.
* [x] P1 exact incident and complete-only regressions pass.
* [x] P2 bounded sanitized diagnostics pass isolation and termination tests.
* [x] P3 explicit invocation passes authorization/admission/budget tests.
* [x] P4 coherent UI refresh passes targeted tests.
* [x] P7A wiki, focused validation and Milestone A delivery complete.
* [ ] P5 authoritative completion decision recorded, or explicitly blocked.
* [ ] P6 selected production branch implemented and validated, if approved.
* [ ] P7B wiki and authorized contract-backed operational verification complete, if approved.

Plan creation is complete when this artifact exists with dependency order, implementation targets, evidence gates and acceptance criteria. Implementation completion requires the applicable checklist items and must not be inferred from this plan's existence.
