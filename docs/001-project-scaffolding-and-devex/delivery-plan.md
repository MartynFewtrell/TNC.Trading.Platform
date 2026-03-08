# 001 Project scaffolding and DevEx delivery plan

This document plans how work package 001 will be delivered incrementally based on [Requirements](requirements.md) and [Technical specification](technical-specification.md).

## Summary

- **Source**: See [Requirements](requirements.md) for canonical work metadata (work item, owner, dates, links) and scope. See [Business requirements](../business-requirements.md) for project-level business context.
- **Status**: draft
- **Inputs**:
  - [Business requirements](../business-requirements.md)
  - [Requirements](requirements.md)
  - [Technical specification](technical-specification.md)

## Description of work

Deliver a baseline repository scaffold that enables safe, repeatable iteration on the Algorithmic Trading Platform. This includes:

- a consistent solution and folder structure (`src/`, `test/`);
- a local run harness suitable for adding more components over time (Aspire AppHost);
- consistent configuration loading patterns (including a safe approach for secrets);
- consistent structured logging conventions;
- liveness and readiness health checks; and
- baseline validation steps and test scaffolding.

Non-goals for this work package include any `IG` integration, strategy runtime, order management, risk controls, and production deployment infrastructure.

## Delivery approach

- **Delivery model**: single PR
- **Branching**: one PR from `001-project-scaffolding-and-devex` to the default branch
- **Dependencies**: Docker Desktop (local prerequisite)
- **Key risks**:
  - Risk: foundational changes can create wide blast radius (solution structure, shared defaults).
    - Mitigation: keep commits incremental within the PR and apply build/test gates and simple smoke validation at each step.
  - Risk: accidental secret leakage while establishing configuration.
    - Mitigation: explicitly document secrets approach; review for secrets; ensure no config dumps in logs.

## Delivery Plan

### Execution gates (required)

Before starting any work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (requirements) | Traceability (spec sections) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Single PR delivery (scaffold, harness, tests, docs) | Deliver the complete baseline scaffold in one PR: solution structure + `global.json`, Aspire AppHost + minimal HTTP host + ServiceDefaults (logging/config/health), baseline tests, and baseline documentation that is self-contained within this work package folder (including a quick start). | FR1, FR2, FR3, FR4, FR5, FR6, NF1, NF2, NF3, SR1, SR2, SR3, SR4, TR1, TR2, OR1 | 3.1, 3.3, 4, 5.1, 5.3, 5.4, 5.5, 6, 7, 8 | None | `dotnet build`; `dotnet test`; run the AppHost and confirm health endpoints; verify docs and links | Close or revert the PR; if merged, revert the merge commit | Follow the quick-start in this folder |

### Work Item 1 details

- [ ] Work Item 1: Single PR delivery (scaffold, harness, tests, docs)
  - [ ] Build and test baseline established
  - [ ] Task 1: Establish baseline structure and SDK pin
    - [ ] Step 1: Ensure `src/` exists (create if missing)
    - [ ] Step 2: Ensure `test/` exists (create if missing)
    - [ ] Step 3: Add `global.json` targeting the latest .NET LTS SDK (per Microsoft Learn)
    - [ ] Step 4: Create/update the solution file to include all projects introduced by this work package
  - [ ] Task 2: Implement local run harness and service defaults
    - [ ] Step 1: Create the Aspire AppHost project at `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`
    - [ ] Step 2: Create the minimal HTTP host project at `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj`
    - [ ] Step 3: Create the shared defaults project at `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj` and apply it to the API service
    - [ ] Step 4: Expose liveness at `/health/live` and readiness at `/health/ready`
    - [ ] Step 5: Validate local run via the AppHost entry point
  - [ ] Task 3: Establish baseline tests and documentation
    - [ ] Step 1: Create baseline test project structure under `test/` per repo test approach (for example `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/`)
    - [ ] Step 2: Add an Aspire closed-box smoke test that starts the `AppHost` (using `Aspire.Hosting.Testing` and `DistributedApplicationTestingBuilder`) and verifies `/health/live` and `/health/ready`
    - [ ] Step 3: Expand work package documentation to include prerequisites, build/start commands, health endpoints, log locations, and basic troubleshooting (self-contained)
    - [ ] Step 4: Ensure this work package contains a brief quick-start that can be followed without leaving this folder
    - [ ] Step 5: Document local validation steps (build/run/health)
  - [ ] Build and test validation

  - **Files**:
    - `global.json`: Pin .NET SDK for consistent local builds
    - `src/`: Product code root
    - `test/`: Automated test root
    - `*.sln`: Solution definition
    - `docs/001-project-scaffolding-and-devex/*`: Work package docs updated to include quick-start and local build/run/validate guidance
  - **Work Item Dependencies**: None
  - **User Instructions**: Follow the quick-start in this folder.

### Work Item N details (copy/paste)

Not applicable for this work package (single work item delivered in one PR).

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test`
- **Integration tests**: `dotnet test`
- **Manual checks**:
  - Ensure Docker Desktop is running
  - Start the local harness (`dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`) and verify health endpoints
  - Confirm logs are structured and do not include secrets
- **Security checks**:
  - Ensure no secrets are committed (review changes for secret values)
  - Ensure health endpoints and logs do not dump configuration values

## Acceptance checklist

- [ ] Work item aligns with `../business-requirements.md`.
- [ ] All referenced `FRx` requirements are implemented and validated.
- [ ] All referenced `NFx` requirements have measurements or checks.
- [ ] All referenced `SRx` security requirements are implemented and validated.
- [ ] Docs updated under `./docs/001-project-scaffolding-and-devex/`.
- [ ] Rollback/backout plan documented for each work item.

## Notes

- The local run harness uses .NET Aspire per `https://aspire.dev/`.
- The .NET SDK target is the latest LTS by default (currently .NET 10 LTS) unless a repo-wide pin exists.

## Quick start

This quick start is provided to satisfy `OR1` (work-package-local guidance).

### Prerequisites

- .NET SDK installed (version pinned by `global.json`)
  - Install guidance: [Install .NET SDK](https://learn.microsoft.com/dotnet/core/install/)
- Docker Desktop installed and running (required)
  - Install guidance: [Docker Desktop](https://www.docker.com/products/docker-desktop/)

1. Build from the repo root:
   ```powershell
   dotnet build
   ```
2. Run the local harness from the repo root:
   Primary:
   ```powershell
   dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj
   ```

   Optional alternative (requires Aspire CLI):
   ```powershell
   aspire run
   ```
3. Verify health:
   - Use the Aspire dashboard to navigate to the API service endpoint.
   - Call `GET /health/live` and `GET /health/ready`.
4. Logs and troubleshooting:
   - Logs are written to the console by default (AppHost output and service output).
   - If `GET /health/ready` returns `503`, review console logs for the readiness failure reason (without exposing secrets).
