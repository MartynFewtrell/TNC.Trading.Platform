# 001 Project scaffolding and DevEx technical specification

This document describes how work package 001 will be implemented, based on `requirements.md` in this folder and aligned to the project-level `../business-requirements.md` and `../systems-analysis.md`.

## 1. Summary

- **Source**: See [Requirements](requirements.md) for canonical work metadata (work item, owner, dates, links) and identifiers (`FRx/NFx/SRx/TRx/ORx`). See [Business requirements](../business-requirements.md) and [Systems analysis](../systems-analysis.md) for project context.
- **Status**: draft
- **Input**: [Requirements](requirements.md)
- **Output**: [Delivery plan](delivery-plan.md)

## 2. Problem and Context

### 2.1 Problem statement

The repository needs a baseline solution structure and a repeatable local run harness so future work packages can be delivered iteratively with consistent configuration, structured logging, and health checks.

### 2.2 Assumptions

- The platform will be built on .NET.
- No `global.json` exists yet; this work package will introduce one to pin the SDK.
- The default target is the latest .NET LTS at the time of writing: .NET 10 (LTS), per Microsoft’s “Releases and support for .NET” documentation.
- Local orchestration requires Docker Desktop to be installed and running.

### 2.3 Constraints

- Work package documents must be stored in this folder per `../../.github/instructions/work-packages.instructions.md`.
- Documentation under `docs/` must follow `../../.github/instructions/docs.instructions.md`.
- Do not introduce secrets into source control and do not log secrets (`BR12`, `NFR1`, `SR2`, `SR3`).
- Prefer .NET Aspire for local orchestration per repository standards, and reference Aspire guidance via `https://aspire.dev/`.

## 3. Proposed Solution

### 3.1 Approach

Implement a minimal baseline .NET solution that includes:

- a local orchestration entry point using .NET Aspire (an AppHost project) to provide a single, consistent “run the platform locally” experience;
- a minimal HTTP host (Minimal API) to prove the baseline wiring for configuration, structured logging, and health endpoints;
- a shared defaults project to centralize cross-cutting concerns (logging, health checks, and future OpenTelemetry conventions);
- a test project skeleton that can run from the repo root, with room to add functional smoke tests aligned to the `TR1` naming convention.

This approach keeps the work package focused on scaffolding outcomes while enabling subsequent work packages to add new services and dependencies without revisiting fundamentals.

### 3.2 Alternatives considered

| Option | Summary | Pros | Cons | Decision rationale |
| ------ | ------- | ---- | ---- | ------------------ |
| A | Run services directly with `dotnet run` (no orchestrator) | Lowest initial complexity; no new tooling | Harder to scale to multiple components; inconsistent dev experience as dependencies grow; no centralized view | Rejected because it increases friction and inconsistency as work packages add components.
| B | Use Docker Compose as the orchestrator | Familiar to many developers; good for container dependencies | Adds parallel orchestration configuration; more drift risk vs repo preference; weaker .NET-first integration | Rejected because repo standards prefer Aspire for local orchestration.
| C | Use .NET Aspire AppHost as the local run harness | Single-command local run; aligns with repo standards; designed for distributed app dev; extensible for future dependencies | Requires Aspire workload familiarity | Accepted as the default local harness.

### 3.3 Architecture

#### Components

- `TNC.Trading.Platform.AppHost` (Aspire AppHost): starts the runnable projects and provides a single local run entry point.
  - Path: `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`
- `TNC.Trading.Platform.ServiceDefaults`: shared bootstrapping extensions for logging, health checks, and future observability conventions.
  - Path: `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj`
- `TNC.Trading.Platform.Api` (Minimal API): minimal HTTP service proving the baseline (config + logging + health).
  - Path: `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj`
- `TNC.Trading.Platform.Api.IntegrationTests`: closed-box Aspire integration tests that launch the AppHost and verify external behavior.
  - Path: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`

#### Data flows

- Developer runs the local harness.
- The harness starts the HTTP service.
- The HTTP service exposes health endpoints.
- Logs are emitted in a structured form to the console.

#### Dependencies

- .NET SDK pinned via `global.json`.
- .NET Aspire tooling (per `https://aspire.dev/`).
- Docker Desktop (installed and running).

## 4. Requirements Traceability

