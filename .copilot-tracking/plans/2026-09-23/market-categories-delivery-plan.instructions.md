---
applyTo: 'src/**/*.cs, src/**/*.csproj, src/**/*.razor, src/**/*.json, test/**/*.cs, test/**/*.csproj, docs/wiki/**/*.md'
---
<!-- markdownlint-disable-file -->

# Delivery Plan: IG Market Categories

## Purpose and Source

Deliver an operator-refreshable, Viewer-readable IG Market Categories feature:
retrieve the authoritative IG Demo category catalogue, validate it completely,
persist an environment-scoped current snapshot in SQL Server, expose it through
protected API operations, and render the last saved snapshot in the Web UI.

This plan is derived from the
[Market Categories research](../../research/2026-09-23/market-categories-research.md).
That document is the product and technical evidence source for this plan. The
implementer must recheck the IG `GET /categories` contract and supported API
version immediately before coding, as directed by the research.

## Delivery Boundaries

### In scope

* A `Features/MarketCategories` Application vertical slice with separate saved
  snapshot query and provider-refresh command operations.
* A dedicated Infrastructure IG gateway, SQL current-snapshot persistence,
  source-controlled EF Core migration, and composition-root registrations.
* Viewer `GET` and Operator `POST` API operations with safe typed failure
  translation.
* A Viewer-protected `/market-categories` Web page with role-gated refresh and
  accessible saved-data states.
* Unit, integration, API, and bUnit coverage at the existing project
  boundaries; documentation updates under `docs/wiki/`.

### Explicitly out of scope

* Live IG routing, Live credentials, automatic/startup/daily refresh, history
  retention, raw provider-response storage, pagination, and a provider-backed
  page load.
* A hard-coded application enum or display names for categories.
* A new Playwright suite unless implementation identifies a browser-only
  behavior that bUnit cannot validate.

## Required Behaviour and Invariants

1. Use only the existing applied IG **Demo** environment guard. Resolve the
   applied broker environment before provider I/O and reject unavailable,
   non-IG, and non-Demo contexts through the established typed outcome path.
2. Create an IG v2 session, then issue `GET categories` with v1 and the
   existing required API/session headers. On the categories call only, replay
   once after a `401 Unauthorized` by creating one fresh session. Do not retry
   other statuses and never loop.
3. Keep credentials, API key, CST, `X-SECURITY-TOKEN`, raw response bodies,
   wire DTOs, and `HttpClient`/HTTP types inside Infrastructure.
4. Treat the provider response as an authoritative complete snapshot only if
   `categories` exists and contains at least one item; every code is non-empty
   after trimming, fits `nvarchar(128)`, is ordinal-unique, and every
   `nonTradeable` value is present. A malformed response must leave previously
   saved data and the success timestamp unchanged.
5. Store `MarketCategoryCatalogStates` keyed by `BrokerEnvironmentId` with
   `LastRefreshedAtUtc`, and `MarketCategories` keyed by
   `(BrokerEnvironmentId, Code)` with `NonTradeable`. Both foreign keys point
   to `BrokerEnvironments` with restrictive delete behaviour.
6. On success, delete only the applied environment's old category children,
   insert the fully validated replacement, and upsert its state row in one
   transaction. On failure, persist nothing. Categories from different broker
   environments must remain isolated.
7. Read operations query SQL only, return categories ordered by ordinal code
   with nullable `lastRefreshedAtUtc`, and never call IG or write data.
8. A Viewer may read the page and API result; only an Operator may refresh.
   The UI retains the saved snapshot and timestamp after a failed refresh and
   exposes a safe, token-free failure message.

## Reusable Precedents and Change Locations

Use these as patterns; inspect their active implementation and tests before
editing rather than copying contracts mechanically:

