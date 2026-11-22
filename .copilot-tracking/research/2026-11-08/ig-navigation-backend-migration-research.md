---
description: 'Backend-owned migration guidelines from paginated IG categories to automated market-navigation discovery.'
ms.date: 2026-11-08
---

# Backend migration to IG market navigation

IG has not responded to the two paging enquiries reported by the operator.
Proceed with navigation investigation and an evidence-gated migration rather
than making another support response a prerequisite. The target is an
automated backend instrument-discovery service inside TNC.Trading.Platform,
not a browser through which users navigate the IG hierarchy.

This is migration research, not an implemented feature or an authorization to
make broker calls. Preserve the earlier
[sample discovery research](ig-sample-instrument-discovery-research.md) as
historical evidence; these guidelines supersede waiting for IG confirmation
as the next step. Navigation availability and actual response semantics still
require an explicitly authorized Demo investigation.

## Recommendation and boundaries

Implement a backend-owned, bounded, durable navigation crawl, initially in
nonpublishing shadow mode. Replace paginated discovery evidence with an
explicit navigation coverage contract before enabling publication. Reuse the
existing market-data full-run admission, fenced leases, protected credentials,
SQL-backed request limits, instrument persistence and batched detail stage
where their contracts permit it. Do not introduce a separate application or
import the deprecated WPF client.

The initial migration changes instrument discovery, not trading execution,
order handling, authentication policy or streaming architecture. A navigation
listing is neither a live price feed nor permission to trade. New instruments
must still pass existing detail, eligibility, freshness and trading controls.

Routine runs must succeed without a signed-in browser, selected tree node or
user action. Operators may configure high-level interests and rollout policy,
approve an investigation or inspect status. They must not fetch children,
assemble an EPIC list or acknowledge each branch to make a run progress.

## Evidence and controlling boundaries