| Requirement ID | Requirement | Implementation notes | Validation approach |
| -------------- | ----------- | -------------------- | ------------------- |
| FR1 | Baseline solution structure | Create/standardize `src/` and `test/` roots; create a single solution containing all projects introduced by this work package; ensure build works from repo root. | `dotnet build` from repo root; verify projects are under `src/` and `test/`.
| FR2 | Local run harness | Add an Aspire AppHost project and wire it to start the minimal HTTP host. Document the run command sequence. | Run the documented command sequence; confirm the host is reachable and health endpoints respond.
| FR3 | Consistent configuration model | Standardize configuration sources: appsettings + environment variables + developer-local secrets mechanism. Document precedence and safe observability (non-secret). | Validate overrides via environment variables; verify no secret values are emitted in logs.
| FR4 | Structured logging | Use a single logging setup across runnable components via shared defaults; include component name and environment in log enrichment. | Inspect console logs for required fields; run a request and confirm correlation identifiers appear when available.
| FR5 | Liveness/readiness health checks | Implement health checks in the minimal HTTP host using the shared defaults. Use separate liveness and readiness endpoints. | Call liveness/readiness endpoints; confirm expected status codes.
| FR6 | Baseline local validation steps | Define an explicit local validation checklist suitable for later CI automation (build + run + health verification). | Execute validation steps from docs; confirm steps are deterministic.
| NF1 | Maintainability/structure conventions | Enforce the `src/` and `test/` structure for all new work package projects. Prefer a small number of foundational projects with clear responsibilities. | Code review + folder checks; confirm solution projects are placed correctly.
| NF2 | Repeatable local startup | Prefer a single orchestrated local run path; avoid manual secret file editing by using developer-local secret stores and env var overrides. | Cold start from clean checkout following the docs without editing tracked files.
| NF3 | Diagnose startup/config failures | Ensure startup logs include clear milestones; ensure readiness health indicates dependency failures (when dependencies exist). | Simulate misconfiguration and verify logs/readiness indicate the problem without leaking secrets.
| SR1 | No default creds beyond local dev scope | Do not introduce baked-in credentials; avoid committing default admin passwords; if an auth dependency is introduced later, it must be configured via developer-local secrets. | Repo scan for committed secret-like values; review container/default configs if added.
| SR2 | No secrets in logs/health | Ensure logging and health endpoints never return secret values; avoid dumping configuration. | Review logging configuration; run and inspect logs and health responses.
| SR3 | Local secrets approach | Document a local secrets approach that does not require committing secrets (for example environment variables and/or a developer-local secrets store). | Validate that the platform can run locally without committing secrets.
| SR4 | Reduce unsafe operation via misconfiguration | Ensure the local harness makes “local development” context explicit (environment name, app name in logs). Provide safe defaults. | Verify logs clearly indicate environment/context; ensure documentation discourages copying secrets into tracked files.
| TR1 | Baseline test structure + naming | Create a test project skeleton under `test/`; apply functional test naming convention when functional tests are added. | `dotnet test` from repo root; verify any functional tests follow `<001>_<FRx>_point_of_test`.
| TR2 | Build correctness quality gate | Provide a documented build command used in validation steps (and suitable for future CI). | Execute documented build; confirm success.
| OR1 | Build/start/validate documentation | Document prerequisites, build/start commands, health endpoints, log locations, and basic troubleshooting within `./docs/001-project-scaffolding-and-devex/` (for example as a quick-start section in `delivery-plan.md`). | Verify the documentation can be followed without leaving `./docs/001-project-scaffolding-and-devex/`.

## 5. Detailed Design

### 5.1 Public APIs / Contracts (optional)

| Area | Contract | Example | Notes |
| ---- | -------- | ------- | ----- |
| REST | `GET /health/live` | `200 OK` | Liveness: process is running. No sensitive content.
| REST | `GET /health/ready` | `200 OK` / `503 Service Unavailable` | Readiness: service is ready to do its job. Dependency checks are added over time.

### 5.2 Data Model (optional)

No data model is introduced in this work package.

### 5.3 Implementation Plan (technical steps)