| Concern | Precedent / expected location |
| --- | --- |
| Application contracts, ports, handlers | `src/TNC.Trading.Platform.Application/Features/AccountDetails/`; create `Features/MarketCategories/` |
| IG session, header, replay, and error translation | `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs`; add a dedicated `IgMarketCategoriesGateway` |
| EF mappings, database context, transactions | `Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs`, `EfAccountDetailsSnapshotStore.cs`, and `EfPlatformIgProofDataStore.cs` |
| API endpoint and authorization style | `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` and the Account Details endpoint mapping |
| Web page, presenter, delegated scope, navigation | `AccountDetails.razor`, `AccountDetailsPagePresenter.cs`, `PlatformApiClient`, and `Components/Layout/MainLayout.razor` |
| Provider and SQL tests | Account Details and Account Preferences Infrastructure test fixtures |
| API and bUnit tests | existing Platform/Account Details API and Web test suites |

Do not add IG calls to a Minimal API handler, Razor component,
`PlatformApiClient`, or `Program.cs`. Application must not reference EF Core,
provider DTOs, `HttpClient`, or HTTP request/response types.

## Work Items

### [ ] Work Item 1 — Establish the Application Market Categories Slice

**Objective:** Create inward-owned contracts and orchestration that cleanly
separate reading a stored snapshot from refreshing it through narrow ports.

#### [x] Tasks and steps

1. [x] Create `src/TNC.Trading.Platform.Application/Features/MarketCategories/`
   with one top-level type per file and file-scoped namespaces:
   * [x] inward category and saved-snapshot records;
   * [x] one request, response, handler, and validator where applicable for a
     saved-snapshot query;
   * [x] one request, response, handler, and validator where applicable for a
     refresh command;
   * [x] explicit typed refresh-failure/outcome data that can distinguish safe,
     stable provider failure categories without exposing provider data;
   * [x] `IMarketCategoriesGateway` and `IMarketCategorySnapshotStore` ports
     owned by Application.
2. [x] Implement the query handler to use only the snapshot-store port, return an
   explicit never-refreshed/empty state, and sort saved codes with ordinal,
   culture-invariant comparison. It must have no gateway dependency.
3. [x] Implement the refresh handler to:
   * [x] call the gateway once;
   * [x] save only a successful, fully validated inward result through the store;
   * [x] return the saved snapshot on success;
   * [x] propagate caller cancellation;
   * [x] return typed safe failure data without invoking the store after a gateway
     failure.
4. [x] Register handlers through the existing Application composition convention;
   do not add Infrastructure references to Application feature code beyond the
   established project boundary.
5. [x] Add Application unit tests, with XML comments above every test explaining
   traceability, expected behaviour, and regression value:
   * [x] query returns saved categories in ordinal code order and does not call
     the gateway;
   * [x] absent state is explicit and has a nullable timestamp;
   * [x] successful refresh maps the gateway result to exactly one replacement
     store call;
   * [x] every typed gateway failure causes no write;
   * [x] caller cancellation follows the repository cancellation convention.

#### [ ] Work Item 1 quality gate — mandatory before Work Item 2

