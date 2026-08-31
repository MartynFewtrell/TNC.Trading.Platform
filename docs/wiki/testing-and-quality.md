---
title: Testing and quality
description: Test levels, quality gates, and regression coverage for the trading platform
author: TNC Trading
ms.date: 2026-07-27
ms.topic: reference
---

Framework-boundary coverage includes an architecture rule that rejects ASP.NET Core framework references from Application, plus focused Application, API, Web, and AppHost tests that protect composition and authentication parity. Phase 8.2 requires the solution build and the unfiltered `phase-8-2-framework-reference-removal` quality gate before further migration work.

This document explains how the current solution is validated, what each test suite covers, and what kinds of regressions the repository is already protecting against.

## Testing strategy summary

The repository uses multiple test levels so the current control-plane behavior is validated from unit level up to browser-driven flows.

### Coordinator retirement coverage

The Phase 6.6 retirement check searches live `src/`, `test/`, and project files
for `PlatformStateCoordinator` references before running the focused suites and
the complete solution gate. The search must find no active coordinator caller,
registration, or test construction. Reconciliation coverage now targets the
explicit `ReconcilePlatformAuthenticationHandler` and its
`PlatformAuthenticationReconciler`; status, events, configuration, manual
retry, audit, and IG login-history coverage targets their individual handlers
and inward ports. This keeps ownership assertions focused on observable
behavior while proving the retired broad coordinator cannot return through a
registration or dependency path.

## Phase 1 safety-rail coverage

Characterization tests protect observable behavior that later migration phases
may change: API route results, status codes, JSON response shape, validation
Problem Details, authorization boundaries, startup readiness, configuration
updates, manual retry acceptance and conflict outcomes, status and event reads,
and representative Web rendering and API-client behavior. These tests assert
transport, persistence, and rendered outcomes rather than coordinator calls,
service registration details, or folder structure.

The architecture integration suite retains the topology-neutral graph checks and
adds a separate role-policy validator. It checks project-file references,
framework references, and stable package families, while keeping the temporary
Web-to-Application authentication-contract exception explicit. Domain checks
activate only when a Domain project exists. The validator tests include a
disposable forbidden-reference probe so the new rule is demonstrated without
leaving a deliberate violation in the repository.

Run the focused Phase 1 checks from the repository root:

```powershell
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj
```

The focused checks are necessary feedback, but they do not replace the
unfiltered solution gate below.

### Supervision hosting coverage

The API unit suite directly tests the host adapter's lifecycle boundary:

- `ExecuteAsync_ShouldContinueAfterTransientFailure_WhenNextTickRuns` proves a
   transient reconciliation exception is logged and does not stop later ticks.
- `ExecuteAsync_ShouldStopPromptly_WhenCancellationIsRequested` proves host
   cancellation exits the loop without waiting for another cadence.
- `StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy`
   proves the initial reconciliation is completed before hosted startup proceeds.

These tests inject the tick and delay seams, so they remain in-process and do
not require SQL Server, Docker, Keycloak, or a web listener. Application tests
continue to cover the reconciliation command and its feature-local workflow;
API tests cover only hosting and composition behavior.

## Test projects

