<!-- markdownlint-disable-file -->
# Implementation Details: IG market collection failures

## Context

Sources: user request; `.copilot-tracking/research/2026-10-04/ig-market-collection-failures-research.md` (lines 11-25, 87-140, 142-175); `.copilot-tracking/research/subagents/2026-10-04/saved-status-contract-research.md` (lines 5-28). Keep all implementation in existing `src/`, `test/`, and `docs/wiki/` layouts. No product changes are made by this plan.

## Phase 1: Diagnose and contain the incident

<!-- parallelizable: false -->

### Step 1.1: Exclude Demo credential handling from scope

Treat Demo key rotation, revocation and secret-store updates as an in-house operational responsibility. This plan does not perform or validate those actions. Keep all implementation evidence redacted: do not write the key or account ID to the repository, logs or test fixtures. Do not interpret credential handling as the fix for the 403: the supplied provider code indicates an allowance denial.

Files: no product files; update `docs/wiki/operator-guide.md` only if implementation guidance would otherwise imply that this plan manages credentials.
Success: no credential value is present in artifacts, and live Demo work is gated on the user's in-house confirmation.
Source: user clarification and primary research (lines 111-116, 125-140). Dependency: none for offline diagnosis or implementation; in-house confirmation is required before live calls.

### Step 1.2: Correlate failures with provider allowance and page evidence

Capture timestamps, endpoint/operation, trace ID, HTTP status, allow-listed provider `errorCode`, allowance type/reset, and category/page metadata. Compare the failed session request to the later successful download under the same profile, using IG client-apps/portal allowance data if available. Record `pageNumber`, requested `pageSize`, `totalPages`, `totalResults`, expected and actual rows on commodities, plus identifier uniqueness without recording full body or credentials. Check Demo cryptocurrency/CFD entitlement only if a later non-allowance 403 warrants it. Classify what remains unproven; do not conflate the independently captured 70-of-150 page with the logged 70-versus-42 failure.

Files: no code required; follow `docs/wiki/operator-guide.md` (lines 105-120).
Success: sanitized incident timeline, allowance bucket/reset evidence or explicit "unavailable", and reproducible page shape; distinguish quota from unclassified access denial. If IG response becomes consistent, record recovery and avoid inventing a pagination workaround.
Source: primary research (lines 89-109, 118-140). Dependency: access to retained structured telemetry; in-house credential readiness is external to this plan.

## Phase 2: Reproduce the commodities integrity boundary

<!-- parallelizable: true -->

### Step 2.1: Characterize the short-page policy

Use `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs` (lines 133-180, 236-250, 270-276) and `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketCategoryInstrumentsGatewayTests.cs` (lines 95-153, 453-479). For an initial 70-of-150 response with advertised total 290, test both a coherent bounded replay at the smaller page size (success) and an inconsistent replay (failure). Independently model the logged expected-70/actual-42 mismatch at its actual page policy, including a later-page mismatch. Assert `IncompleteCollection` / `PageRowCountMismatch` and page/expected/actual evidence only for terminally incomplete attempts; never publish their partial rows.

Files: the gateway test file above; gateway only if a confirmed defect is demonstrated by fixtures.
Success: both observed initial shapes are explicitly tested; a coherent replay publishes normally and terminal mismatches fail closed. Dependency: sanitized offline fixture, not portal access; independent of phase 3 files.
Source: primary research (lines 29-35, 58-60, 100-102, 114-115).

### Step 2.2: Validate category gateway tests

Run `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter FullyQualifiedName~IgMarketCategoryInstrumentsGatewayTests` after checking the exact project path. Record baseline differences rather than disabling checks. Dependency: step 2.1; runs independently of phase 3's test project selection if builds do not contend, otherwise defer to phase 5.

## Phase 3: Reproduce allowance and access handling

<!-- parallelizable: true -->

### Step 3.1: Characterize the session error cases

Use `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketDetailsGateway.cs` (lines 29-37, 551-598, 674-690) and `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketDetailsGatewayTests.cs` (lines 400-513). Verify known exact allowance 403 is non-retryable within the attempt, unknown and entitlement-style non-allowance 403 remain unclassified authorization failures unless an approved explicit mapping exists, and 429 is distinct. Test redaction and no automatic intra-attempt retry. Check later scheduled-run eligibility in `src/TNC.Trading.Platform.Application/Features/MarketDetails/MarketDetailCollectionCoordinator.cs`; add a focused coordinator test if existing coverage misses recovery.