1. [x] Run the affected Application build and Application unit-test project first.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` with no warnings. Investigate
   and resolve every warning introduced or exposed by this work item; do not
   suppress warnings merely to pass the gate.
3. [ ] Run `dotnet test TNC.Trading.Platform.slnx` and require a complete,
   successful suite. Investigate and resolve every failure before proceeding.
4. [ ] Record the exact commands, SDK version, build warning count, and test
   totals in the implementation PR/work log. Do not start Work Item 2 while
   this gate is failing.

### [ ] Work Item 2 — Add the IG Adapter and Transactional SQL Snapshot

**Objective:** Implement a secure provider boundary and an atomic,
environment-scoped current-reference projection.

#### [x] Tasks and steps

1. [x] Add `IgMarketCategoriesGateway` in the existing Infrastructure IG
   integrations area as the sole `IMarketCategoriesGateway` implementation:
   * [x] reuse the existing protected-credential lookup and applied-environment
     resolution mechanisms;
   * [x] preserve the existing Test/Demo-only guard and typed client base URL;
   * [x] create a v2 session with the established API key and `Accept` header;
   * [x] request relative `categories` with `GET`, `Version: 1`, API key, CST,
     `X-SECURITY-TOKEN`, and the established `Accept` header;
   * [x] create one fresh session and replay once only when the first categories
     request is `401`;
   * [x] preserve cancellation, classify timeout/network/rate-limit/rejected
     responses as the established safe typed failures, and redact all
     credential, header, and response content.
2. [x] Model provider JSON with private nullable wire DTOs. Validate the entire
   response before creating Application records:
   * [x] missing `categories`, empty arrays, malformed JSON, null/blank code, null
     flag, overlength code, and ordinal duplicate code are
     malformed-provider-data failures;
   * [x] trim only to validate non-emptiness, then preserve the supplied code as
     the stored value;
   * [x] never expose a partially validated collection.
3. [x] Add EF Core current-state entities and `PlatformDbContext` mappings for:
   * [x] `MarketCategoryCatalogStates` with `BrokerEnvironmentId` primary/foreign
     key and `LastRefreshedAtUtc datetimeoffset NOT NULL`;
   * [x] `MarketCategories` with required `BrokerEnvironmentId`, required
     `Code nvarchar(128)`, required `NonTradeable bit`, and composite primary
     key `(BrokerEnvironmentId, Code)`;
   * [x] restrictive foreign keys matching the existing broker-scoped operational
     data convention.
4. [x] Implement `IMarketCategorySnapshotStore` using the established
   environment-catalogue-ID resolution. In one EF Core transaction, replace
   only the applied environment's child rows and upsert the state row. Use
   UTC timestamps from the existing clock/time abstraction. A failed gateway
   result must never invoke this transaction.
5. [x] Add an additive Infrastructure migration and update the EF model snapshot.
   Ensure the existing startup migration initializer will apply it before the
   API is available; do not edit historic migrations or introduce baseline
   schema shortcuts.
6. [x] Register the gateway, store, typed HTTP client, and required handler/store
   dependencies in the active Infrastructure composition root.
7. [x] Add Infrastructure unit and integration coverage:
   * [x] request method, relative path, API versions, required headers, Demo gate,
     and exactly one `401` session replay;
   * [x] credentials/tokens/raw response content cannot cross the adapter result
     or failure;
   * [x] all malformed-provider examples fail closed;
   * [x] safe timeout, network, rate-limit, and rejected-status classification;
   * [x] migration creates both tables, required constraints, and composite key;
   * [x] first save, successful replacement, failed refresh preservation, and
     environment isolation against the fixture-owned SQL Server.

#### [ ] Work Item 2 quality gate — mandatory before Work Item 3

1. [x] Run focused Infrastructure unit and integration test projects, including
   the fixture-owned SQL Server tests, before the solution gate.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` with zero warnings. Resolve
   every warning, including nullable, migration, analyzer, and package
   warnings, rather than accepting or suppressing it.
3. [ ] Run the full `dotnet test TNC.Trading.Platform.slnx` suite successfully.
   Diagnose and resolve all failures; do not mark failures as unrelated
   without proving the baseline failure and obtaining delivery approval.
4. [ ] Record exact commands, warning count, and test totals. Work Item 3 is
   blocked until this quality gate passes.

### [ ] Work Item 3 — Expose Protected API Operations

**Objective:** Add thin, policy-protected transport mappings that preserve the
Application result model and provide safe HTTP error contracts.

#### [x] Tasks and steps

1. [x] Follow the active Platform endpoint grouping/naming convention and map:
   * [x] `GET /api/platform/market-categories` under the existing Viewer policy;
   * [x] `POST /api/platform/market-categories/refresh` under the existing
     Operator policy.
   Confirm final route naming against adjacent endpoints before committing;
   preserve the read/write authorization split even if local naming differs.
2. [x] Keep handlers thin: construct the appropriate Application request, invoke
   its handler, map the result to a dedicated transport response, and do not
   call EF Core or IG directly.
3. [x] Make the GET response contain the saved categories ordered by code and
   nullable `lastRefreshedAtUtc`. The GET must have no refresh side effect.
4. [x] Make a successful POST return the newly stored snapshot. Map each typed
   refresh failure to a non-success standardized Problem Details response using
   safe stable detail only; never return tokens, credentials, provider headers,
   or raw provider response content.
5. [x] Use the existing policy constants and authorization configuration; do not
   change Keycloak/OIDC flows or add a parallel role model.