| Project | Test type | Focus |
| --- | --- | --- |
| `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` | Unit | Pure retry timing, schedule evaluation, auth-state policy, use-case handlers, and application logic using fakes and in-memory state only. |
| `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` | Unit | EF/Data Protection-backed workflow scenarios, configuration persistence, secret protection, notification providers, IG adapter translation, redaction, retention behavior, and persistence adapters using EF Core's in-memory provider. |
| `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests` | Unit | AppHost settings parsing, provider-branch environment wiring, infrastructure/project registration, and focused composition-topology smoke coverage. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` | Unit | API auth-configuration behavior, configuration validation, and auth-audit summary resolution without distributed runtime startup. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` | Integration | API contracts, real AppHost-backed service behavior with one shared AppHost-plus-Keycloak runtime for the retained real-token auth slice, and the isolated synthetic-token negatives that still require controlled invalid JWT and claim-shape inputs. |
| `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests` | Architecture integration | Topology-neutral production project-reference integrity, including missing-target and cycle diagnostics. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` | Unit | Web authentication policy registration, claim mapping, direct `PlatformApiClient` boundary behavior, and bUnit component coverage for the refreshed Blazor shell and key operator pages. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` | Functional | Requirement-driven redirect, sign-out, CSRF, and rendered HTML outcomes with one shared real AppHost-plus-Keycloak runtime per auth collection. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` | End-to-end | One retained browser smoke that proves the real AppHost-plus-Keycloak sign-in path from runtime listener discovery through the protected UI surface. |

## AppHost-backed distributed validation model

The distributed validation model now follows the delivered Aspire topology rather than a substitute path.

- `src/TNC.Trading.Platform.AppHost/AppHost.cs` is now a thin composition root.
- Infrastructure registration, project registration, and shared environment wiring live in focused AppHost support files.
- AppHost-focused unit tests now cover `AppHostSettings`, provider-parity environment wiring, infrastructure/project registration, and a resource-model composition smoke that checks preserved resource names, waits, endpoint registrations, and operator-facing links before the higher-cost distributed suites run.
- AppHost-backed integration, functional, and end-to-end suites validate the real Aspire-managed runtime with Docker-backed infrastructure, SQL Server, Mailpit, and Keycloak.
- The only supported AppHost override is the narrow API-authentication switch used by the synthetic bearer-token integration slice; it keeps the Web runtime on Keycloak while allowing API-only invalid-token and claim-shape negatives to reach the protected boundary.
- Shared real-runtime helpers now start the AppHost through Aspire-managed testing, discover runtime listener URLs from the started resource set and observed local listeners, and validate the delivered listener set instead of fixed launch-settings assumptions.
- The real-token API authentication integration suite now reuses one AppHost-plus-Keycloak process per xUnit collection, while the synthetic-token API negatives stay isolated in their own AppHost-backed collection because they still require the API-only test-provider override.
- The Web functional and Web end-to-end auth suites now each reuse one AppHost-plus-Keycloak process per xUnit collection so the retained distributed coverage proves the delivered topology without repeatedly paying startup cost for every test case.

The only retained synthetic auth negatives are the isolated API bearer-token cases that need intentionally invalid issuer, audience, expiry, unsupported audit payloads, or controlled display-name claim shapes. Those tests are kept in a dedicated integration-test slice because the real provider path does not expose a stable way to mint those selectively invalid or incomplete JWTs on demand.

## What the tests already cover

### Application behavior

The unit tests cover:

- retry-delay calculation and backoff caps
- missing-credential degraded behavior
- retry-state visibility rules
- blocked-live safety behavior
- notification suppression and retry-cycle updates
- schedule evaluation
- proof-data query paths, including successful capture, accounts-query failure, positions-query failure, fallback account selection, status projection with proof data, and status projection with null proof data
- pure status projection reads that do not invoke reconciliation and return an explicit missing-state outcome when no runtime row exists
- freshness metadata from the persisted last-validation timestamp
- pure event projection reads that preserve newest-first ordering and perform no writes

Phase 6.4 query-boundary tests also verify cancellation propagation for both
status and event handlers, explicit missing-state and freshness metadata, and
the Infrastructure event projection adapter's filter forwarding with zero
write calls. These tests keep no-write behavior executable rather than relying
only on constructor shape or source inspection.

Application unit tests do not reference Infrastructure. Adapter-backed workflow
coverage that constructs EF Core, Data Protection, notification, or provider
implementations is owned by the Infrastructure unit project instead. This keeps
the Application boundary limited to use-case and policy behavior that can run
with fakes and in-memory state.

The application unit suites now prefer direct compile-time access to internal
reconciliation, schedule, and IG sanitization types instead of generic
string-based reflection helpers. The only supporting seam added for this
hardening was an internal visibility expansion for the test projects together
with making the targeted retry-cycle helper callable as an internal member, so
renamed members now fail at compile time rather than surfacing as runtime
reflection errors.

### Infrastructure behavior

The unit tests cover:

- SQL-backed configuration seeding and update behavior
- restart-required behavior for startup-fixed changes
- protected credential storage and rotation
- secret redaction in audits and operational data
- notification provider fallback and dispatch recording
- retention cleanup for operational records

- retry-cycle and IG proof-data workflow scenarios that exercise EF stores,
  protected credentials, notification dispatch, and provider-shaped ports

- two in-memory EF continued-inactivity regression tests covering both a
   changed inactive reason and an unchanged inactive reason; each verifies the
   persisted `BlockedReason` and `LastValidatedAtUtc` values, preserves
   `LastTransitionAtUtc` and retry/session metadata, and confirms that no
   notification or operational event side effects are created

- direct `PlatformStateTransitionEngine` coverage in
   `Apply_ShouldRejectSelfTransition_WhenCurrentStateIsOutOfSchedule`, which
   verifies that the strict `OutOfSchedule -> OutOfSchedule` transition is
   rejected before mutating status, reason, timestamps, session metadata, or
   degraded-state values

The infrastructure unit suites now instantiate internal persistence, notification, and configuration components directly through compile-time references. The remaining infrastructure test helper is limited to typed in-memory `PlatformDbContext` and data-protection setup so the tests no longer rely on string-based constructor, method, enum, or property lookup.

The Infrastructure integration project now runs a focused real SQL Server
suite against a unique database created inside the already-running Docker SQL
container. The fixture discovers the container endpoint and managed local
credential through Docker metadata, creates its own database, resets only that
database between tests, and drops it during cleanup. It does not launch the API
   or exercise a destructive startup reset branch; no such branch is used by the
   startup initializer.

Account Details SQL coverage verifies additive migration creation, atomic
parent/child rollback, durable context-restart readback, composite-keyset
history navigation, and cross-context environment-scoped lease contention.
Hosted authorization and browser journeys require Docker-backed AppHost, SQL
Server, Keycloak, and a deterministic fake IG HTTP endpoint. A real IG
test-account smoke is opt-in only with operator-provided credentials and is
never part of the default suite.

The Infrastructure unit suite also covers `PlatformStartupInitializer` directly:

* `InitializeAsync_ShouldApplyStepsInRequiredOrder_WhenApiStarts`
* `InitializeAsync_ShouldFailReadiness_WhenMigrationFails`
* `InitializeAsync_ShouldPropagateCancellation_WhenBootstrapIsCancelled`

These tests prove the schema, bootstrap configuration, and retention order,
fail-closed startup behavior, and cancellation propagation without replacing the
real SQL migration and provider-specific retention assertions in the integration
suite.

The SQL suite covers:

* `MigrateAsync_ShouldCreateCurrentSchema_WhenDatabaseIsEmpty`
* `MigrateAsync_ShouldUpgradeWithoutDataLoss_WhenNoHistoryDatabaseIsTransitioned`
   using an explicit guarded history baseline
* `InitializeAsync_ShouldFailClosedAndPreserveSchema_WhenMigrationConflictsWithExistingObject`,
   which injects an incompatible SQL object, verifies that the prior migration
   history and object remain, and confirms bootstrap configuration does not run
* `InitializeAsync_ShouldRecoverAfterOperatorCorrection_WhenPartialSchemaHasNoMigrationHistory`,
   which verifies that a partial no-history schema fails without destructive repair,
   then succeeds after the operator removes the partial object and retries startup
* `CommitAsync_ShouldRollbackAllLocalWrites_WhenOneWriteFails`
* `CommitAsync_ShouldRollbackConfigurationCredentialsAndAudit_WhenAuditWriteFails`,
   which injects a real SQL audit-write failure and verifies that the previous
   configuration, protected credentials, and audit history remain unchanged
* `HandleAsync_ShouldCommitAndReconcile_WhenConfigurationIsValid`, which also
   verifies the commit-before-reconcile call order
* `AcquireAsync_ShouldRejectConcurrentOwner_WhenAnotherSqlSessionOwnsTheLease`
   and `AcquireAsync_ShouldRecoverAfterOwnerRelease_WhenReplicaSessionEnds`,
   which prove SQL Server session-owned cross-replica exclusion and recovery
* `DeleteExpiredAsync_ShouldPreserveCurrentRecords_WhenSqlServerExecutesRetention`
* `GetLatestAsync_ShouldReadProofDataAfterContextRestart_WhenSnapshotWasSavedToSqlServer`,
   which writes a latest proof snapshot, disposes the writing context, reads it
   through a new context, and verifies replacement remains one row per broker
   environment.

Phase 10.1 also runs `ProtectAndUnprotect_ShouldSurviveProviderRestartAndKeyRotation_WhenSqlKeyRingIsShared`.
It uses a unique SQL database, creates the source-controlled schema, protects
with one Data Protection provider, generates a newer key, and unprotects with
a separately constructed provider. This is the executable evidence for shared
key-ring persistence and rotation compatibility. It does not claim recovery
after the database or key material is deleted.

Phase 10.2 proves the selected proof-data durability guarantee with the SQL
restart/readback test above and an AppHost composition assertion that the SQL
resource uses persistent container lifetime. Unit tests continue to use the
explicit in-memory proof store only when testing reconciliation behavior
without external infrastructure; production DI always selects the EF SQL
adapter. The focused proof-data test therefore checks the real SQL adapter,
while the composition and documentation establish that the running API uses
the same durable path.

The provider-specific migration and retention assertions are intentionally not
represented only by EF Core InMemory tests. The no-history test proves data
preservation after a guarded baseline, while the startup initializer uses SQL
migrations for production and an explicit disposable schema path only for
isolated in-memory tests.

The infrastructure notification tests now also cover the `NotificationDispatcher` runtime branches for unconfigured recipients, missing provider registrations, handled provider exceptions, failure-then-retry behavior, and repeated-attempt recording. This keeps notification routing, best-effort at-least-once semantics, failed dispatch persistence, and secret-safe failure shaping in the low-cost unit layer instead of relying on broader runtime scenarios. The tests intentionally do not claim durable duplicate prevention because the current design has no outbox or cross-process idempotency key.

### API behavior

The API and Web authentication registration tests are parity checks for the
host-owned policy boundary. `AddPlatformApiAuthentication_ShouldRegisterExpectedRolePolicies_WhenConfiguredForTests`
and `AddPlatformWebAuthentication_ShouldRegisterExpectedRolePolicies_WhenConfiguredForTests`
must continue to find the same Viewer, Operator, and Administrator policy names
and allowed-role semantics. This proves the move out of Application does not
alter route authorization while keeping ASP.NET Core registration at each host.

The API tests cover:

- health endpoints
- anonymous `401` behavior for protected endpoints
- invalid issuer, invalid audience, invalid signature, expired, and no-role bearer-token fail-closed behavior
- viewer, operator, and administrator bearer-token access behavior across status, configuration, manual-retry, events, and administrator auth-summary endpoints
- current `/api/platform/status` contract coverage for IG login current-state detail and the latest stored non-secret login payload embedded in the existing response
- explicit `stateAvailability` and `lastReconciledAtUtc` status fields for missing and persisted runtime state
- status and event contract coverage proving reads do not reconcile or write
- `GetPlatformStatusMappingTests` validation of the `IgProofDataResponse` contract shape for both populated and null cases
- persisted operator auth audit-event recording through the protected API boundary for sign-in, sign-out, access-denied, and token-acquisition-failure outcomes
- validation-problem payloads for unsupported or malformed auth-audit event submissions
- display-name fallback behavior for auth-audit summaries when `preferred_username`, `name`, or both claims are absent
- validation-problem payload shape for invalid protected configuration updates
- secret-safe responses
- role-boundary enforcement across protected API routes

The API unit suite now uses direct compile-time access to internal authentication and configuration-validation types instead of the former generic `ApiReflection` helper. A small internal auth-audit resolver seam now owns the summary, severity, and display-name fallback rules so supported audit events, unsupported event rejection, and username fallback order can be validated cheaply before the higher-cost integration tests persist events through the protected runtime boundary.

### UI behavior

The Web unit, functional, and end-to-end tests cover:

- host-local authorization policy registration for viewer, operator, and administrator routes, with API/Web parity checks
- anonymous, no-role, and elevated-role operator-context mapping
- theme-mode parsing and Radzen Software theme selection for the shared UI shell
- delegated-scope token evaluation and navigation recovery decisions for protected UI flows
- lower-level protected-route redirect decisions and auth-audit helper behavior
- direct `PlatformApiClient` success and failure-path handling for status, configuration, events, manual retry, and auth-administration requests
- bUnit coverage for the refreshed `MainLayout`, `Home`, `Status`, and `Configuration` surfaces, including signed-in versus signed-out rendering, degraded warnings, manual-retry affordances, current IG login-state labels, latest payload details, configuration save-state behavior, and access-denied routing
- public landing-page behavior
- local sign-in surface behavior in lightweight automated runs
- unit-level route-first anonymous challenge behavior for protected status, configuration, and administrator surfaces
- lightweight functional checks for public entry, tampered external return targets, and the hardened GET-versus-POST sign-out boundary
- one retained functional sign-out smoke that proves a real Keycloak-backed sign-out forces the next protected navigation back to sign-in
- one retained functional insufficient-role smoke that proves a signed-in viewer is denied from the operator-only configuration route through the real runtime
- one retained functional CSRF negative that proves the real sign-out POST rejects requests without the antiforgery token
- one retained real Keycloak browser smoke that discovers the live Web listener from AppHost startup output and reaches the protected UI without fixed-port assumptions

### Keycloak readiness and isolation

The real-runtime authentication harness uses layered readiness gates. `StartAsync()` only starts the distributed application. The harness then waits for the Aspire-managed Keycloak resource to become Healthy, verifies that the application realm discovery document returns an issuer exactly matching `http://localhost:8080/realms/tnc-trading-platform`, and performs a behavior-level check before the suite uses the runtime. API suites prove token issuance for the seeded test user, client, and scope. Web suites follow the sign-in challenge through to the Keycloak login page and later prove the protected UI flow.