Files: gateway and coordinator test files at the corresponding existing test-project locations; production gateway/coordinator only for reproduced defects.
Success: 403 variants and 429 remain distinct, no secret leakage, and a later allowance-reset run can execute in a deterministic test. Dependency: sanitized offline fixture, not portal access; independent of phase 2 gateway/tests (run shared suite in phase 5 if concurrent builds conflict).
Source: primary research (lines 37-44, 99-108, 142-167).

### Step 3.2: Validate gateway tests

Run the same infrastructure unit-test project with filter `FullyQualifiedName~IgMarketDetailsGatewayTests` once phase 3 edits are complete; defer if sharing build output with phase 2. Dependency: step 3.1.

## Phase 4: Persist and present safe diagnostics

<!-- parallelizable: false -->

### Step 4.1: Design a bounded diagnostic contract and migrate storage

Decide safe nullable fields per attempt and failed detail target: `FailureKind`, allow-listed `ProviderErrorCode`, `HttpStatus`, `Operation`, `PageNumber`, `Expected`, `Actual` where meaningful. Extend `MarketDetailGatewayFailure` to retain the allow-listed code, HTTP status and fixed operation captured by `IgMarketDetailsGateway`, then thread those fields through the detail coordinator's failed target result, run store and read store; make the category failure and coordinator carry only fields available for that category request. Avoid storing arbitrary provider codes, response bodies, identifiers or headers; map only known codes and fixed operation names. Use the existing migration mechanism for nullable columns on `InstrumentCollectionCategoryAttemptEntity` and the detail target entity; do not backfill historical failures as if diagnostics were known. If provider code cannot be captured for category 403 yet, leave it null rather than inferring the market-detail session code. Retain category snapshot time independent of attempt time.

Files: `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentFailure.cs` (lines 4-15), `src/TNC.Trading.Platform.Application/Features/MarketDetails/MarketDetailGatewayFailure.cs` (lines 3-6), `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/InstrumentCollectionCategoryAttemptEntity.cs` (lines 3-14), existing detail target entity and EF migration folder (locate at implementation), `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentCycleStore.cs` (lines 899-950), `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketDetailRunStore.cs` (lines 408-425).
Success: new rows store only typed safe fields and an exact allow-listed detail code survives persistence; old rows read with null diagnostics and retain existing safe error; migration upgrades an existing SQL database. Dependencies: phases 2 and 3, migration tooling and existing persistence conventions.
Source: saved-status trace (lines 5-23); primary research (lines 103-108, 142-151).

### Step 4.2: Carry diagnostics through read, API, and UI

Extend `MarketCategoryInstrumentCategoryStatus`, `EfMarketCategoryInstrumentStatusReader`, `MarketCategoryInstrumentStatusResponse`, `MarketCategoryInstrumentEndpointMapping`, the web status view-models and `MarketCategories.razor` to render saved-category classification and contextual counts/status for Viewer users, with neutral text when historical fields are null. For detail failures, map the failed target result's known allowance code, HTTP status and operation through the detail read store, API response, web view-model, `MarketDetailsPagePresenter`, and `MarketDetails.razor` only where that saved target result applies. Assert exact-code fidelity after a database reload. Do not present an unknown 403 as confirmed entitlement; never label one category's 403 with another operation's error code. Preserve separate last-success and last-attempt presentation and independent detail observation/coverage.

