<!-- markdownlint-disable-file -->
# Implementation Details: Account Preferences Load Performance Mitigation

## Context Reference

Sources: `.copilot-tracking/research/2026-08-31/account-preferences-load-performance-research.md`, `.copilot-tracking/research/subagents/2026-08-31/account-preferences-mitigation-plan-verification-research.md`, and the user request dated 2026-08-31.

## Implementation Phase 1: Define the Application query contract

<!-- parallelizable: false -->

### Step 1.1: Add explicit query-state semantics

Create one Application-owned top-level type in a matching file that represents configured and unconfigured query results. Update `GetAccountPreferencesResponse` to carry this state without requiring consumers to infer meaning from an independent legacy gateway outcome and nullable SQL state. Keep the legacy provider outcome temporarily because existing tests and unsupported compositions still exercise the provider-backed branch; mark removal as follow-on work rather than mixing it into this mitigation.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesQueryState.cs` - New configured or unconfigured discriminated query-state type.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesResponse.cs` - Carry explicit query state and temporary legacy outcome fallback.

Discrepancy references:
* `PD-01` - Temporary retention of the legacy provider-backed branch.

Success criteria:
* Consumers can distinguish configured SQL state, valid unconfigured absence, and legacy provider failure without null-field inference.
* The new top-level type follows one-type-per-file and matching-file-name conventions.
* No synthetic `Guid.Empty`, rowversion, or bootstrap persistence identity represents an unconfigured installation.

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesResponse.cs` (Line 3) - Current ambiguous response shape.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesVerificationStatus.cs` (Lines 3-11) - Existing public Unconfigured vocabulary.

Dependencies:
* None.

### Step 1.2: Return valid SQL absence without provider access

Update `GetAccountPreferencesHandler.HandleAsync` so a registered current-state store always controls the production query. Convert a null store result to explicit Unconfigured query state and a populated result to Configured query state. Do not call `IAccountPreferencesGateway` in either SQL branch. Preserve the legacy provider-backed branch only when no store is registered.

Files:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs` - Correct current-state result construction and branch behavior.

Success criteria:
* A missing SQL row is a successful Unconfigured query result.
* A populated SQL row preserves all durable desired, observed, revision, and status fields.
* Both SQL branches make zero gateway calls.

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs` (Lines 14-45) - Owning query control flow.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs` (Lines 8-11) - Valid null-on-empty behavior.

Dependencies:
* Step 1.1 completion.

### Step 1.3: Add Application contract tests

Extend the handler fixture with a controllable fake current-state store. Add separately named and requirement-documented tests for missing-row and populated-row behavior, including exact gateway call-count assertions. Preserve legacy-branch tests while that branch remains supported.

Files:
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs` - Add SQL-backed query-state cases.

Success criteria:
* Missing-row test asserts successful Unconfigured state and zero gateway calls.
* Populated-row test asserts complete state preservation and zero gateway calls.
* Existing legacy-provider cases remain green.

Context references:
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs` (Lines 150-175) - Existing legacy-only fixture.

Dependencies:
* Steps 1.1 and 1.2 completion.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`

## Implementation Phase 2: Correct the API contract

<!-- parallelizable: false -->

### Step 2.1: Map configured and unconfigured success

Update the endpoint handler to prefer explicit query state. Map Configured through the existing current-state response mapper. Add an explicit Unconfigured response factory or mapper that returns null desired, account, observation, and revision fields with both application and verification status set to Unconfigured. Use legacy outcome mapping only for the temporary provider branch.

Files:
* `src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs` - Select the query-state HTTP path.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - Add unconfigured response mapping.

Discrepancy references:
* `PD-01` - Temporary legacy fallback remains explicit at the endpoint boundary.

Success criteria:
* Configured SQL state returns 200 with complete data.
* Missing SQL state returns 200 with explicit Unconfigured data.
* Neither successful SQL state enters provider failure mapping.