The readiness policy classifies failures deliberately:

- Connection failures, temporary `404`, `429`, and `503` responses are transient and are retried against one bounded deadline.
- `400`, `401`, other permanent HTTP failures, an exact-issuer mismatch, and malformed successful discovery data fail immediately with URI, status, and safe diagnostic context.
- Timeouts retain the last transient status or exception without response content, tokens, or credentials.

Each distributed fixture attempts complete cleanup even when startup or disposal fails. It kills and waits for external AppHost processes, drains captured output, disposes the managed application and builder, restores environment-variable scopes, and preserves secondary cleanup exceptions with the primary failure. A failed readiness check must not leave a process, container, or test override behind.

The real-authentication projects disable persistent Keycloak state. Each AppHost test session therefore receives volume-free, session-scoped Keycloak state and imports the checked-in realm into a clean container. This is test isolation, not a change to the persistent local-development mode. Container lifetime, durable data, and test session state remain separate concerns.

The imported realm contains wildcard localhost callback and origin entries. The retained browser sign-in smoke proves that the randomized Web listener can complete the Keycloak callback without mutating the client through the Admin API. This keeps the session fixture free from cross-test client configuration changes while preserving the real provider path.

### Port coordination and concurrent validation

Keycloak intentionally owns host port `8080` because the local issuer, browser redirects, cookies, and authority all use that origin. The participating real-authentication processes acquire a machine-local file lease at `%TEMP%\TNC.Trading.Platform\leases\keycloak-port-8080.lock` before starting the fixed-port AppHost. Acquisition waits up to five minutes and polls for release; disposal closes the lock and removes the file.

