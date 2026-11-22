---
description: 'Evidence-gated delivery plan for backend-owned IG navigation discovery, durable shadow collection and controlled publication.'
ms.date: 2026-11-08
---

# Implementation plan: IG navigation backend migration

## Status and authority

Status: in progress; offline Phase 1 transport and internal probe preparation
are implemented. No phase exit gate or broker verification is complete.
See the [execution log](ig-navigation-backend-migration-execution-log.md).

Source: [backend migration research](../../research/2026-11-08/ig-navigation-backend-migration-research.md).
Keep the [earlier sample research](../../research/2026-11-08/ig-sample-instrument-discovery-research.md)
as historical evidence. The newer research supersedes waiting for IG support
as the next step, not the requirement to validate endpoint semantics.

This plan does not authorize broker requests, database maintenance, Demo
cutover or Live enablement. Obtain explicit authorization for each
broker-backed investigation or rollout scope. Ordinary CI uses controlled
fixtures. Do not start collection workers against IG during offline validation.
Do not create a new work package or modify the IG sample repository.

## Outcome and scope

Deliver an automated backend run that discovers, validates, publishes and
hydrates the configured account-visible navigation universe without a
browser session, selected tree node or operator traversal. Preserve shared
limits, recoverable failures, last-good data and trading safeguards.

Reuse the existing full-run scheduler, admission, fenced leases, protected
credentials, SQL-backed allowance, instrument persistence and batched detail
stage where their contracts fit. Application owns traversal and acceptance;
Infrastructure owns IG transport and durable storage. API and Blazor remain
thin command/status adapters reading saved projections.

Exclude trading execution, orders, authentication-policy changes, streaming
redesign, a separate application and a generic crawler framework. Navigation
membership is not trading eligibility or a live price feed. Track the earlier
publication-frequency mismatch and gateway name/expiry restrictions as
adjacent defects; fix only confirmed blockers with focused regression tests.

## Evidence and local hypothesis

* [Coordinator dependencies](../../../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L17)
  include category refresh, so replacing the listing gateway alone leaves a
  paging-era bootstrap dependency.
* [Admission selection](../../../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L204)
  freezes interests; extend that boundary to strategy, mappings and context.