Context references:
* `src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs` (Lines 8-11) - Current state-discard defect.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` (Lines 9-13) - Existing configured mapper.

Dependencies:
* Implementation Phase 1 completion.

### Step 2.2: Make failure mapping exhaustive

Replace the catch-all provider-unavailable fallback with the following exhaustive contract. Keep platform authentication and authorization as framework-owned 401/403 responses; downstream provider-session rejection is not a platform challenge.

| Category | HTTP | Problem type | Stable title | `Retry-After` |
|----------|-----:|--------------|--------------|---------------|
| `UnsupportedEnvironment` | 409 | `/problems/account-preferences/unsupported-environment` | Account preferences are unsupported in the current environment. | Never |
| `Unauthorized` | 502 | `/problems/account-preferences/provider-session-rejected` | IG account preferences session was rejected. | Never |
| `RateLimited` | 429 | `/problems/account-preferences/rate-limited` | Account preferences allowance was exhausted. | Only with a validated delay |
| `MalformedProviderData` | 502 | `/problems/account-preferences/malformed-provider-data` | Account preferences provider data was invalid. | Never |
| `Rejected` | 409 | `/problems/account-preferences/state-conflict` | Account preferences request conflicts with current state. | Never |
| `Unavailable` | 503 | `/problems/account-preferences/provider-unavailable` | Account preferences provider is unavailable. | Only with a known server delay |
| `Timeout` | 504 | `/problems/account-preferences/provider-timeout` | Account preferences provider timed out. | Never |
| `Unsupported` | 502 | `/problems/account-preferences/unsupported-provider-response` | Account preferences provider response was unsupported. | Never |
| `AccountMismatch` | 409 | `/problems/account-preferences/account-mismatch` | IG session account does not match the configured account. | Never |
| `Transient` | 503 | `/problems/account-preferences/transient-provider-failure` | Account preferences provider failed temporarily. | Only with a known server delay |

Reserve `Rejected` for revision or authoritative state conflict. Reclassify arbitrary IG transport and 5xx failures as `Unavailable` or `Transient`, and classify an unrecognized deterministic non-success IG 4xx response as `Unsupported`, at the gateway boundary so none acquire misleading 409 semantics. Add `Retry-After` only when the API has a validated or known delay to communicate.

Files:
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - Exhaustive outcome, status, and Problem Details mapping.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - Classify generic provider transport and 5xx failures as Unavailable or Transient, unsupported deterministic 4xx responses as Unsupported, and only authoritative conflicts as Rejected.

Discrepancy references:
* `PD-02` - Apply the selected exhaustive HTTP contract while preserving dependency semantics.

Success criteria:
* Every named failure category has an intentional status and stable RFC 9457 title/type.
* No deterministic category falls through to provider-unavailable 503.
* Platform authentication failures remain distinct from IG provider-session failures.
* Provider transport, 5xx, and unsupported deterministic 4xx failures are not classified as `Rejected` conflicts.

Context references:
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesFailureCategory.cs` (Lines 3-15) - Categories requiring exhaustive handling.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` (Lines 37-51) - Current partial mapping and broad fallback.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` (Lines 77-141) - Current provider response classification.

Dependencies:
* Step 2.1 completion.

### Step 2.3: Add API boundary tests

Add direct endpoint-handler tests for Configured and Unconfigured query states. Expand mapping tests to enumerate named categories rather than integer casts and assert status, stable Problem Details title/type, exact `failureCategory` extension, controlled detail, and retry signaling. Assert provider response bodies, `SafeReason`, credentials, and session diagnostics are absent. Include a row-count guard against `Enum.GetValues<AccountPreferencesFailureCategory>().Length` and assert an unknown cast throws instead of falling through. For RateLimited, Unavailable, and Transient, cover both no-delay and validated-delay inputs; for every ineligible category, prove `Retry-After` remains absent even when a delay is supplied. Extend gateway tests so arbitrary provider transport and 5xx failures classify as Unavailable or Transient, only authoritative revision/state conflicts classify as Rejected, and name the unsupported 4xx regression `GetAsync_ShouldClassifyAsUnsupported_WhenProviderReturnsUnrecognizedDeterministicClientError`.