The lease coordinates processes on the same machine that use this shared lock path. It does not coordinate separate machines, isolated CI containers, or processes that do not participate in the harness. xUnit collection fixtures serialize tests inside one test assembly only. They cannot prevent the API, Web functional, and Web E2E projects from starting concurrently, so the cross-process lease remains necessary. An independently running external AppHost can still bind port 8080 first and cause a test startup conflict.

Use these focused commands from the repository root when validating the retained real-runtime slices:

```powershell
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter "FullyQualifiedName~PlatformAuthenticationIntegrationTests"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~Authentication"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~Authentication"
```

To prove cross-process coordination, start the API, Web functional, and Web E2E commands concurrently. They should serialize fixed-port ownership through the five-minute lease. A timeout or bind error should be recorded as an environment conflict, not interpreted as an authentication assertion failure. Stop any separately running AppHost before repeating the proof.

Focused manual validation still complements the automated suite for the refreshed shared shell, theme switching, remembered browser preference, header presentation, and narrower-width layout behavior.

## Coverage map

The current suite ownership is intentionally pyramid-shaped so the repository keeps most auth and UI confidence in cheaper tests and retains only a narrow real-runtime smoke set.

| Area or risk | Primary suites | Retained high-cost coverage |
| --- | --- | --- |
| Route-first anonymous challenge and return-url normalization | Web unit tests, Web functional tests, API integration tests | No retained real-runtime protected-route matrix; only the lightweight public-entry and external-return functional checks remain |
| UI role boundaries and access-denied routing | Web unit tests, Web functional tests | One real-runtime functional viewer-to-configuration denial smoke |
| Sign-out wiring, fail-closed post-sign-out behavior, and OIDC logout participation | Web unit tests, API integration tests, Web functional tests | One real-runtime functional post-sign-out protected-route smoke |
| Sign-out CSRF hardening | Web functional tests | One real-runtime functional missing-antiforgery negative |
| Browser-provider parity for the delivered local auth path | Web unit tests, Web functional tests | One real-runtime E2E sign-in smoke from AppHost listener discovery to protected content |
| Refreshed Blazor shell and operator-page rendering | Web unit tests with bUnit | Manual responsive and theme checks only |

