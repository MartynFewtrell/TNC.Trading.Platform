---
title: Clean Architecture Recommendation Review
description: Stress-test of the staged Domain, Application, and Infrastructure recommendation against repository evidence and architecture rules
ms.date: 2026-07-26
ms.topic: concept
---

## Research Questions

* Does the selected staged Domain, Application, and Infrastructure approach follow `.github/instructions/architecture.instructions.md` without adding speculative boundaries?
* Which claims or dependency proposals are unsupported or incorrect?
* Which migration hazards, sequencing constraints, circular dependencies, and contract compatibility concerns are missing?
* When should Domain be created, and which policy cluster can justify it?
* Can status and event queries become pure without changing freshness guarantees?
* Where should the hosted supervisor move, and what runtime ownership must remain explicit?
* How should API and Web share authentication setup without reversing dependencies?
* Which transaction, concurrency, IG session-token, EF initialization, and migration semantics constrain the refactor?
* Where should tests move, and can Web remove its Application reference?
* Do the three alternatives remain correctly evaluated?

## Initial Hypothesis and Discriminating Check

Alternative 2 is directionally correct, but the proposed stage order is unsafe if operational decisions remain deferred until Stage 8. Database lifecycle, writer concurrency, transaction boundaries, and IG session lifetime constrain Stages 1, 4, 5, and 6. The cheapest disconfirming check is to verify whether the current coordinator, stores, startup path, and IG contracts already encode explicit concurrency, atomicity, and token-lifetime guarantees that can survive the proposed moves unchanged.

## Findings

Status: Complete.

The consolidated recommendation is directionally correct but not safe to adopt
with its current stage order or target graph. Alternative 2 remains a reasonable
conditional target, but Alternative 1 is the correct immediate architecture
while policy is made cohesive inside Application. Domain should be created by
an extraction-readiness gate, not because Stage 3 has arrived.

The recommendation correctly preserves the existing inward source dependency,
keeps EF Core and external adapters in Infrastructure, keeps AppHost and
ServiceDefaults out of in-process behavior, rejects bounded-context project
proliferation, and separates provider wire data from inward contracts. These
points follow `.github/instructions/architecture.instructions.md:22-55` and
`.github/instructions/architecture.instructions.md:92-118`.

The recommendation needs eight material corrections:

1. Move operational decisions that constrain refactoring from the final stage
	 to the beginning. Writer concurrency, database lifecycle, local transaction
	 guarantees, notification idempotency, and token lifetime constrain the first
	 reconciliation and coordinator changes.
2. Make query purity a contract change, not a deletion of `TickAsync` alone.
	 Status currently writes during `GetOrCreateAsync` and computes current
	 schedule context over mutable state.
3. Treat the hosted supervisor as an API-owned hosting adapter around an
	 Application reconciliation command. Preserve startup blocking, one-second
	 cadence, cancellation, scope creation, and error-continuation behavior.
4. Do not claim Web can remove Application until shared authentication types
	 have an agreed owner. The current dependency is broad and intentional.
5. Replace the illustrative IG boundary. It cannot both return a session and
	 keep all raw session tokens exclusively in Infrastructure without specifying
	 an opaque-handle lifetime and process-affinity model.
6. Preserve explicit local commit boundaries while splitting ports. The current
	 configuration update is implicitly atomic because one scoped `DbContext`
	 stages configuration, credentials, and audit before one save.
7. Decide migrations before hiding initialization behind a facade.
	 `EnsureCreatedAsync` and migrations are different schema-lifecycle models,
	 and Development currently deletes a persistent SQL database at every start.
8. Make `Infrastructure -> Domain` optional. It is justified only when an EF
	 mapping or adapter genuinely consumes Domain types. Adding it to complete a
	 diagram conflicts with the instruction against speculative dependencies.

## Confirmed Findings

### Existing dependency direction is worth preserving