| Step | Change | Files/Modules | Notes |
| ---- | ------ | ------------- | ----- |
| 1 | Pin SDK and create baseline structure | `global.json`, `src/`, `test/` | Use latest .NET LTS by default; create roots if missing.
| 2 | Create baseline solution and projects | `*.sln`, `src/TNC.Trading.Platform.AppHost/*`, `src/TNC.Trading.Platform.ServiceDefaults/*`, `src/TNC.Trading.Platform.Api/*` | Create the AppHost, ServiceDefaults, and API projects under `src/`.
| 3 | Implement consistent configuration loading | `src/*/Program.cs`, `src/*/appsettings*.json` | Define precedence (JSON → env vars → developer-local secrets mechanism).
| 4 | Implement structured logging conventions | `src/*/Program.cs`, `src/*/ServiceDefaults/*` | Include component name and environment in logs; ensure no secrets are logged.
| 5 | Implement health endpoints | `src/*/Program.cs` | Add liveness and readiness endpoints; keep responses minimal.
| 6 | Add test skeleton and smoke test | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*` | Add an Aspire closed-box smoke test (via `Aspire.Hosting.Testing`) that launches the AppHost and verifies health endpoints.
| 7 | Document local build/run/validate guidance | `docs/001-project-scaffolding-and-devex/delivery-plan.md` | Provide a work-package-local quick-start that satisfies `OR1`.

### 5.4 Error Handling

| Scenario | Expected behavior | Instrumentation |
| -------- | ------------------ | --------------- |
| Missing required configuration value | Service fails fast at startup with a clear error message that does not include secret values. | Structured error log with component and environment context.
| Health readiness failure (dependency unavailable) | Readiness returns `503` and indicates “unready” without leaking sensitive details. | Warning/error logs identifying the failing dependency at a high level.
| Unexpected exception on request path | Request fails with an appropriate status code; exception is logged with correlation where available. | Structured logs with correlation id (when present).

### 5.5 Configuration

| Setting | Purpose | Default | Location |
| ------ | ------- | ------- | -------- |
| `DOTNET_ENVIRONMENT` | Select environment (for example Development) | `Development` | Environment variable |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment selection | `Development` | Environment variable |
| `Logging:LogLevel:Default` | Baseline log level | `Information` | `appsettings.json` / `appsettings.Development.json` |
| `Health:Path:Liveness` | Liveness path | `/health/live` | App configuration |
| `Health:Path:Readiness` | Readiness path | `/health/ready` | App configuration |

## 6. Security Design

- **AuthN/AuthZ**: Not implemented in this work package. If/when local authentication is added, it must follow repo standards (Keycloak running in a container orchestrated by Aspire) and remain compatible with OIDC/OAuth 2.0.
- **Secrets**: No secrets are committed. Local secrets are supplied via developer-local mechanisms (environment variables and/or a developer-local secrets store) documented in the work package documentation (see `delivery-plan.md`).
- **Data protection**: No sensitive data is persisted. Health responses and logs must not contain secret values.
- **Threat model notes**:
  - Accidental exposure of secrets via logs/config dumps: mitigated by never logging raw configuration and by reviewing log enrichment.
  - Insecure default credentials: mitigated by not shipping default credentials and requiring developer-local secret injection.

## 7. Observability

| Signal | What | Where | Notes |
| ------ | ---- | ----- | ----- |
| Logs | Startup milestones, configuration source indicators (non-secret), request handling, health status transitions | Console | Must not include secrets (`SR2`). Include component name and environment (`FR4`). |
| Metrics | Health check status and basic request metrics (when enabled) | Local dev dashboard / future sink | Use defaults aligned with Aspire conventions when applicable. |
| Traces | Basic request traces (when enabled) | Local dev tooling / future tracing backend | If OpenTelemetry is enabled, ensure sampling/config is documented. |

## 8. Testing Strategy

| Test type | Coverage | Location | Notes |
| --------- | -------- | -------- | ----- |
| Unit | Shared defaults behaviors where practical | `test/` | Keep unit tests fast and deterministic.
| Integration | Minimal HTTP host wiring (health endpoints, startup) | `test/` | Can be added as a smoke/integration test depending on harness.
| Functional | End-to-end local smoke (build → run → health) | `test/` | If functional tests are added, use readable `MethodName_StateUnderTest_ExpectedResult` names such as `CalculateTotal_ShouldReturnZero_WhenCartIsEmpty` while keeping work package and requirement traceability explicit through structure or metadata.

## 9. Rollout Plan

| Phase | Action | Success criteria | Rollback |
| ----- | ------ | ---------------- | -------- |
| 1 | Introduce scaffolding solution + local harness | `dotnet build` succeeds; local harness starts; health endpoints respond; no secrets committed | Revert the changeset introducing the scaffolding and docs |

## 10. Open Questions

- None.

## 11. Appendix (optional)

- .NET releases and support (supported versions and LTS): https://learn.microsoft.com/dotnet/core/releases-and-support
- .NET Aspire reference site: https://aspire.dev/
- Work package requirements: `requirements.md`
- Project business requirements: `../business-requirements.md`
- Project systems analysis: `../systems-analysis.md`