Files:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/GetAccountPreferencesEndpointHandlerTests.cs` - Direct handler behavior tests.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs` - Exhaustive category tests.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs` - Provider response classification tests.

Success criteria:
* Both SQL query states return 200 through the endpoint handler.
* Tests cover all named categories, all Problem Details fields, provider-diagnostic exclusion, and supplied-delay eligibility.
* Row-count and unknown-cast tests detect future unmapped or accidental fallback values.
* Gateway tests prevent transport, 5xx, and unsupported deterministic 4xx failures from becoming 409 conflicts.
* No-row handling never reaches provider failure mapping.

Context references:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs` (Lines 10-36) - Current partial numeric coverage.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs` (Lines 79-113) - Existing provider replay and classification coverage.

Dependencies:
* Steps 2.1 and 2.2 completion.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~IgAccountPreferencesGateway"`

## Implementation Phase 3: Specialize Web transport resilience

<!-- parallelizable: true -->

### Step 3.1: Extract reusable typed-client composition

Move `PlatformApiClient` registration from top-level startup orchestration into a Web service-collection extension callable by production startup and unit tests. In that extension, call `RemoveAllResilienceHandlers()` before adding exactly one specialized standard resilience handler so the inherited Service Defaults pipeline is replaced rather than stacked.

Files:
* `src/TNC.Trading.Platform.Web/Program.cs` - Delegate typed-client registration to the extension.
* `src/TNC.Trading.Platform.Web/DependencyInjection/PlatformApiClientServiceCollectionExtensions.cs` - Own client registration and resilience composition.

Success criteria:
* Production resolves the same typed client and base address through the extracted registration.
* The effective client has one resilience pipeline, not the global pipeline plus a specialized pipeline.
* Web unit tests can resolve the production registration through `IHttpClientFactory`.

Context references:
* `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs` (Lines 42-49) - Inherited standard handler.
* `src/TNC.Trading.Platform.Web/Program.cs` (Lines 15-18) - Current registration site.

Dependencies:
* Verify the exact resilience APIs against the Web project's resolved package before editing.

### Step 3.2: Define bounded retry semantics

Configure the specialized predicate to retry safe transport failures and attempt timeouts; safe HTTP 408, 502, and 504 responses; and HTTP 429 or 503 only when a valid `Retry-After` is present. Disable retries for unsafe HTTP methods. Preserve caller cancellation and a bounded total timeout. Keep error translation in `PlatformApiClient` unchanged unless tests reveal a contract mismatch.

Files:
* `src/TNC.Trading.Platform.Web/DependencyInjection/PlatformApiClientServiceCollectionExtensions.cs` - Configure retry predicate, retry-after handling, method safety, and timeouts.

Discrepancy references:
* `PD-03` - Apply one policy to the existing shared typed client as the smallest safe mitigation.

Success criteria:
* Deterministic unsignaled 503 responses are not retried.
* Explicitly signaled 429 and 503 responses remain eligible for safe-method retries.
* POST, PUT, PATCH, and DELETE requests are not automatically replayed.
* Transport failures and selected gateway failures retain bounded safe-read recovery.

Context references:
* `.copilot-tracking/research/2026-08-31/account-preferences-load-performance-research.md` (Lines 151-188) - Recommended retry contract.
* `https://learn.microsoft.com/dotnet/core/resilience/http-resilience` - Resilience API behavior.

Dependencies:
* Step 3.1 completion.

### Step 3.3: Test physical attempt counts

Resolve the production client registration with a counting primary handler. Add tests for one attempt on deterministic unsignaled 503, selected safe retries, one attempt for unsafe methods, caller cancellation, and absence of multiplied attempts from handler stacking. Assert attempt counts rather than wall-clock timing.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientResilienceTests.cs` - Registration-level attempt-count tests.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs` - Change only if a reusable counting handler belongs in the existing fixture.