The current production graph is acyclic and has the important inversion
`Infrastructure -> Application`; API composes both and Web does not reference
Infrastructure. Evidence:
`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:3-7`,
`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:13-15`,
and `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:3-6`.

The selected recommendation correctly keeps API's Infrastructure reference as
a composition exception. The repository rule expressly allows concrete adapter
selection for registration, startup, and hosting while prohibiting business
decisions there. Evidence:
`.github/instructions/architecture.instructions.md:49-55`.

### Domain is justified only after policy extraction

Neither proposed first cluster is ready to move unchanged. Schedule policy is
deterministic, but its input accepts invalid time zones, empty trading days, and
inconsistent windows. Transition policy consumes Application configuration and
mutable runtime-state records. Evidence:
`src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:5-64`,
`src/TNC.Trading.Platform.Application/Configuration/TradingScheduleConfiguration.cs:1-9`,
`src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:5-75`,
and `src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs:1-44`.

Create Domain only when one cluster meets all of these conditions:

* It owns validated construction or invariant-preserving transitions
* It accepts Domain-owned values rather than Application snapshots
* It has direct behavior tests with no host, persistence, or provider
* Application maps into and out of it without Domain referencing Application
* Moving it removes a real forbidden dependency or stabilizes shared policy

Until then, Alternative 1 is not a failed architecture. It is the repository's
required incremental state under
`.github/instructions/architecture.instructions.md:56-58`.

### Pure queries match the documented API contract

The broker-login requirement says the UI reads backend-maintained status
without triggering a new login. Evidence:
`docs/006-ig-login/requirements.md:75-99` and
`docs/006-ig-login/requirements.md:120-124`.
Removing reconciliation from `GetPlatformStatus` and `GetPlatformEvents` is
therefore correct.

The implementation needs more than removing the first call. Both query methods
currently invoke `TickAsync`; status then calls a write-capable
`GetOrCreateAsync`, mutates the returned object with runtime context, and reads
several stores. Evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:31-80`.
The EF adapter inserts and commits a default state when none exists. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRuntimeStateStore.cs:10-28`.

A pure status query needs a non-creating read port and an explicit missing-state
result. It may derive current schedule context from `TimeProvider` without a
write, but it must distinguish that derived context from the latest reconciled
session state. Add a last-reconciled or observed-at value so a stopped
supervisor cannot leave stale `Active` state looking current. This preserves
the fail-safe requirement at `docs/006-ig-login/requirements.md:105-116`.

### Hosted supervision belongs at the API hosting boundary

The loop derives from `BackgroundService`, creates scopes, resolves the
coordinator, catches tick failures, logs them, delays one second, and continues.
Evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs:1-48`
and
`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisorRunner.cs:1-47`.
Those are host-adapter responsibilities, not use-case responsibilities.

Move the hosted service, scoped runner, cadence, and failure policy to API.
Application should expose one explicit reconciliation command/handler that can
also be invoked during startup. Do not move this loop to AppHost or
ServiceDefaults. AppHost composes distributed resources, and ServiceDefaults
owns telemetry, discovery, health, and resilience rather than business
execution.

Startup currently blocks before the request pipeline until schema setup,
configuration activation, retention, and an initial tick complete. Evidence:
`src/TNC.Trading.Platform.Api/Program.cs:22-54`. Preserve that availability
contract unless a new `Starting` status and readiness policy are specified.

### Infrastructure mechanisms and policy are mixed

Restart eligibility, retention eligibility, notification selection, and
redaction classification are business or application policy around valid outer
mechanisms. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:5-13`,
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionPolicy.cs:6-17`,
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatchPolicy.cs:6-24`,
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalDataRedactor.cs:7-120`.

The recommendation is right to move decisions inward while leaving EF deletes,
SMTP/Azure calls, JSON, and provider exception translation outward. Redaction
deserves extra care: the secret taxonomy is inward security policy, but
provider-specific header and payload sanitization remains beside the adapter.

### Alternative 3 remains premature

Authentication, configuration, retry, schedule, notification, and status all
share one runtime-state workflow and one SQL context today. The central
coordinator has 13 dependencies, and its operations span those concerns.
Evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:9-29`.
Splitting assemblies by putative bounded context before defining state and
transaction ownership would create cross-module coordination without current
deployment or ownership benefit. The consolidated rejection of Alternative 3
is confirmed.