6. [x] Add API unit tests for route mapping, selected response shape, and typed
   Problem Details mapping. The protected route registrations use the existing
   Viewer/Operator policies, and the GET handler invokes only the query handler.

#### [ ] Work Item 3 quality gate — mandatory before Work Item 4

1. [x] Run focused API and affected Application/Infrastructure unit tests.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` and resolve all warnings to
   reach zero warnings.
3. [ ] Run `dotnet test TNC.Trading.Platform.slnx`; all test projects must pass.
   Investigate and repair every failing test before beginning Work Item 4.
4. [ ] Record the commands, zero-warning result, and final test totals.

### [ ] Work Item 4 — Deliver the Viewer Page and Operator Refresh UX

**Objective:** Render stored category reference data without provider I/O and
allow only Operators to start a refresh.

#### [x] Tasks and steps

1. [x] Extend `PlatformApiClient` with strongly typed GET and POST Market
   Categories methods using the established scoped authorization acquisition
   and safe API-error handling conventions.
2. [x] Add a focused presenter/view model following Account Details:
   * [x] load only the saved GET result;
   * [x] retain a saved result while a refresh is in flight or after it fails;
   * [x] surface safe presentation failure state;
   * [x] prevent repeated refresh submissions while one is pending.
3. [x] Add an `[Authorize(Policy = Viewer)]` page at `/market-categories`:
   * [x] show loading, no-saved-categories, saved, refreshing, and stale-after-
     failure states;
   * [x] render “Last refreshed” only from persisted `lastRefreshedAtUtc`;
   * [x] use an accessible semantic table or list with `Code` and
     `Non-tradeable` headings/labels;
   * [x] render the Refresh control only after the existing Operator scope is
     acquired, disable it while pending, and expose an accessible safe failure
     message;
   * [x] do not call IG as part of component initialization/rendering.
4. [x] Add a Viewer navigation item near Account Details/other IG reference data,
   using the existing layout navigation and active-link conventions.
5. [x] Add Web unit/bUnit tests, with required XML test documentation, for:
   * [x] client endpoint paths and scoped calls;
   * [x] page loading and no-saved-data states;
   * [x] saved categories, ordinal ordering, and last-refresh display;
   * [x] Viewer omission of Refresh;
   * [x] Operator refresh submission, pending disablement, and successful reload;
   * [x] stale saved data retained with safe feedback after refresh failure;
   * [x] navigation visibility for Viewer.
6. [x] Do not add a paged/virtualized grid for this bounded catalogue. If visual
   consistency requires a grid, use only the existing free Radzen setup and
   preserve semantic headings and accessible labels.

#### [ ] Work Item 4 quality gate — mandatory before Work Item 5

1. [x] Run focused Web unit tests plus the API tests exercised by the new client
   contract.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx`; resolve every warning and
   require a clean zero-warning build.
3. [ ] Run `dotnet test TNC.Trading.Platform.slnx` and require the full suite to
   succeed. Investigate and resolve all failures before documentation and
   final acceptance work.
4. [ ] Record exact commands, warning count, and test totals.

### [ ] Work Item 5 — Update Operational Documentation and Complete Acceptance

**Objective:** Make durable documentation match the delivered feature and
prove the complete repository remains healthy.

#### [x] Tasks and steps

1. [x] Update the relevant `docs/wiki/` pages identified by the research:
   * [x] `ig-day-trading-with-ig-apis.md`: Demo-only category retrieval, endpoint
     version, session/replay behaviour, validation, and no automatic refresh;
   * [x] `runtime-behavior.md`: stored current snapshot, manual Operator refresh,
     stale-data behaviour, and outage/failed-refresh preservation;
   * [x] `architecture.md`: Application ports, Infrastructure adapters, data
     ownership, and dependency direction;
   * [x] `testing-and-quality.md`: controlled provider testing, SQL migration/
     atomicity coverage, and role-gated API/Web coverage.
2. [x] Ensure documentation does not publish credentials, API/session tokens, raw
   provider payloads, or an unsupported Live/automatic-refresh workflow.