Files: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentStatusReader.cs` (lines 45-104), `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCategoryStatus.cs` (lines 6-14), `src/TNC.Trading.Platform.Api/Features/Platform/MarketCategoryInstrumentStatusResponse.cs` (lines 4-32), `src/TNC.Trading.Platform.Api/Features/Platform/MarketCategoryInstrumentEndpointMapping.cs` (lines 56-112), `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` (lines 277-344), `src/TNC.Trading.Platform.Web/MarketCategoryCollectionStatusViewModel.cs` (lines 3-11), `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategories.razor` (lines 81-144), `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketDetailReadStore.cs` (lines 23-57, 231-266), `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketDetails.razor` (lines 56-80).
Success: both saved views show truthful safe diagnostics after restart, older rows still render, and Viewer authorization and response semantics remain intact. Dependency: step 4.1.
Source: saved-status trace (lines 9-23).

### Step 4.3: Add bounded telemetry and regression coverage

Add a classified failure counter at the category/detail coordinator boundaries with environment, category, failure kind and only allow-listed provider code labels; register the meter through `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs` (lines 103-126). Avoid key, account, instrument identifiers and unbounded messages as labels. Improve session warning text for known allowance without relabeling unknown 403. Add SQL migration/persistence, endpoint and web tests proving a failed current attempt plus a prior successful snapshot, and a separately classified market-detail target; test legacy null fields and secret redaction. Reuse existing test projects and naming/traceability comment conventions.

Files: category/detail coordinators and their existing test projects; `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs`; `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/MarketCategoryInstrumentSqlIntegrationTests.cs` (lines 621-632); corresponding `MarketDetailSqlIntegrationTests.cs` (lines 640-666); `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/MarketCategoryInstrumentEndpointTests.cs` (lines 89-111); `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MarketCategoriesTests.cs` (lines 47-78) and `MarketDetailsTests.cs` (lines 22-90).
Success: typed low-cardinality counters and cross-layer tests confirm saved failure and earlier success are independent; no known 403 maps to invalid credentials. Dependency: steps 4.1-4.2.
Source: primary research (lines 142-167); saved-status trace (lines 25-28).

### Step 4.4: Update operator guidance

Update `docs/wiki/operator-guide.md` (lines 101-139, 480-497) with Demo allowance investigation, later-run recovery, page mismatch, safe status labels and credential-safe capture. Link to `https://aspire.dev/` as primary source if describing Aspire telemetry operations. Do not add a numbered work package.
Success: operators can distinguish quota, unclassified access denial and provider page inconsistency from saved state and know when to escalate to IG. Dependency: steps 4.1-4.3.

## Phase 5: Validate and hand off

<!-- parallelizable: false -->

### Step 5.1: Run focused then full project checks

Run the affected infrastructure unit and SQL integration tests, API and web unit tests, then `dotnet build` and the repository's full test/lint scripts or solution test command once all edits land. Verify migrations against existing SQL and a fresh database; if Docker/Aspire integration prerequisites are unavailable, report the exact unrun checks. Test coherent and inconsistent pagination, known/unknown 403 and 429, later schedule recovery, legacy rows, redaction and UI/coverage separation. Do not weaken validation to make tests pass.

### Step 5.2: Reset local application data and verify startup scheduling

Identify the repository-supported reset mechanism and the exact database/data stores it affects before executing it. Export or back up the current local application data so the reset is recoverable, then reset only the disposable local application state required to remove saved collection attempts, snapshots and scheduling markers that could suppress a fresh market-data download. Do not delete credentials, provider configuration, unrelated operator settings, shared databases or production data. Start the application using the normal Aspire/local path and verify structured logs show the market-data collection scheduler starting and attempting the relevant download. If no supported reset mechanism exists, stop this step and record the missing operational procedure rather than issuing an ad hoc destructive command.

Files: existing repository reset/startup scripts and local database configuration (locate during implementation); `docs/wiki/operator-guide.md` if the reset procedure is not documented.
Success: a recoverable backup exists, only intended disposable application state is reset, startup begins a new collection attempt, and no credential/configuration values are changed. Dependency: step 5.1, a disposable local environment, and the user's confirmation before any destructive local reset.

### Step 5.3: Perform a controlled Demo confirmation

When the in-house operator confirms Demo credentials are ready and allowance conditions are suitable, run the collection after the verified reset to confirm only outcomes actually observed and inspect saved state and sanitized telemetry. Use deterministic gateway/coordinator/integration tests from steps 3.1 and 4.3, not a forced provider error, to prove known allowance, non-allowance 403, pagination mismatch and next-slot recovery. Observe a second scheduled slot only if safely available; record unobserved provider outcomes explicitly. Fix only minor validation issues in this phase; re-plan major provider/API contract changes.

Dependencies: phases 1-4; Docker/Keycloak/SQL prerequisites for end-to-end execution, Demo access for live confirmation.