The current real-runtime auth matrix is intentionally narrow: one browser sign-in smoke, one functional sign-out smoke, one functional insufficient-role smoke, and one functional CSRF negative. Broader route matrices, role-policy checks, API-boundary checks, and rendered-component checks now live in lower-level suites so the distributed layer stays small and evidence-driven.

## Deterministic vs. real-IG validation

Application authentication and proof-data tests use fake
`IBrokerAuthenticationGateway` implementations plus in-memory stores. They
exercise `PlatformAuthenticationReconciler` behavior without HTTP, provider
DTOs, session headers, or Infrastructure exceptions. Direct reconciliation
coverage includes serialized writer contention, cancellation, provider and
persistence failure continuation, missing credentials, retry scheduling, and
idempotent replay behavior.

Infrastructure `IgBrokerAuthenticationGatewayTests` use a deterministic fake
`HttpMessageHandler` to cover successful session and proof translation, provider
rejection classes, malformed responses, caller cancellation, secret exclusion,
Demo routing, proof-query degradation, and Live rejection before any network
call. The architecture suite scans Application source for HTTP-client usage, IG
session-header literals, and retired provider contract names. Running
`dotnet test` requires no IG credentials or external network access.

### Opt-in real-IG smoke verification

A real IG Demo session can be verified manually by:

1. Starting the platform through AppHost: `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`
2. Navigating to `/configuration` and entering valid IG Demo credentials
3. Waiting for the next background supervisor tick (typically a few seconds)
4. Navigating to `/status` and confirming:
   - The "IG login" accordion shows a successful state and a recent `LastSuccessfulLoginAtUtc` timestamp
   - The "IG Demo proof data" accordion shows real account data with a recent `Retrieved at` timestamp

This manual path requires valid IG Demo credentials and network access to `https://demo-api.ig.com`. It is not required for normal `dotnet test` runs and should not be used as a substitute for the deterministic unit test suite.

## Refreshed quality evidence for work package 005

The latest mitigation reruns for `docs/005-refactor-app-host/` are recorded in [Quality evidence after test mitigation](../005-refactor-app-host/004-quality-evidence-after-test-mitigation.md).

### Latest Coverlet evidence

| Test project | Line coverage | Branch coverage | Material outcome |
| --- | --- | --- | --- |
| `AppHost.UnitTests` | `94.69%` | `100.00%` | New direct AppHost coverage now protects the refactored support units with a strong low-cost regression net. |
| `Application.UnitTests` | `55.11%` | `42.81%` | Stable versus the baseline review. |
| `Infrastructure.UnitTests` | `36.51%` | `24.46%` | Modest improvement over the baseline review. |
| `Api.UnitTests` | `5.87%` | `7.23%` | Improved over the baseline review, but still the weakest lower-level coverage area. |
| `Web.UnitTests` | `34.17%` | `26.16%` | Stable versus the baseline review. |

### Latest Stryker evidence

| Test project | Mutation score | Material outcome |
| --- | --- | --- |
| `AppHost.UnitTests` | `77.86%` | New strong mutation signal for AppHost settings, wiring, and composition support behavior. |
| `Application.UnitTests` | `18.80%` | Unchanged from the baseline review; future gains should come from application orchestration scenarios rather than extra percentage-only tests. |
| `Infrastructure.UnitTests` | `27.83%` | Slight improvement over the baseline review. |
| `Api.UnitTests` | `26.56%` | Material improvement over the baseline review, but still below the stronger AppHost signal. |
| `Web.UnitTests` | Not available | The rerun still fails inside Stryker when mutating `src/TNC.Trading.Platform.Web/Program.cs`, so Web mutation evidence remains blocked pending a Stryker-compatible fix or isolation strategy. |

The refreshed evidence shows that the mitigation materially improved the AppHost and API fast-feedback story without broadening the distributed suites. The remaining low-signal areas are still better treated as future prioritization input than as a reason to add brittle tests.

## Quality characteristics currently protected

The automated suite already checks important non-functional expectations:

- environment safety for Test versus Live selection
- write-only secret handling
- redaction of sensitive values from records and responses
- persisted auth audit history for sign-in, sign-out, denied access, and token-acquisition failures without exposing tokens
- stable health endpoints for orchestration
- restart-required behavior for startup-fixed configuration changes
- durable operational history when SQL-backed persistence is used
- degraded-state visibility instead of silent failure

## Test naming and traceability

The repository uses descriptive test names and requirement comments.

In the current suites, tests trace directly to the requirement sources that introduced or refined the delivered behavior:

- `docs/003-authentication-and-authorisation/requirements.md` for sign-in, sign-out, role-boundary, and local Keycloak behavior
- `docs/004-ui-update-and-refactor/requirements.md` for the refreshed Blazor shell and operator-page rendering behavior
- `docs/005-refactor-app-host/requirements.md` and `docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md` for test-pyramid hardening, fixture reuse, and reduced distributed-suite scope

This keeps the traceability view aligned with the current repository state instead of only the older environment-foundation package.

## Running the tests

### Clean Architecture migration quality gate

The migration gate runs the solution-qualified build and the complete,
unfiltered test suite from the repository root. It includes all solution-listed
projects, including Docker-backed Aspire, functional, and end-to-end tests.

Prerequisites are the SDK pinned by `global.json`, Docker Desktop for the
distributed suites, and the repository's Playwright browser prerequisites.
Provide any required local secrets through the existing user-secret or
environment configuration. The gate does not print or persist secret values.

Run it with a phase or subphase identifier:

```powershell
.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-0-baseline
```

Timestamped command logs and TRX results are written below
`artifacts/quality-gates/<GateId>/<timestamp>/`. The script reports aggregate
passed, failed, and skipped test counts and returns a non-zero exit code when
the build or complete test command fails.

If the complete test command reports the documented intermittent MSBuild
child-node failure, rerun only the serialized equivalent and record both
attempts:

```powershell
dotnet test TNC.Trading.Platform.slnx -m:1
```

Do not silently retry, add filters, or treat an environment prerequisite failure
as a passing baseline.

### Architecture graph validation

The architecture integration-test project provides a focused, topology-neutral
check over the production project graph. It verifies that direct project
references resolve to existing project files and that the production reference
graph is acyclic. The check does not infer architectural roles from names,
folders, project count, or a canonical layer layout, so valid project additions,
removals, and renames remain acceptable when the resulting graph is valid.

Run the focused check from the repository root:

```powershell
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj
```

This graph check does not yet enforce inward dependency direction, simple
boundary data or adapter translation, policy placement, inner-owned ports,
composition-root constraints, or stable component roles. Those properties
require source review, behavior-focused tests, or a repository-specific boundary
policy after the refactoring establishes stable boundaries. A passing graph
check is therefore evidence of reference integrity and acyclicity only, not
proof of complete Clean Architecture adoption.

From the repository root:

```powershell
 dotnet test
```

If a local environment hits intermittent MSBuild child-node exits during the repository-wide run, use serialized execution:

```powershell
 dotnet test -m:1
```

To build first:

```powershell
 dotnet build
 dotnet test
```

## Local test behavior notes

Many integration, functional, and end-to-end tests run through the Aspire AppHost.

The auth-focused distributed suites now run against the real Aspire-managed AppHost and Keycloak local runtime.

The Web auth suites may still use `Authentication__Test__EnableInteractiveSignIn=true` where a browser-driven helper surface is required, but that setting no longer changes AppHost composition or switches the runtime into a synthetic mode.

This keeps the suites aligned with the delivered local runtime while still exercising the distributed application shape. Distributed validation now uses the supported Docker plus Keycloak local runtime rather than an in-memory substitute path, and the shared harness forces volume-free, session-scoped Keycloak state for those auth collections so realm imports stay deterministic between runs. The harness gates startup on Aspire health, exact application-realm issuer discovery, and the behavior under test; a healthy container alone is not proof that token issuance or browser sign-in is ready.

For Web auth scenarios, the shared real-runtime helpers start the AppHost through Aspire-managed testing, discover the live Web listener from the managed runtime listeners instead of fixed launch ports, and establish authenticated browser sessions before copying the resulting platform cookie into the functional client container.

API integration tests now prefer real Keycloak-issued bearer tokens for protected-route coverage and reuse one shared real AppHost-plus-Keycloak runtime per collection for the retained real-token auth matrix. Only the isolated invalid-issuer, invalid-audience, expired-token, audit-validation, and display-name-fallback negatives remain synthetic, and those tests use a separate AppHost-scoped API provider override so the API can validate controlled JWT and claim-shape inputs without changing the default Keycloak-backed runtime.