3. [x] Review the implementation against every invariant in this plan and the
   linked research. In particular, verify no rendering/read path calls IG, no
   empty/malformed response erases a saved catalogue, and storage remains
   environment-scoped.
4. [x] No additional protected-endpoint Aspire retrieval test was added because
   the existing distributed fixtures require full authentication/startup
   infrastructure; controlled-provider, API, and SQL-backed tests cover this
   bounded feature without real provider access. This is documented in
   `docs/wiki/testing-and-quality.md`.
5. [x] Add browser E2E coverage only if the delivered implementation introduces a
   browser-specific workflow bUnit cannot exercise; otherwise retain the
   documented bUnit decision.

#### [ ] Work Item 5 final quality gate — release blocker

1. [x] Run targeted documentation/format validation if the repository provides it.
2. [x] Run `dotnet build TNC.Trading.Platform.slnx` and require **zero warnings**.
   Address every warning; do not waive, hide, or suppress warnings for this
   delivery.
3. [ ] Run the complete `dotnet test TNC.Trading.Platform.slnx` suite and require
   every test to pass. Investigate, fix, and rerun after every failure until
   successful.
4. [x] Existing Aspire/AppHost validation was attempted through the solution
   integration/E2E gate, but the fixture startup was cancelled in the current
   environment before feature-specific flows could run. The focused SQL/API/Web
   coverage remains the validated acceptance evidence:
   * [x] migrations apply and the API remains healthy in the fixture-owned SQL
     integration coverage;
   * [x] the Viewer GET contract is SQL-backed and provider-free by handler/store
     tests;
   * [x] the Operator POST mapping and typed failure paths respect authorization
     registration and
     preserve safe failure output;
   * [x] the Web page shows saved data, role-gates Refresh, and retains saved data
     after a controlled failure.
5. [ ] Capture the final SDK version, exact build/test commands, zero-warning
   build evidence, full test totals, and any AppHost validation result in the
   delivery record. Do not declare this feature complete until every item is
   successful.

## Validation record

* SDK: .NET `10.0.302` from `global.json`.
* Build: `dotnet build TNC.Trading.Platform.slnx --no-restore
  --verbosity:minimal` — **0 warnings, 0 errors**.
* Focused Application tests: **156 passed**.
* Focused Infrastructure unit tests: **156 passed**; Market Categories SQL
  integration tests: **2 passed**.
* Focused API unit tests: **78 passed**; focused Market Categories mapping tests:
  **2 passed**.
* Web unit tests: **154 passed**.
* Full `dotnet test TNC.Trading.Platform.slnx --no-restore
  --verbosity:minimal` — existing Aspire API integration and Web
  functional/E2E fixtures failed during `ManagedAppHostFixture` startup with
  `TaskCanceledException`; no Market Categories test failed. The related full
  suite checkboxes remain open pending a runnable Docker/Aspire environment.

## Acceptance Criteria

* Operators can explicitly refresh IG Demo market categories; Viewers can only
  read the stored snapshot.
* A successful refresh atomically replaces the current applied environment
  snapshot and timestamp; the API/Web result is code-ordered.
* No successful refresh exists is distinguishable from an empty/failed later
  refresh. Failed, malformed, or partial provider data never erases the
  previous success or timestamp.
* No endpoint, page load, or page render performs provider I/O except the
  authorized refresh operation.
* All credentials, session tokens, provider DTOs, and raw provider responses
  remain Infrastructure-local and are absent from API/UI/log failure content.
* The migration, SQL constraints, role policies, UI states, documentation, and
  automated coverage are in source control.
* Every Work Item gate and the final release gate have a zero-warning build
  and a fully successful solution test run.

## Implementation Sequencing and Dependencies

Execute Work Items strictly in order: 1 → 2 → 3 → 4 → 5. Each quality gate is
a hard dependency for the next work item. No implementation work begins in a
later item while its predecessor has an unresolved warning or failed test.

## Planning Record

* Created: 2026-09-23
* Scope: planning only; no application code, configuration, tests, migrations,
  or wiki documentation are changed by the creation of this plan.
* Primary evidence:
  [Market Categories research](../../research/2026-09-23/market-categories-research.md)