Success criteria:
* Attempt counts exactly identify deterministic, transient, unsafe, cancellation, and non-stacking behavior.
* Tests do not use elapsed-time thresholds or jitter-sensitive assertions.
* Existing API-client parsing tests continue to pass.

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs` (Lines 96-118) - Current direct construction bypasses registration.

Dependencies:
* Steps 3.1 and 3.2 completion.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences|FullyQualifiedName~PlatformApiClient"`

## Implementation Phase 4: Make closed-box state deterministic

<!-- parallelizable: true -->

### Step 4.1: Add a SQL persistence switch

Read `AppHost:UsePersistentSqlState` in AppHost composition, defaulting to true for local development. Apply `WithDataVolume()` and persistent container lifetime only when true. In false mode, omit the named volume and use session-scoped lifetime so a fresh AppHost starts from a migrated empty database.

Files:
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs` - Conditional SQL persistence composition.
* `src/TNC.Trading.Platform.AppHost/appsettings.json` - Add the explicit local default only if AppHost configuration defaults are conventionally declared here.

Discrepancy references:
* `PD-04` - Introduce a test-harness persistence seam while retaining local durability.

Success criteria:
* Local default behavior keeps persistent SQL state.
* Closed-box false mode has neither a named SQL volume nor persistent container lifetime.
* Keycloak persistence behavior remains unchanged.

Context references:
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs` (Lines 5-18) - Existing Keycloak switch and unconditional SQL persistence.

Dependencies:
* None.

### Step 4.2: Use ephemeral SQL in test hosts

Set `AppHost__UsePersistentSqlState=false` in both shared AppHost process start paths. Add AppHost unit tests proving the persistent default and volume-free session mode. Keep fixed Keycloak lease and existing external configuration behavior intact.

Files:
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - Configure SQL isolation in both process start paths.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs` - Verify both persistence modes.

Success criteria:
* Both process and managed AppHost test paths disable persistent SQL.
* Unit tests prove resource annotations for default and ephemeral modes.
* Existing Keycloak isolation tests remain green.

Context references:
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` (Lines 13-24, 55-102) - Current shared starts configure only Keycloak persistence.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs` (Lines 16-66) - Existing resource tests.

Dependencies:
* Step 4.1 completion.

### Step 4.3: Reset provider request evidence and restore harness compilation

Add a lock-protected `ClearRequests` operation to `ControllableIgProvider` and call it at scenario boundaries before no-request assertions. In the same shared test-double file, repair the pre-existing WireMock callback return-type incompatibility using the installed API's `ResponseMessage` contract. Keep this repair behavior-neutral outside test compilation.

Files:
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - Reset ledger and correct callback response types.

Success criteria:
* Ledger reset and request snapshots use the same lock.
* Functional and E2E projects compile before new scenario assertions run.
* Provider behavior and response status/body remain unchanged by the compatibility repair.

Context references:
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` (Lines 26, 40-62) - Append-only ledger and current callback incompatibility.

Dependencies:
* Installed WireMock package API.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --filter "FullyQualifiedName~AppHostInfrastructureRegistrationTests"`
* `dotnet build test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`
* `dotnet build test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj`

## Implementation Phase 5: Prove user-visible behavior

<!-- parallelizable: false -->

### Step 5.1: Test successful unconfigured rendering

Add a presenter or bUnit test in the existing page test file that returns explicit Unconfigured current state, verifies no error alert, and confirms history is requested only after current-state success. Do not parallelize state and history in this mitigation.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Add successful unconfigured rendering coverage.

Success criteria:
* Unconfigured status renders from a successful response without warning text.
* Existing failure-suppresses-history and current-before-history behavior remains covered.

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` (Lines 24-51) - Current sequencing.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` (Lines 309-346) - Existing page behavior tests.

Dependencies:
* Implementation Phases 1 through 3 completion.

### Step 5.2: Add a fresh-install functional contract test

Create a distinct scenario that starts with ephemeral SQL, clears the IG request ledger, makes the provider unavailable, authenticates as Operator, calls the public account-preferences GET, and asserts HTTP 200 with Unconfigured state. Assert that the ledger contains no `GET /gateway/deal/preferences` attributable to the initial read. Keep convergence/update behavior in its existing separate scenario.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` - Add fresh-install closed-box behavior.

