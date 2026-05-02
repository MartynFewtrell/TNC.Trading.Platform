# 002 AppHost bootstrap refactor plan

This document plans a focused refactor that reduces AppHost-owned bootstrap environment variables and moves one-time application defaults into API-owned initialization aligned with the existing work-package requirements and technical direction.

## Summary

- **Source**: See [Requirements](../requirements.md) for canonical work metadata and [Technical specification](../technical-specification.md) for the target architecture. The current refactor target is [AppHost.cs](../../../src/TNC.Trading.Platform.AppHost/AppHost.cs).
- **Status**: draft
- **Inputs**:
  - [Requirements](../requirements.md)
  - [Technical specification](../technical-specification.md)
  - [Initial delivery plan](001-delivery-plan.md)
  - [AppHost.cs](../../../src/TNC.Trading.Platform.AppHost/AppHost.cs)

## Description of work

Refactor the current Aspire AppHost bootstrap flow so `TNC.Trading.Platform.AppHost` remains responsible for orchestration and local infrastructure wiring, while the API owns one-time platform defaults and SQL-backed configuration initialization. The current `AppHost.cs` mixes responsibilities by wiring infrastructure concerns together with domain bootstrap values for platform environment, broker environment, trading schedule, retry policy, notification defaults, and bootstrap metadata.

This refactor narrows the AppHost to the concerns that should stay environment-driven and orchestration-specific, such as SQL Server wiring, Mailpit wiring, Azure Communication Services connection settings, endpoint links, and developer-local infrastructure toggles. It moves one-time initialization values into an API-owned path that can seed or initialize SQL-backed operator-managed configuration without requiring a growing list of environment variables in the AppHost. The result should improve readability, reduce long-term configuration drift, and align the implementation with the repository rule that operator-managed configuration belongs in SQL Server and is managed through the Blazor UI.

Non-goals for this refactor include changing the work-package requirements, introducing runtime environment switching, changing the selected notification transports, or replacing the existing Aspire orchestration model.

## Delivery approach

- **Delivery model**: single PR
- **Branching**: implement on `002-environment-and-auth-foundation` as a focused refactor branch update that preserves the current work-package scope
- **Dependencies**: existing SQL Server configuration persistence; API startup/bootstrap flow; Aspire AppHost orchestration; Mailpit local integration; ACS configuration handling; Blazor configuration management surface
- **Key risks**:
  - Removing AppHost environment variables too early could break local startup or leave the API without required initial values.
    - Mitigation: define a clear configuration-ownership matrix first and move defaults to an idempotent API-owned initialization path before deleting AppHost entries.
  - Defaults could become duplicated across AppHost, appsettings, SQL seed logic, and UI flows.
    - Mitigation: establish one source of truth for initial defaults and document which values are orchestration-only versus operator-managed.
  - Secret-bearing values could be accidentally moved into tracked configuration.
    - Mitigation: keep secrets externalized and retain only secret-safe bootstrap defaults in API-owned initialization.
  - Startup-fixed settings could be confused with runtime-editable settings.
    - Mitigation: preserve the current rule that startup-fixed changes are stored for subsequent startup and managed through the API/UI model rather than runtime switching.

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
| Work Item 1: Refactor AppHost bootstrap ownership | Move one-time bootstrap defaults for platform, broker, trading schedule, retry, notification, and bootstrap metadata out of `AppHost.cs`, keep only orchestration/infrastructure wiring in the AppHost, and introduce an API-owned initialization path for SQL-backed configuration defaults that remains compatible with the Blazor configuration-management model | `FR1`, `FR2`, `FR20`, `FR21`, `FR22`, `NF2`, `NF3`, `NF4`, `SR2`, `SR3`, `OR1`, `OR7`, `OR8` | `technical-specification.md` §3.1, §3.3, §5.3, §5.5, §6, §8 | Existing SQL-backed configuration model, API startup/bootstrap flow, AppHost orchestration, Web configuration UI | `dotnet build`; `dotnet test`; verify local AppHost startup; verify initial configuration values are available without the removed AppHost variables; verify operator-managed values remain SQL-backed and secret-safe | Revert the PR or temporarily restore the removed AppHost bootstrap environment variables while keeping the API-owned initialization path disabled | Continue using Aspire to run locally; after the refactor, expect AppHost to carry only orchestration and infrastructure settings while supported configuration remains owned by the API and SQL-backed storage |

### Work Item 1 details