For protected-route and sign-out functional coverage, the test suites prefer deterministic cookie-container control and redirect assertions instead of arbitrary waits or a substitute runtime.

The retained real-infrastructure auth smoke stays intentionally narrow. It reuses one AppHost-plus-Keycloak runtime per E2E collection, discovers the AppHost-started Web UI endpoint from runtime listener output instead of `launchSettings.json`, and then exercises one real Keycloak sign-in journey to the protected `/status` surface.

The functional auth collection also reuses one AppHost-plus-Keycloak runtime and keeps only the real-runtime cases that still add unique value above the lower-level suites: post-sign-out fail-closed behavior, one insufficient-role route denial, and the CSRF-negative sign-out boundary.

## Manual validation areas still worth checking

Automated tests cover a large part of the current control plane, but manual review is still useful for:

- starting the AppHost and checking dashboard links for the Web UI, API, Keycloak, Mailpit, and Scalar surfaces together with service startup output
- signing in through the live Keycloak local realm and confirming the protected operator Web experience loads through the real AppHost-managed route flow
- calling the public API health endpoints and one protected API route through the AppHost-exposed listener set
- checking local Mailpit behavior when infrastructure containers are enabled
- checking Scalar UI in development
- reviewing generated logs during degraded and recovery scenarios

The distributed auth suites should now be interpreted as real-runtime validation of the AppHost topology rather than as a synthetic fallback path.

The refreshed quality evidence also confirms that the cheapest new AppHost suite now carries most of the AppHost refactor confidence, while the distributed auth suites remain intentionally narrow and focused on the unique real-runtime behaviors that unit tests cannot prove.

## Quality checklist for future changes

When extending the application, keep these areas protected:

- do not expose stored secrets in UI, API, logs, or records
- preserve Test-platform live safety rules
- keep degraded-state UI available
- keep health endpoints stable
- maintain environment tagging in events and notifications
- keep retry behavior observable and deterministic

## Related documents

- [Application overview](application-overview.md)
- [Local development guide](local-development.md)
- [Runtime behavior](runtime-behavior.md)

## Account Preferences coverage

Coverage verifies the SQL desired-state and IG observed-state split. Application
tests cover status classification, revision guards, bounded retry timing,
account mismatch, observe-only reconciliation, manual retry, and explicit
remediation. Infrastructure SQL tests cover the current-state and audit
migration, atomic commits, restart durability, leases, stale completion
protection, and account-bound observations.

API tests cover the SQL-only GET, `Pending` update response, retry and
remediation routes, authorization, stale revisions, and secret-safe Problem
Details. The provider double is configured through
`Ig:AccountPreferencesBaseUrl`, so closed-box tests prove convergence without
contacting real IG. bUnit and Playwright coverage verifies desired and observed
labels, status warnings, retry/remediation affordances, and independent
display when IG is unavailable.

Application tests cover Test-only policy, legacy Demo presentation, validation,
typed failures, confirmed updates, indeterminate-write reconciliation,
repeated equal observations, partitioned strict cursors, retention defaults,
and secret-safe failure events. Infrastructure tests cover the typed
`trailingStopsEnabled` request contract, Version 2 session headers, strict
Boolean/SUCCESS parsing, allowance classification, redaction, restricted 401
replay, append-only persistence, equal-timestamp ordering, migration/index
creation, restart readback, and configured operational-record cleanup.

API tests cover Operator authorization, nullable-Boolean validation, stable
`400`/`409`/`429`/`502`/`503`/`504` mappings, Problem Details extension fields,
diagnostic non-leakage, and history cursor validation. Web bUnit tests
deterministically cover the Account Preferences radio semantics, pending and
provider-confirmed state transitions, `true` and `false` outbound Boolean
values, save rejection, HTTP `409` mismatch, partial success, confirmation
dismissal, and safe handling of initial-load failure. They also retain coverage
for disabled loading/save state, authorization, history paging, and
cancellation on disposal.

Distributed checks are bounded and retain diagnostics. Default automation does
not contact the real IG service; real-IG verification remains operator-
controlled manual evidence only. These bUnit tests do not prove browser
behavior, responsive reflow, keyboard interaction, or assistive-technology
behavior, which require separate manual validation.