| Current surface | Evidence | Migration consequence |
| --- | --- | --- |
| Sample navigation protocol | [Retained endpoint inventory](ig-sample-reference/api-reference.md#L33) | Use version-1 root and node requests as a candidate contract, not the WPF interaction model. |
| Collection port | [Existing gateway contract](../../../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/IMarketCategoryInstrumentsGateway.cs#L6) | Its complete-page semantics and category-code input are not a navigation node port. |
| Complete listing model | [Collection type](../../../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCollection.cs#L5), [paging metadata](../../../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCollectionMetadata.cs#L3) | Make coverage strategy explicit instead of fabricating a page number or provider total. |
| Scheduler and category bootstrap | [Coordinator dependencies](../../../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L17), [selection frozen at admission](../../../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L204) | Replace or route category refresh as well as listing discovery; retain admission and frozen business selection. |
| Publication validation | [Writer validation](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs#L296) | Validate typed navigation evidence while retaining profile, revision, lease, identity and transaction checks. |
| Full-run and stale-source behavior | [Current runtime](../../../docs/wiki/runtime-behavior.md#L117) | Preserve partial-run reporting and explicit stale sources; extend resume and source identity for navigation. |
| Detail retrieval | [Current batching and memberships](../../../docs/wiki/runtime-behavior.md#L150) | Reuse at-most-50-EPIC requests and cross-category target deduplication, not one REST request per discovered instrument. |

The local hypothesis is that the navigation endpoints can expose the required
account-visible EPICs without the contradictory category pagination metadata.
The cheapest falsifier is a bounded backend root request followed by a child
request under the applied Demo context. Unsupported endpoints, access denial
or an unusable response contract would block this migration path. Successful
responses establish availability, not exhaustive coverage.

## Backend ownership and flow

The worker invokes an Application-owned discovery use case under the existing
full-run admission. That use case owns traversal policy, business selection,
coverage decisions and publication readiness. An Infrastructure adapter owns
IG URLs, version headers, session handling, bounded JSON parsing and transport.
Application-owned ports expose simple node/market data and traversal storage;
no provider DTOs, HTTP clients or EF entities flow into policy.

Reuse existing feature folders and composition roots. Introduce only the
navigation transport and durable traversal boundaries needed for this
operation; do not build a generic crawler framework. The API and Blazor UI
remain thin command/status adapters and never own traversal.

The proposed stage sequence is:

1. Admit one run and freeze applied context, business interests, schedule,
   discovery strategy and mapping revision.
2. Establish a broker session, fetch navigation roots and stage the root
   catalogue for this run.
3. Resolve the selected business categories to roots using versioned policy.
4. Traverse all reachable nodes for those roots, recording responses,
   graph edges, market observations and bounded coverage evidence.
5. Derive category membership from the collected graph and validate coverage
   and publication change guards.
6. Publish approved listing snapshots with fenced source revisions. Record
   failures without replacing previous complete snapshots.
7. Feed accepted sources into the existing detail stage; report discovery,
   detail and aggregate full-run coverage independently.

Root discovery is automatic on every admitted refresh. Ordinary view/status
queries read saved SQL projections only. Closing the browser or restarting a
Web replica must have no effect on collection progress.

## Category bootstrap and automatic root resolution

Do not leave the existing category refresh as a mandatory paging-era bootstrap.
In navigation mode, derive the provider catalogue from navigation roots and
route the catalogue stage accordingly. Shadow mode stages this information
without altering the production category catalogue, interest selections or
listing pointers. Retain the category API only as a deliberately selected
legacy strategy or comparison source.

Keep platform category identity separate from opaque IG node IDs. Persist a
versioned mapping from a stable platform category code to one or more provider
roots, scoped to the applied broker environment/profile and account context.
Existing interests such as commodities should retain their business identity;
do not reinterpret the category code as an IG node ID.

The backend refreshes roots, resolves existing mappings by provider identity,
and handles all descendant navigation itself. Initial mapping policy may be
seeded from the authorized root inspection; this is configuration work, not
an operator navigation workflow. Avoid fuzzy display-name matching. If a
provider ID disappears or changes, mark that mapping unresolved and preserve
last-good data until a deterministic replacement rule or configuration
revision is available. Never silently move a selected category to a similar
name or select all new roots by default.

If one root maps to multiple business categories, or one category maps to
multiple roots, preserve that relationship explicitly. Provider-removed
identities become non-current rather than losing historical observations.
Unavailable selected roots remain failed requirements, not silently omitted
categories. Unselected new roots can be catalogued without crawling them.

## Traversal algorithm and data rules

Use an iterative queue or stack, not recursive UI handlers. Start with a
deterministically ordered selected root set. Within a run, fetch each unique
node once after a validated success; persist children and markets independently.
Do not copy the sample's `if nodes ... else markets` branch, which can ignore
markets when a non-null node list is present.

Before accepting responses, establish fixture-backed rules for null, absent
and empty arrays. Process both collections if the validated contract permits
mixed responses. An absent payload is not an empty category. A valid explicit
empty root may produce an empty listing, but an abrupt empty result must also
pass the publication change guard.

Persist graph edges separately from node-fetch status. A visited-node set
prevents duplicate requests; it does not establish that the graph is acyclic
or preserve membership on its own. Validate directed cycles and depth bounds,
and derive each selected root's reachable market set from the complete stored
graph. A shared subtree must contribute its EPICs to every applicable root,
even when it was fetched first for another category.

Use EPIC as market identity. Permit identical display names on different EPICs,
deduplicate repeated EPICs and retain all category/node memberships. Normalize
and validate identity fields at the adapter boundary. Reject ambiguous identity
or unresolved conflicting instrument attributes; do not arbitrarily select
the last response. Treat volatile price observations as time-stamped evidence,
not necessarily an identity conflict.

Do not discard an instrument because streaming is unavailable, the market is
closed or bid/offer is missing. Discovery membership and trading eligibility
are different decisions. Retain safe provider expiry labels as text; derive
typed expiry values separately where supported. Optional fields absent from
navigation must remain optional with explicit quality evidence, not fabricated
values to satisfy the paginated DTO. Validate size, numeric and storage bounds.

## Durability, scope and bounded execution

Extend the existing durable full-run model with node-level work and staged
observations rather than adding a second independent scheduler. Proposed
persistence responsibilities are:

| Record | Required evidence |
| --- | --- |
| Navigation run | Run ID, applied context, account-context reference/hash, trading day, frozen selection, strategy/version, mapping revision, root revision, fence and capture interval |
| Node attempt | Opaque node ID, pending/in-flight/succeeded/failed status, attempts, timestamps, safe failure and response digest |
| Graph observation | Parent/child edges and node-to-EPIC memberships scoped to the run |
| Market staging | Validated instrument identity, optional observations and source timestamps |
| Coverage result | Expected/discovered nodes, successful and unresolved nodes, unique EPIC count, bounds, change checks and policy version |
| Publication reference | Accepted strategy-specific listing source and immutable coverage evidence |

These are proposed responsibilities, not prescribed table or type names.
Reuse existing storage where it fits and keep retained broker data subject to
the platform's existing access and retention policies. Do not persist secrets,
session tokens or raw provider bodies in traversal evidence.

Checkpoint a validated response and its discovered work atomically. After a
crash, resume pending work and retry expired in-flight attempts under a new
fence. A response lost before checkpoint may require a repeated request; that
request must consume allowance again. Persisted successful nodes need not be
fetched again only while the same run remains within its configured capture
interval and source-context checks still hold.

Do not combine staged nodes across changed account context, endpoint profile,
mapping revision or incompatible root revisions. Supersede incompatible work
and start a fresh bounded run. Reauthenticate without persisting tokens.
Leases must renew through pacing waits and checkpoint work; stale replicas
cannot publish or continue after losing authority.

Use the existing schedule rules: no new admission outside the window, and
only the currently permitted fenced continuation of an admitted run. Add an
explicit maximum capture duration so restart/resume cannot keep a partially
observed tree alive indefinitely. Retained partial work is not slot success.

Configure maximum unique nodes, graph depth/edges, response bytes, selected
EPICs, capture duration and actual HTTP attempts. Do not reinterpret the
existing 100-page limit as a tested 100-node limit. Keep the existing bounded
15,000-EPIC detail universe until deliberately changed. Start the endpoint
probe with a small explicit ceiling, for example 10 actual HTTP attempts
including session requests; exhausting it means investigation incomplete,
not a publishable truncated listing. Calibrate production node bounds from
shadow evidence before expanding coverage.

Every send, retry and reauthentication passes through shared allowance and
account/application rate limits. Start sequentially; concurrency is not an
escape from provider quotas. Estimate detail cost from the distinct EPIC set
and reserve remaining capacity before hydration. Unknown tree size needs a
hard crawl ceiling and capacity checks at each expansion. Budget or duration
exhaustion must produce a safe incomplete outcome, never early publication.

## Coverage and safe publication

Navigation has no verified provider total. Define the new acceptance contract
as validated coverage of selected account-visible navigation subtrees observed
during a bounded interval, not every instrument IG offers globally or an
atomic provider snapshot. This is a deliberate semantic change that must be
represented in storage, status and downstream source validation.

Publication requires all selected roots for that listing to be resolved, all
reachable nodes successfully parsed, no pending or failed work, valid graph
structure and identities, complete memberships, no exceeded bounds, valid
context/revisions/fence and passed change guards. Record absent provider-total
evidence explicitly. Never fake a one-page result or set a provider total to
the observed EPIC count to satisfy current pagination validation.

Track stable structural digests of roots, edges and EPIC membership separately
from changing prices. A final bounded root check can detect some root changes,
but cannot prove every leaf remained unchanged. If roots or mappings change
incompatibly during capture, supersede and retry within policy. Retain this
consistency limitation rather than claiming a provider-wide point-in-time view.

Before replacing a last-good listing, apply configured change guards for
unexplained disappearance, empty results, missing required reference EPICs
and material count/membership changes. Thresholds must be explicit,
environment-scoped and calibrated from shadow runs; an arbitrary percentage
is not evidence. A quarantined change needs investigation or a versioned
policy decision, not a user walking the tree. Do not require a category API
snapshot for categories that have never successfully published.

Keep paginated and navigation evidence as typed alternatives under a shared
validated-listing contract. Update the Application model, writer validation,
EF mappings and SQL constraints additively; preserve historical paginated
records without manufacturing navigation evidence for them. Record strategy,
coverage-policy version and source revision in downstream detail fingerprints.

Publish a category only after all of its mapped roots pass. Preserve existing
per-category success/failure isolation where independent roots are healthy;
the aggregate full run is partial if any selected category fails. Publish
through the existing transaction and fence checks. Do not update pointers
node-by-node, retire instruments from partial traversal, or mark a shadow run
as covering a scheduled slot.

The detail stage may use an older accepted listing only under an explicit
same-context stale-source policy. A strategy change must not relabel legacy
data as fresh navigation data. Trading consumers must retain freshness and
eligibility gates; this migration must not make stale evidence trade-ready.

## Investigation, rollout and rollback

| Increment | Scope | Exit evidence |
| --- | --- | --- |
| Contract probe | Authorized backend session, root and child requests; no catalogue/listing publication | Endpoint works under Demo context; response shapes and safe failures captured; no browser dependency |
| Offline traversal | Fixture-backed Application traversal, identity and graph policy | Branching, shared nodes, cycles, empty/mixed responses and late failures behave deterministically |
| Durable shadow | Scheduled backend crawl for one bounded interest set, staged storage only | Restart/resume, fences, limits and capture duration hold; comparison evidence explains membership differences |
| Publication boundary | Typed coverage model, additive storage migrations, category bootstrap routing and detail source validation | Navigation evidence accepted without weakening legacy pagination checks; partial data never replaces last-good pointers |
| Controlled Demo cutover | Versioned strategy configuration for a bounded scope; automated listings and details | Repeated scheduled runs complete without UI, change guards pass, detail outcomes and source lineage remain distinct |
| Broader adoption | Expand validated root coverage; separately approve Live | Measured node/request costs fit limits; rollback and account-specific coverage tests pass |

Selection strategy should be explicit and frozen per run, for example legacy
pagination or navigation with a separate shadow setting. Do not silently fall
back to pagination on a navigation failure or run both production collectors
against the same pointers. Shadow comparisons consume real shared allowance;
they must not starve production or count as successful legacy collection.

For coverage calibration, use existing last-good snapshots where available,
known required EPICs, repeated bounded navigation observations and detail
validation. Explain additions/removals; count equality alone is insufficient.
IG support confirmation may strengthen confidence later but is not a mandatory
exit criterion. Neither keyword search nor watchlists are exhaustive reference
sets. If endpoint availability fails, stop this rollout rather than treating
support silence as permission to invent results.

Rollback stops new navigation admissions, safely cancels or supersedes active
work and preserves all history and pointed accepted snapshots. Re-enable the
legacy strategy only where it remains usable and validated; the current paging
blocker means rollback cannot promise restored refreshes. Otherwise operate
with explicit stale/blocked discovery status under existing trading safeguards.
Use additive, backward-compatible migrations for the rollout and test the
actual rollback build; do not assume an older binary understands navigation
source records. Avoid destructive down-migrations as a recovery mechanism.

## Required verification and observability

* Unit tests cover automatic root resolution, immutable selection/mapping,
  shared subtrees across categories, duplicate names and EPICs, invalid fields,
  empty/mixed responses, cycles, depth bounds and deterministic membership.
* Transport fixtures cover endpoint versions, path escaping, bounded bodies,
  session replay, transient failures and known versus unknown access/allowance
  denials. Every outbound attempt is budgeted and paced.
* SQL-backed tests cover atomic checkpoints, crash recovery, competing replica
  fences, context changes, expiry of staged work, per-category publication,
  preservation of legacy evidence and no partial pointer replacement.
* Source/detail tests cover cross-category deduplication, at-most-50-EPIC
  batching, the 15,000-target bound, stale sources and strategy-specific
  fingerprints. Non-streaming instruments remain in discovery.
* Closed-box checks run with no browser session, restart the API mid-crawl,
  and verify automatic completion or a bounded safe failure. Broker-backed
  checks require explicit authorization; normal CI uses controlled fixtures.
* Rollout tests prove shadow mode cannot mutate production category/listing
  pointers or scheduled coverage, and rollback does not bypass freshness or
  publish a legacy fallback as navigation success.

Persist sanitized run/stage status, root/node counts, successful/pending/failed
work counts, unique EPICs, membership changes, actual requests, wait time,
elapsed capture time, coverage-policy version and last accepted source time.
Use bounded safe failure codes distinguishing unavailable endpoint, unresolved
root, invalid response, graph failure, exhausted capacity, changed context,
quarantined change and publication failure. Logs must not include credentials,
tokens, raw bodies or node/EPIC-bearing request URLs. Avoid node IDs or EPICs
as high-cardinality metric labels. Restricted identities belong in authorized
storage, not public diagnostics.

Update affected implementation wiki pages when behavior is implemented,
including source semantics, schedule/resume behavior, operator status, storage
and test approach. Do not rewrite the current wiki as though this proposed
migration has already shipped.

## Remaining decisions

The first implementation increment must settle the available root/node
contract for the applied Demo account and the initial category-to-root mapping.
Shadow evidence must then establish practical node/capture bounds, change
guards and the account-visible coverage policy. Live availability and account
membership require separate verification; Demo success is not Live proof.

The confirmed publication-frequency mismatch and gateway display-name/expiry
restrictions described in the earlier research remain adjacent defects to
address with focused tests. They are not reasons to weaken navigation coverage
or evidence that navigation has already resolved the paging blocker.

Completion means a backend run can discover, validate, publish and hydrate
the configured navigation universe without user traversal, within shared
resource limits, with honest coverage semantics and recoverable failure.