## Evidence

The review read the consolidated primary document and all four cited evidence
documents:

* `.copilot-tracking/research/2026-07-26/clean-architecture-migration-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/solution-graph-and-conventions-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/application-layer-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/infrastructure-layer-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/external-clean-architecture-guidance-research.md`

Focused repository checks established the following facts:

* No production source contains an explicit transaction, row-version,
	concurrency token, reconciliation lock, or lease around auth state
* Runtime-state creation and save use separate `SaveChangesAsync` calls and no
	singleton-row constraint. Evidence:
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRuntimeStateStore.cs:10-62`
	and
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:37-48`
* Retry, event, snapshot, notification, and runtime stores commit independently.
	Representative evidence:
	`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:99-143`,
	`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinatorSideEffects.cs:12-67`,
	and
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs:30-78`
* The API startup path uses `EnsureDeletedAsync` in Development followed by
	`EnsureCreatedAsync`; no migration call exists. Evidence:
	`src/TNC.Trading.Platform.Api/Program.cs:26-47`
* AppHost gives SQL Server a data volume and persistent lifetime. Evidence:
	`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:14-22`
* Web's Application dependency supports shared options, authority/audience
	resolution, policy names, roles, claims, scopes, schemes, and audit event
	names, not Application handler dispatch. Evidence:
	`src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:1-99`,
	`src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:5-95`,
	`src/TNC.Trading.Platform.Web/Authentication/PlatformAuthAuditClient.cs:7-50`,
	and `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:4-100`
* API consumes the same options, resolver, policy registration, schemes, and
	policy names. Evidence:
	`src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs:1-68`
	and `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:13-48`
* IG CST, account security token, and API key cross the current inward port and
	are passed into proof calls. Evidence:
	`src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs:1-19`
	and
	`src/TNC.Trading.Platform.Application/Services/PlatformIgProofDataEnricher.cs:11-38`
* Repository requirements require those tokens to remain ephemeral, unlogged,
	unpersisted, and unavailable to API/UI callers. Evidence:
	`docs/006-ig-login/requirements.md:38-56`,
	`docs/006-ig-login/requirements.md:105-116`, and
	`docs/wiki/architecture.md:216-229`
* The current Application unit project references Infrastructure, while the
	repository test convention requires separate test projects by owner and test
	type. Evidence:
	`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:20-24`
	and `.github/instructions/tests.instructions.md:25-58`.

## Required Corrections

### Correct the target dependency graph

Use this graph while Domain exists:

```text
Domain <- Application <- Infrastructure
						 ^                ^
						 |                |
						API --------------+