* [Collection validation](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs#L296)
  requires paging metadata and category snapshot provenance. Navigation needs
  typed alternative evidence, not fabricated totals or weakened legacy checks.
* [Retained protocol inventory](../../research/2026-11-08/ig-sample-reference/api-reference.md#L33)
  identifies candidate version-1 root/node requests; it does not prove current
  account access, exhaustive coverage or response semantics.

Hypothesis: navigation can supply the required account-visible EPICs through
bounded subtree traversal under existing full-run ownership. The cheapest
provider falsifier is an authorized backend Demo root request followed by a
child request. Access denial, unsupported endpoints or an unusable response
contract stop this path. Success proves availability only. Offline graph tests
and durable shadow evidence must establish the later acceptance contract.

Reconcile these source anchors and the current schema before product edits.
Load applicable architecture, configuration, C#, auth, test, Aspire, API and
Radzen instructions for each touched slice. Keep existing framework targets
and composition conventions; use [Aspire documentation](https://aspire.dev/)
for lifecycle operations. Do not assume proposed record/type names already exist.

## Sequencing and decisions

Execute phases in order. Offline preparation can proceed without broker
authorization, but unsupported provider semantics cannot be declared verified
by fixtures. Publication remains disabled until phases 1-5 pass. Demo cutover
and broader/Live adoption each require their own decision and authorization.

Before the probe, record the applied Demo environment/profile, protected
account-context reference, selected investigation scope, request ceiling,
body/time bounds and cancellation authority. Start with at most 10 actual HTTP
attempts including session requests, retries and reauthentication. Exhaustion
means incomplete investigation, not a truncated publishable result.

During shadow calibration, settle explicit environment-scoped node, depth,
edge, response-byte, EPIC, capture-duration and HTTP-attempt ceilings, change
guards and coverage-policy version. Do not convert the 100-page limit into a
100-node assumption. Retain the 15,000-distinct-EPIC detail universe until a
separate deliberate change. No arbitrary disappearance percentage is a gate.

## Implementation checklist

### Phase 1: Bounded contract probe

* [x] Add a minimal Application-owned root/node transport port and an IG
  adapter using version-1 requests, escaped opaque node IDs, bounded parsing
  and existing session/allowance handling. Keep provider DTOs internal.
* [x] Add an explicitly gated backend investigation entry point with no
  catalogue/listing publication and no routine UI navigation dependency.
* [x] Verify each send, retry and reauthentication is budgeted and paced;
  distinguish unsupported endpoints, known access/allowance denials, unknown
  denial, transient failure and malformed/oversized payloads safely.
* [ ] With separate authorization, execute the bounded Demo root/child probe.
  Retain sanitized shape evidence and safe fixture representations, not raw
  provider bodies, tokens, credentials or identity-bearing request URLs.
* [ ] Decide null/absent/empty/mixed collection semantics from evidence;
  record initial deterministic business-category-to-root mappings and any
  unresolved contract questions. Never infer emptiness from absent payloads.

Exit: endpoint availability and supported response rules documented for the
applied Demo account, with actual attempt counts. Otherwise stop provider
rollout and retain blocked/stale discovery. IG support response is not required.

The authorized 2026-11-14 root attempt returned `EndpointUnsupported` after
two HTTP sends. No root shape was returned, so no child request, response
semantics decision, or category mapping is supported by this evidence. Keep
the remaining Phase 1 evidence gates open and do not advance provider rollout.

### Phase 2: Offline traversal and mapping policy

* [ ] Implement an Application-owned iterative traversal with deterministic
  selected roots, independent children/markets processing and one fetch per
  unique successfully validated node within a run.
* [ ] Keep platform category codes separate from provider node IDs. Version
  many-to-many mappings by environment/profile/account context; resolve by
  provider identity, never fuzzy names or automatic selection of new roots.
* [ ] Store edges independently of fetch state; detect directed cycles and
  bounds, then derive every category's reachable EPIC memberships from the
  full graph. Shared subtrees must contribute to every applicable category.
* [ ] Validate normalized EPIC identity, numeric/storage bounds and conflicting
  attributes; allow duplicate names, deduplicate EPICs and retain memberships.
  Separate time-stamped volatile observations from stable identity evidence.
* [ ] Retain closed/non-streaming/missing-price instruments, safe textual
  expiry labels and genuinely optional fields with explicit quality evidence.
* [ ] Test branching, diamonds/shared roots, duplicate EPICs/names, invalid
  fields, mixed/empty/absent responses, cycles, depth/edge limits, deterministic
  order and late failures. Use documented repository-style test names.

Exit: fixture-backed policy computes deterministic complete memberships or a
bounded incomplete result. Missing selected roots remain failed requirements;
no operator child-fetch action is required.

### Phase 3: Durable nonpublishing shadow

* [ ] Extend existing full-run admission/storage with frozen strategy,
  mapping/root revisions, applied/account context, fence and capture interval.
  Do not add an independent scheduler or combine incompatible staged runs.
* [ ] Persist node attempts/status/digests, graph observations, validated
  markets and coverage evidence. Atomically checkpoint each accepted response
  together with discovered work. Persist no broker secrets or raw bodies.
* [ ] Recover pending and expired in-flight work under a new fence; reuse
  successful checkpoints only within the same compatible, unexpired run.
  Repeated requests after lost checkpoints consume allowance again.
* [ ] Enforce sequential shared pacing, expansion-time capacity checks and
  all configured ceilings. Renew leases through requests, waits and commits;
  cancel stale replicas immediately on authority loss.
* [ ] Preserve schedule admission/continuation rules; supersede changed context,
  incompatible root/mapping revisions or expired capture. Partial retained
  work never counts as slot success.
* [ ] Route shadow catalogue discovery from fresh navigation roots each run;
  stage observations without changing production catalogue, interests,
  listing pointers or scheduled coverage. Bound comparison costs so they
  cannot starve production.
* [ ] Test SQL atomicity, crash points, competing replicas, context changes,
  time expiry, allowance exhaustion and immutable production pointers.
  After authorization, gather bounded shadow evidence for one interest set.

Exit: restart/resume and fences hold; shadow cannot publish. Measured request,
node and capture costs support explicit production bounds and change guards.
Unexplained membership differences remain unresolved, not accepted by count equality.

### Phase 4: Typed coverage and publication boundary

* [ ] Introduce paginated and navigation alternatives under a shared validated
  listing contract. Record strategy/policy/source revision and explicit absence
  of provider-total evidence. Keep legacy pagination validation unchanged.
* [ ] Add compatible EF mappings, SQL constraints and migrations, preserving
  historical paginated evidence without backfilling invented navigation data.
* [ ] Route the production catalogue/bootstrap stage by frozen strategy.
  Navigation derives roots automatically and applies versioned mappings;
  legacy category refresh is an explicit legacy/comparison path only.
* [ ] Require every mapped root for a category to resolve, all reachable work
  to succeed, valid graph/identities/memberships, no exceeded bounds and valid
  context/revisions/fence. Record stable root/edge/EPIC structural digests
  separately from price changes.
* [ ] Add a budgeted final root check; supersede incompatible changes. State
  that this detects some drift, not leaf stability or an atomic IG snapshot.
* [ ] Apply calibrated guards for empty results, unexplained disappearance,
  missing required reference EPICs and material membership/count changes.
  Quarantine failures pending investigation or versioned policy decisions.
* [ ] Publish accepted categories transactionally through existing fence
  checks; isolate independent category failures and report aggregate partial
  runs. Never replace pointers or retire instruments from partial traversal.
  Do not require a successful category API snapshot for first navigation publication.
* [ ] Test legacy evidence preservation, strategy-specific rejection,
  first publication, explicit empty versus abrupt empty, guard quarantine,
  shared-root failures, healthy independent categories and stale-writer rejection.

Exit: navigation publication has honest selected-subtree interval coverage,
not global/provider-total completeness. Failed or shadow work cannot replace
last-good pointers or claim scheduled-slot coverage.

### Phase 5: Detail lineage, status and closed-box verification

* [ ] Feed accepted sources to existing cross-category deduplicated detail
  retrieval, at most 50 EPICs per request and 15,000 distinct targets. Estimate
  cost and reserve remaining capacity before hydration.
* [ ] Include strategy, coverage-policy version and immutable source revision
  in detail fingerprints. Permit older accepted sources only under explicit
  same-context stale-source policy; legacy evidence never becomes fresh navigation.
* [ ] Expose separate discovery, detail and aggregate coverage through saved
  SQL status projections. Preserve downstream freshness and eligibility gates.
* [ ] Persist sanitized stage status, root/node/work/EPIC counts, membership
  changes, actual requests, waits, capture elapsed time, policy version and
  last accepted source time. Use bounded safe failure codes; no node/EPIC
  metric labels, raw bodies or sensitive URLs in diagnostics.
* [ ] Test batching boundaries, deduplication, target/capacity exhaustion,
  stale and changed-context sources, fingerprint distinctions, non-streaming
  membership and trading rejection of stale/ineligible evidence.
* [ ] Run fixture-backed closed-box tests without a signed-in browser; restart
  the API mid-crawl and verify bounded completion/failure. Confirm closing a
  browser or restarting a Web replica cannot control traversal progress.

Exit: offline end-to-end checks prove automated discovery-to-detail behavior,
safe observability and unchanged trading safeguards. Complete documentation
updates for implemented phases before considering them delivered.

### Phase 6: Controlled Demo cutover

* [ ] Obtain authorization for a bounded Demo selection, calibrated limits,
  change guards, strategy/mapping/policy revisions and rollout observation period.
* [ ] Enable explicit navigation strategy for that scope. Freeze settings per
  run; do not silently fall back or run competing production collectors against
  the same pointers. Disable publication immediately if evidence gates fail.
* [ ] Verify repeated scheduled runs complete without UI action; compare
  accepted memberships with available last-good data, known required EPICs,
  repeated observations and detail validation. Explain additions/removals.
* [ ] Verify actual allowance costs, partial-category isolation, guard behavior,
  lineage, freshness and recovery. Record the measured acceptance results.

Exit: approved Demo scope repeatedly publishes and hydrates within limits,
with distinct lineage and explainable coverage. Keyword search, watchlists,
count equality or Demo success alone are not exhaustive reference proof.

### Phase 7: Rollback rehearsal and broader adoption

* [ ] Rehearse stopping new navigation admissions and cancelling/superseding
  active work while retaining history and accepted snapshot pointers.
* [ ] Test the actual rollback build against additive schema and navigation
  records; do not assume older binaries understand the new evidence. Avoid
  destructive down-migrations and preserve audit/allowance history.
* [ ] Re-enable legacy only where usable and validated. Otherwise expose
  stale/blocked discovery; rollback cannot promise restored paging refreshes.
* [ ] Prove rollback cannot bypass freshness or label fallback as navigation
  success. Retain no-partial-publication and fence tests during rollback.
* [ ] Expand Demo roots only after measured bounds/coverage tests pass for
  the added scope. Obtain separate Live authorization and account-specific
  endpoint/mapping/membership verification before any Live rollout.

Exit: rollback is demonstrated without data loss or relaxed safeguards;
each expanded environment/account scope has its own evidence and approval.

## Validation and evidence records

Run the cheapest touched-slice test after each substantive edit, then broaden
only for affected shared contracts. Reuse existing test projects and repository
validation tooling; record the resolved commands and results at implementation
time rather than inventing project names here. Normal CI must remain offline.

For each phase, record changed files, focused check results, unresolved failures,
sanitized provider evidence where authorized, configured limits/revisions and
the gate decision in a sibling dated execution log when implementation starts.
Do not mark a phase complete solely because its code compiles. Migration and
rollback phases require SQL-backed compatibility and publication tests.

## Wiki impact

Update pages only when behavior is implemented; do not present this proposal
as shipped. Each phase needs the following change/no-change decision confirmed
in its execution evidence before completion.

* Phase 1: change [testing and quality](../../../docs/wiki/testing-and-quality.md)
  for investigation safety; change [operator guide](../../../docs/wiki/operator-guide.md)
  only if an investigation command is exposed. No production-runtime change.
* Phase 2: change [architecture](../../../docs/wiki/architecture.md) for policy
  ownership and mappings, and testing guidance for graph/identity semantics.
  No production pointer or operator-workflow change.
* Phase 3: change [runtime behavior](../../../docs/wiki/runtime-behavior.md),
  architecture and operator guidance for shadow/resume/fences/bounds. Address
  staged-data retention, restricted identity access and audit preservation in
  architecture/operator guidance; no production listing-semantic change yet.
* Phase 4: change runtime behavior, architecture and
  [application overview](../../../docs/wiki/application-overview.md) for typed
  coverage, catalogue routing, storage compatibility and accepted publication.
* Phase 5: change runtime behavior, testing guidance, operator guidance and
  [API reference](../../../docs/wiki/api-reference.md) for status/source lineage;
  change [local development](../../../docs/wiki/local-development.md) for
  repeatable fixture-backed restart checks. No auth/streaming-policy change.
* Phase 6: change operator/runtime guidance for approved Demo settings,
  calibrated bounds, guards and rollout outcomes. No claim of Live support.
* Phase 7: change operator/local-development/testing guidance for demonstrated
  rollback, schema compatibility and retention/audit preservation. Update
  environment/account coverage only after separately verified adoption.

## Completion criteria

All applicable checkboxes, evidence gates and wiki decisions are complete for
the approved rollout scope. A scheduled backend run automatically resolves,
discovers, validates, publishes and hydrates that scope within shared limits.
Incomplete/quarantined work preserves last-good data; source semantics,
staleness, partial outcomes and rollback limitations remain explicit. Broader
or Live adoption can remain deferred only with its pending scope clearly recorded.