Success criteria:
* Fresh install returns 200 Unconfigured during IG outage.
* Initial GET causes zero IG preferences requests.
* The test does not depend on execution order or state from another scenario.

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` (Lines 24-52) - Existing mixed outage and convergence scenario.

Dependencies:
* Implementation Phases 1, 2, and 4 completion.

### Step 5.3: Strengthen the E2E smoke test

Use Playwright web-first assertions to wait for the loading state to exit, assert visible Unconfigured state, and assert absence of `[data-testid='account-preferences-error']` and provider-unavailable text. Prefer role, label, or text locators for visible state and retain the test id only for the negative alert assertion.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - Strengthen user-visible assertions.

Success criteria:
* The page completes loading without hard-coded waits.
* Unconfigured state is visible.
* No provider-unavailable alert is rendered.

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` (Lines 25-35) - Current label-only smoke test.

Dependencies:
* Steps 5.1 and 5.2 completion.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferences"`

## Implementation Phase 6: Align documentation

<!-- parallelizable: false -->

### Step 6.1: Document the delivered contract and operation

Update the wiki after implementation so examples and status tables use the final type and status names. Document: SQL-only configured and unconfigured GET behavior; deterministic versus dependency Problem Details; retry and `Retry-After` semantics; fresh-install operator presentation; persistent local SQL versus ephemeral closed-box SQL; and the exact tests that now prove each claim. Update architecture pages only if final ownership or type structure changes their existing statements.

Files:
* `docs/wiki/api-reference.md` - Response shape, exhaustive statuses, and retry signaling.
* `docs/wiki/runtime-behavior.md` - SQL absence and Web retry behavior.
* `docs/wiki/operator-guide.md` - Fresh-install UI and genuine failure distinction.
* `docs/wiki/testing-and-quality.md` - Exact delivered test evidence.
* `docs/wiki/local-development.md` - SQL persistence setting and test mode.
* `docs/wiki/application-overview.md` - Update only if final Application ownership differs.
* `docs/wiki/architecture.md` - Update only if final dependency direction differs.

Success criteria:
* Wiki statements match implemented status names, HTTP semantics, configuration keys, and test coverage.
* No page claims progressive history rendering or test evidence that was not delivered.

Context references:
* `docs/wiki/api-reference.md` (Lines 551-568) - Intended SQL-only Unconfigured contract.
* `docs/wiki/runtime-behavior.md` (Lines 728-747) - Intended IG-outage availability.
* `docs/wiki/operator-guide.md` (Lines 565-589) - Operator behavior.
* `docs/wiki/testing-and-quality.md` (Lines 553-590) - Current overstated coverage.
* `docs/wiki/local-development.md` (Lines 146-164) - Persistence guidance.

Dependencies:
* Implementation Phases 1 through 5 completion.

### Step 6.2: Validate documentation integrity

Run the repository-root PowerShell check below for local wiki link targets. Correct links and anchors changed by the documentation edits, then inspect changed heading fragments in Markdown Preview because the local-target check does not validate anchors. Do not broaden into unrelated Markdown cleanup or create a new repository script for this one-off validation.

Validation commands:
* `$failures = foreach ($source in Get-ChildItem '.\docs\wiki' -Recurse -Filter '*.md' -File) { $text = Get-Content $source.FullName -Raw; foreach ($match in [regex]::Matches($text, '(?<!!)\[[^\]]+\]\((?<href>[^)]+)\)')) { $href = $match.Groups['href'].Value.Trim('<', '>'); if ($href -notmatch '^(?:https?://|mailto:|#)') { $relativePath = [Uri]::UnescapeDataString(($href -split '#', 2)[0]); if ($relativePath -and -not (Test-Path (Join-Path $source.DirectoryName $relativePath))) { [pscustomobject]@{ Source = $source.FullName; Target = $href } } } } }; if ($failures) { $failures | Format-Table -AutoSize | Out-String | Write-Error; throw "Broken local wiki links: $($failures.Count)" }; Write-Host 'All local docs/wiki link targets resolve.'` - Fail when a local wiki target does not exist.