Web -> ServiceDefaults
Web -> Application only while shared host-auth contracts remain there
AppHost -> API, Web
Infrastructure -> Domain only for demonstrated direct mapping/use
```

The primary graph's unconditional `Infrastructure -> Domain` and absence of
`Web -> Application` are target aspirations, not currently supported edges.
Adding a direct Infrastructure edge without a compile-time consumer violates
the prohibition on speculative dependencies at
`.github/instructions/architecture.instructions.md:56-58`.

Prevent two likely circular designs:

* Domain must not consume `PlatformConfigurationSnapshot` or
	`PlatformRuntimeState`; otherwise moving those Application types produces
	`Application -> Domain -> Application`. Map to Domain-owned values first.
	Evidence:
	`src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:5-55`.
* A shared authentication project, if eventually justified, must not reference
	Application, API, or Web. API and Web may reference it. Do not place these
	types in ServiceDefaults or make API reference Web.

### Move semantic decisions before structural stages

Replace the current Stage 8 deferral with a Stage 0 decision record and focused
characterization tests for:

* One API replica versus multiple replicas
* Scheduled reconciliation, startup reconciliation, manual retry, and
	configuration-update writer serialization
* Optimistic concurrency, a database lease, or a single elected writer
* Atomic local records versus idempotent/resumable effects
* Notification delivery deduplication or outbox semantics
* EF migration ownership and startup behavior
* Data Protection key-ring durability
* IG session-token lifetime and future session-consuming operations

The current process-local notification set does not coordinate restarts or
replicas. Evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:23-28`
and
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:707-725`.
Manual retry and scheduled reconciliation both perform writes, so making the
scheduler the only tick caller does not by itself create a single writer.
Evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:82-173`.

### Preserve transaction intent during port decomposition

Do not split configuration updates into independently committing configuration,
credential, and audit ports. The current implementation stages configuration
changes, protected credential entities, and an audit entity on the same scoped
context and commits them together. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:43-113`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs:27-91`.

Use one operation-specific commit port or an Application-owned transaction
intent implemented by Infrastructure. Avoid a generic repository or generic
unit-of-work wrapper. Keep remote IG and notification I/O outside a database
transaction.

For reconciliation, choose one of two explicit semantics before extracting
handlers:

* Commit all local state, retry, event, and snapshot effects through one
	operation-specific persistence boundary after external calls complete
* Make each effect idempotent and persist workflow progress so retries resume
	safely

The illustrative handler in the primary recommendation is not sufficient: a
state save followed by a separate event append can leave inconsistent local
records. The current coordinator already exposes this failure mode through
independent saves at
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:99-143`.

### Define concurrency before making queries pure

Pure reads remove status/event requests as writers, but startup, the supervisor,
manual retry, and configuration update still race. The current update handler
also invokes `TickAsync` after persistence. Evidence:
`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:5-15`.

At minimum:

* Give the single runtime-state row a stable key or uniqueness constraint
* Add an optimistic version/concurrency token if multiple writers remain
* Serialize manual retry and scheduled reconciliation through the chosen writer
* Make event/notification idempotency keys durable rather than process-local
* Test two concurrent reconciliations and two concurrent initial creates against
	SQL Server, not only EF InMemory

### Replace the IG port proposal

For the current requirement, prefer one capability such as
`AuthenticateAndCollectProofAsync` that returns:

* Sanitized non-secret login evidence
* Sanitized account/position proof data
* A classified inward failure outcome

Infrastructure should authenticate, hold CST/security-token/API-key values only
for the dependent proof calls, then discard them. This matches the implemented
same-tick requirement at `docs/wiki/architecture.md:216-229`.

Do not return the proposed `BrokerSession` until a later use case requires a
long-lived session. At that point specify whether it is:

* An inward secret-bearing value, which conflicts with the present security
	boundary
* An opaque handle to an Infrastructure session cache, which introduces
	process affinity, expiry, disposal, replica routing, and restart semantics
* A capability-oriented gateway operation that hides the token entirely

The third option is preferred. The current adapter also ignores the requested
environment and hard-codes Demo. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:62-65`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:23-68`.
Do not add Live routing as part of this migration because Live is explicitly out
of scope at `docs/006-ig-login/requirements.md:57-66`; either reject Live at the
gateway or defer a Live endpoint until requirements authorize it.

### Decide EF lifecycle before introducing an initializer

An Infrastructure startup facade improves encapsulation but must not conceal an
undecided schema policy. `EnsureCreatedAsync` bypasses migration history, while
Development deletion destroys configuration and encrypted credentials in the
persistent SQL volume. Evidence:
`src/TNC.Trading.Platform.Api/Program.cs:26-47` and
`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:14-22`.

Before the facade is accepted:

1. Choose source-controlled migrations beside `PlatformDbContext`.
2. Define how the existing no-history database receives the initial migration,
	 including whether local data is intentionally reset once.
3. Choose who applies migrations: a deployment job/bundle is safer for multiple
	 replicas; guarded startup migration may remain a local-development option.
4. Preserve ordering: schema, startup configuration, retention, initial
	 reconciliation, then readiness/request serving.
5. Add SQL Server tests for constraints, transactions, concurrency, and
	 migration from the previous schema.

Migrations remain in Infrastructure beside
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:5-119`.