- [ ] Work Item 1: Refactor AppHost bootstrap ownership
  - [x] Build and test baseline established
  - [x] Task 1: Define configuration ownership and bootstrap boundaries
    - [x] Step 1: Inventory the current `WithEnvironment(...)` entries in `src/TNC.Trading.Platform.AppHost/AppHost.cs`
    - [x] Step 2: Classify each value as orchestration wiring, secret/integration wiring, startup-fixed default, or operator-managed default
    - [x] Step 3: Define which values remain AppHost-owned and which values move to API-owned initialization
    - [x] Step 4: Document the resulting configuration-ownership rules in the implementation notes or updated work-package docs
  - [x] Task 2: Implement API-owned initialization for one-time defaults
    - [x] Step 1: Introduce a single API-owned initialization path for platform, broker, trading-schedule, retry, notification, and bootstrap metadata defaults
    - [x] Step 2: Make the initialization idempotent so existing SQL-backed configuration is not overwritten on each startup
    - [x] Step 3: Preserve the distinction between startup-fixed settings and operator-editable settings stored for subsequent startup
    - [x] Step 4: Keep secret values externalized and ensure no currently stored secrets are exposed through the new initialization path
  - [x] Task 3: Simplify the AppHost to orchestration concerns only
    - [x] Step 1: Remove domain bootstrap defaults from `src/TNC.Trading.Platform.AppHost/AppHost.cs`
    - [x] Step 2: Keep SQL Server, Mailpit, ACS, endpoint-link, and local infrastructure-toggle wiring in the AppHost
    - [x] Step 3: Group the remaining AppHost wiring so the file reads as orchestration rather than application bootstrap
    - [x] Step 4: Preserve existing API and web startup behavior, including Scalar and operator-UI links
  - [x] Task 4: Validate the new bootstrap flow
    - [x] Step 1: Add or update tests for initialization behavior, idempotent seeding, and reduced AppHost configuration contract
    - [x] Step 2: Run the AppHost locally and confirm the platform still starts with SQL Server and notification wiring intact
    - [x] Step 3: Confirm the Blazor configuration surface continues to display and update supported SQL-backed settings
    - [x] Step 4: Confirm startup behavior still distinguishes in-schedule, out-of-schedule, active, and degraded auth states without AppHost-owned domain defaults
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: reduce bootstrap environment variables and keep orchestration-only wiring
    - `src/TNC.Trading.Platform.Api/*`: add or update API-owned initialization, bootstrap defaults, and SQL-backed configuration loading
    - `src/TNC.Trading.Platform.Api/appsettings*.json`: only if a non-secret local bootstrap profile is needed as part of API-owned initialization
    - `src/TNC.Trading.Platform.Web/*`: verify existing configuration-management UI assumptions still match the new ownership model
    - `test/TNC.Trading.Platform.Api/*`: unit/integration coverage for initialization and configuration behavior
    - `test/TNC.Trading.Platform.Web/*`: functional coverage for configuration visibility and startup-state visibility if impacted
    - `docs/002-environment-and-auth-foundation/apphost-bootstrap-refactor-plan.md`: refactor plan
  - **Work Item Dependencies**: the API must own SQL-backed initialization before AppHost defaults are removed; tests and local validation must follow the ownership refactor before completion
  - **User Instructions**: run the platform through Aspire as normal; manage supported configuration through the Blazor UI and SQL-backed storage; expect the AppHost to stop carrying one-time trading-schedule, retry-policy, notification-default, and environment-selection values once the refactor is complete

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test`
- **Integration tests**: `dotnet test`
- **Manual checks**:
  - Start the platform through the AppHost and confirm the API and web projects still come up successfully
  - Confirm AppHost still wires SQL Server, Mailpit, ACS settings, and dashboard links correctly
  - Confirm initial non-secret platform, broker, trading-schedule, retry, and notification values are available from the API-owned initialization path rather than AppHost environment variables
  - Confirm existing SQL-backed configuration is not overwritten on restart when values are already present
  - Confirm startup-fixed values remain clearly distinguished from operator-managed values edited through the Blazor UI
  - Confirm local notification behavior still works with Mailpit and does not depend on AppHost-owned domain defaults
- **Security checks**:
  - Confirm no secret values move from external configuration into tracked files or plaintext database records
  - Confirm API responses, logs, notifications, and UI views remain secret-safe
  - Confirm the refactor preserves SQL Server as the store for operator-managed configuration

## Acceptance checklist

- [x] Refactor plan aligns with `requirements.md` and `technical-specification.md`.
- [x] AppHost responsibility is reduced to orchestration and infrastructure wiring.
- [x] One-time platform defaults have a single API-owned initialization path.
- [x] Operator-managed configuration remains SQL-backed and Blazor-managed.
- [x] Secrets remain externalized and are not exposed by the new initialization flow.
- [x] Validation and rollback steps are documented.

## Notes

- The primary concern is ownership and maintainability, not the raw number of environment variables.
- Keeping a small number of orchestration-focused AppHost variables is acceptable; the refactor should remove only values that are better treated as application initialization data.
- If a single bootstrap profile flag is still useful for local development, it should be minimal and should not reintroduce dozens of AppHost-owned domain defaults.
- This refactor should keep the implementation aligned with the repository rule that operator-managed configuration belongs in SQL Server and is updated through the Blazor UI.
- Configuration ownership after the refactor:
  - AppHost owns SQL Server references, Mailpit wiring, ACS transport settings, endpoint links, and local infrastructure toggles.
  - API-owned bootstrap configuration now seeds non-secret platform, broker, trading-schedule, retry, notification-recipient, auth-simulation, and bootstrap-metadata defaults.
  - SQL Server remains the source of truth for operator-managed settings after the initial idempotent seed.