Files:
* Affected `docs/wiki/` files from Step 6.1.

Success criteria:
* Affected wiki links and anchors validate.
* Documentation diff contains only delivered behavior and necessary corrections.
* Changed heading fragments receive a manual Markdown Preview check.

Dependencies:
* Step 6.1 completion.

## Implementation Phase 7: Final validation and runtime proof

<!-- parallelizable: false -->

### Step 7.1: Run focused project validation

Run each behavior-scoped suite. Before treating Infrastructure integration as a gate, repair the pre-existing stale `NudgeAuthenticationAsync` call by supplying the current snapshot and due-time arguments in accordance with the existing test setup; keep that repair isolated from mitigation behavior.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences|FullyQualifiedName~PlatformApiClient"`
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --filter "FullyQualifiedName~AppHostInfrastructureRegistrationTests"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferences"`

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - Isolated pre-existing compile repair if still required.

Success criteria:
* All focused suites compile and pass.
* Attempt-count tests prove performance behavior without elapsed-time assertions.
* Any unavailable external prerequisite is reported with the exact skipped command and reason.

Dependencies:
* Implementation Phases 1 through 6 completion.

### Step 7.2: Run solution-level validation

Run the complete solution build and test suite after focused checks. Record existing package advisories separately from behavioral failures and do not expand this task into dependency upgrades.

Validation commands:
* `dotnet build TNC.Trading.Platform.slnx`
* `dotnet test TNC.Trading.Platform.slnx`

Success criteria:
* Solution build succeeds.
* Solution tests pass, with environmental skips or unrelated failures reported explicitly.

Dependencies:
* Step 7.1 completion.

### Step 7.3: Capture an Aspire runtime trace

Run the normal local Aspire topology against a fresh SQL test state. Load the authenticated Account preferences page once and retain trace evidence showing one current-state request, one subsequent history request, and no IG preferences span attributable to initial GET. Confirm the page has no provider-unavailable warning.

Files:
* `artifacts/quality-gates/account-preferences-load-performance-mitigation/` - Store runtime trace and concise evidence using the repository's existing artifact pattern.

Success criteria:
* Trace has one physical current-state API attempt on the healthy fresh-state path.
* History begins after successful current state under the retained sequencing.
* Initial GET produces no IG preferences request.
* UI reaches Unconfigured without warning.

Dependencies:
* Steps 7.1 and 7.2 completion.
* Docker and the local Aspire topology.

### Step 7.4: Handle validation findings

Fix minor, isolated defects in touched files and rerun the narrowest failing check. When a failure requires architectural expansion, dependency upgrades, or unrelated refactoring, document it and create follow-on research instead of widening this mitigation.

Success criteria:
* Every mitigation-caused failure is resolved and rerun.
* Remaining blockers have evidence, ownership, and a recommended next action.

Dependencies:
* Steps 7.1 through 7.3 completion.

## Dependencies

* .NET SDK from `global.json`
* Docker and Aspire-managed dependencies
* SQL Server integration-test access
* Playwright browser installation
* Exact resilience and WireMock package APIs resolved by the repository

## Success Criteria

* Fresh SQL absence is a successful, explicit Unconfigured result from Application through UI.
* Deterministic failures do not incur automatic retry delay or misleading provider-unavailable responses.
* Safe transient recovery remains bounded, observable, and covered by physical-attempt tests.
* Test hosts and request ledgers are deterministic across repeated runs.
* Focused tests, solution validation, runtime trace, and wiki documentation agree on delivered behavior.