### Treat Web dependency removal as optional

Web can remove Application only after authentication ownership changes. The
shared surface includes options, provider resolution, roles, policies, claims,
scopes, schemes, and audit event names. Evidence:
`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs:1-120`,
`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationOptions.cs:1-179`,
and
`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationConfigurationResolver.cs:1-141`.

Move ASP.NET Core policy registration into each host first so Application can
drop the shared framework reference. Evidence:
`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35`
and
`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:9-11`.

Then choose deliberately:

* Retain `Web -> Application` temporarily for neutral constants/options. This
	dependency points inward and is less harmful than a speculative project.
* If the shared surface remains substantial and stable after host registration
	moves, create one narrowly named host-auth contract assembly referenced by
	API and Web only. Do not call it generic `Shared` or `Contracts`.
* Duplicate only tiny host-specific registration behavior, not role/scope names
	whose divergence would break authorization compatibility.

Preserve configuration keys and API/Web authorization names during any
namespace move. Existing Web and API auth tests should move with their host
behavior rather than being rewritten as Domain/Application tests.

### Preserve external contract compatibility

Internal movement must retain:

* Existing API paths, verbs, status codes, and response JSON
* `202 Accepted` versus `409 Conflict` for manual retry when replacing normal
	exceptions with typed results. Evidence:
	`src/TNC.Trading.Platform.Api/Features/Platform/TriggerManualAuthRetryEndpointHandler.cs:8-36`
* Problem Details behavior for configuration validation when policy moves from
	API to Application. Evidence:
	`src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationEndpointHandler.cs:10-29`
* Latest status, latest non-secret login snapshot, proof data, and history
	projection shapes. Evidence:
	`src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusMapping.cs:8-76`
	and
	`src/TNC.Trading.Platform.Api/Features/GetIgLoginHistory/GetIgLoginHistoryMapping.cs:7-24`
* Startup availability semantics and authorization policy/scope names

Add characterization tests at API and Web boundaries before namespace or
contract movement. Do not expose Domain entities directly to either adapter.

### Correct test ownership and placement

Create Domain tests only with Domain:

```text
test/TNC.Trading.Platform.Domain/
	TNC.Trading.Platform.Domain.UnitTests/
```

Application unit tests must reference Application and Domain only. Rewrite
policy and handler scenarios with inward fakes. Move tests according to the
behavior they prove:

* Hosted supervisor cadence/error behavior moves to API unit tests
* IG HTTP, sanitization, mapping, and token-boundary tests belong to
	Infrastructure unit tests
* EF mapping and adapter transaction tests belong to Infrastructure tests
* SQL migration, concurrency, and transaction semantics need a separate
	Infrastructure integration project with real SQL Server
* End-to-end startup ordering, readiness, Keycloak, and service wiring remain
	Aspire closed-box tests

Do not move every coordinator test wholesale to Infrastructure. First retain
Application policy/use-case assertions with fakes, then move only tests whose
claim depends on a concrete adapter. This follows
`.github/instructions/tests.instructions.md:25-58` and avoids preserving the
current misplaced project edge at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:20-24`.

## Residual Risks

* The requirement says the backend maintains a usable IG session, but the
	implementation discards session tokens after same-tick proof calls and marks
	the session active using a configured 15-minute lifetime. Evidence:
	`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:348-428`
	and
	`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:677-684`.
	Future trading operations need a separate session-lifecycle decision.
* A one-second supervisor across multiple API replicas can amplify IG login,
	event, and notification calls unless leader/lease behavior is defined.
* Notification transport occurs before its audit rows are committed. A database
	failure after provider success can produce duplicate delivery on retry.
	Evidence:
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs:30-78`.
* Data Protection ciphertext durability depends on a persistent shared key ring,
	but API currently only calls `AddDataProtection()` with no visible key-ring
	storage. Evidence: `src/TNC.Trading.Platform.Api/Program.cs:11-19`.
* Proof data is a process-local singleton, so a restart or another replica can
	return no proof despite SQL-backed active state. Evidence:
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/InMemoryPlatformIgProofDataStore.cs:6-17`
	and
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:60-60`.
* Moving policy inward can reveal that retained API wording is Demo-specific.
	Keep the current contract stable during architecture work and change language
	only through a requirement-backed API version or compatible field evolution.
* Domain extraction may provide little value if schedule and transition rules
	continue to change with use-case workflow. The readiness gate must be allowed
	to conclude that no Domain project is needed yet.

## Final Recommendation

Confirm the architectural direction with amendments.

Use Alternative 1 as the immediate migration architecture: purify Application,
move hosted/framework/provider concerns outward, restore honest commands and
queries, and establish direct handler/policy tests. Treat Alternative 2 as the
conditional target once one policy cluster owns validated values and invariant
behavior. Continue to reject Alternative 3 until capability ownership,
transactions, and team/deployment boundaries become stable.

Use this corrected sequence:

1. Record behavioral contracts and decide replica, concurrency, transaction,
	 notification, token, Data Protection, and EF migration semantics.
2. Add role-aware architecture checks and API/Web characterization tests.
3. Introduce an explicit reconciliation command; move hosted scheduling to API;
	 preserve startup reconciliation and readiness ordering.
4. Add read-only state/query ports, remove query-triggered writes, and expose
	 freshness or latest-known semantics.
5. Refactor one write use case while preserving one local commit boundary and
	 external-effect idempotency.
6. Replace the IG boundary with a capability that consumes ephemeral tokens
	 inside Infrastructure and returns sanitized results.
7. Move ASP.NET Core auth registration to hosts; defer Web's Application
	 reference removal until shared auth ownership is justified.
8. Introduce migrations and the Infrastructure initialization facade under the
	 decided deployment model.
9. Create Domain only when the extraction-readiness gate passes; add only the
	 project references that real consumers require.
10. Reorganize Infrastructure namespaces mechanically after behavior and
		contracts are stable.

This sequence follows the repository's instruction to preserve valid boundaries,
move one responsibility at a time, and avoid speculative projects or pass-through
abstractions. It also prevents the migration from changing consistency,
availability, security, or public contracts accidentally.

## Clarifying Questions

The architecture direction is not blocked, but implementation requires product
or operations decisions on these points:

* Will API run as one replica or multiple replicas in the first deployed target?
* Must configuration, credential, and audit updates be atomic as they are now?
* Is notification delivery at-most-once, at-least-once, or best effort?
* Is IG proof data intentionally process-lifetime state or should it survive
	restart and remain consistent across replicas?
* Who owns production schema migration execution and Data Protection key-ring
	persistence?
* Will a future use case require IG session tokens beyond the immediate proof
	calls, or can the adapter keep them entirely within one capability call?

## Recommended Next Research

* [ ] Decide the deployment replica and writer-leasing model
* [ ] Define local transaction and notification delivery guarantees
* [ ] Design the initial EF migration transition from `EnsureCreatedAsync`
* [ ] Trace production Data Protection key-ring configuration
* [ ] Decide IG session lifetime for future trading capabilities
* [ ] Prototype a read-only status port and freshness contract without changing
	the existing HTTP response shape
