# AppHost Aspire Password Defaults Plan

> Use this plan to remove the AppHost SQL Server and Keycloak password prompts for new developers by adopting Aspire's default local password generation and persistence behavior while preserving the delivered authentication and authorization experience.

## Summary

- **Source request**: adopt the default Aspire behavior for managing the local SQL Server and Keycloak passwords so the Aspire Dashboard no longer prompts for them on first run.
- **Work package**: `./docs/003-authentication-and-authorisation/`
- **Status**: `in-progress`
- **Inputs**:
  - `../requirements.md`
  - `../technical-specification.md`
  - existing numbered plan files in this folder (for example `001-delivery-plan.md` and `004-work-package-refactoring-mitigation-plan.md`)
  - `../../src/TNC.Trading.Platform.AppHost/AppHost.cs`

## Description of work

This plan updates the AppHost composition so the local SQL Server and Keycloak resources no longer depend on explicitly declared password parameters that force manual input through the Aspire Dashboard. Instead, the AppHost should rely on Aspire's built-in password generation and secret persistence for local resource startup.

The work is intentionally narrow and behavior-preserving for the delivered authentication package: local Keycloak sign-in must continue to work with the seeded realm import, the local SQL-backed persistence path must remain intact, the stable Keycloak port and current service wiring must remain intact, and the existing seeded operator accounts and their documented local-only password must remain unchanged. The change must also account for developers who already have persisted local SQL or Keycloak state created under the current explicit password setup.

## Plan approach

- **Delivery model**: `small scoped behavior change`
- **Branching**: keep the work on `003-authentication-and-authorisation` and deliver the change as one coordinated slice because AppHost composition, local validation guidance, and persisted-state instructions must stay aligned.
- **Dependencies**:
  - `src/TNC.Trading.Platform.AppHost/`
  - `docs/wiki/local-development.md`
  - `docs/wiki/architecture.md`
  - authentication validation coverage already present in `test/`
- **Behavior-preservation boundaries**:
  - Local development must still use Keycloak running in an Aspire-orchestrated container.
  - The Web and API projects must still start with the same local authentication topology and seeded realm import.
  - The seeded local operator accounts and the local-only seeded-user password `LocalAuth!123` must remain unchanged unless explicitly requested later.
  - SQL-backed local persistence must remain the supported runtime path.
  - The change must remove the password prompts for normal local startup without introducing new checked-in secrets.
- **Key risks**:
  - Existing local SQL or Keycloak state may have been initialized under the current explicit-password configuration.
    - **Mitigation**: define and document a clean reset path for persisted local state before switching developers to the new default-password flow.
  - Removing explicit password parameters could accidentally break local startup if any test or documentation still assumes the named parameters exist.
    - **Mitigation**: inventory current references before removing the parameters and update affected documentation in the same change.
  - The distinction between infrastructure admin credentials and the seeded local operator sign-in password could become unclear.
    - **Mitigation**: update local guidance to distinguish the AppHost-managed infrastructure passwords from the fixed seeded Keycloak user password used only for local validation.

## Plan

### Execution gates (required)

Before starting *any* work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (requirements) | Traceability (spec sections) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Prepare the credential-transition safety net | Confirm the current AppHost password-parameter usage, verify whether any product code, tests, or guidance still depend on the explicit parameter names, and define the reset guidance for developers with existing persisted local SQL or Keycloak state. | `NF3`, `SR3`, `IR1`, `TR3`, `OR1`, `OR2` | §3.3, §4 (`NF3`, `SR3`, `IR1`, `TR3`, `OR1`, `OR2`), §5.3 step 1 and step 9, §5.5, §9 | Baseline item; should complete before AppHost composition changes so reset and documentation guidance is based on confirmed current behavior. | `dotnet build`; `dotnet test`; workspace search for parameter-name dependencies; draft reset guidance review | Revert any exploratory documentation updates if they prove inconsistent with the current AppHost behavior before code changes begin. | Do not ask developers to delete local state until the reset rules are confirmed for both SQL and Keycloak resources. |
| Work Item 2: Adopt Aspire-managed default passwords for local resources | Remove the explicit SQL Server and Keycloak password parameters from AppHost composition, let Aspire manage the default local passwords, preserve the stable local Keycloak port and realm import, and keep the existing Web/API environment wiring intact. | `NF3`, `SR3`, `IR1`, `OR1`, `TR3` | §3.1, §3.3, §4 (`NF3`, `SR3`, `IR1`, `OR1`, `TR3`), §5.3 step 1, §5.5, §6, §9 phase 1 | Depends on Work Item 1 so the persisted-state and dependency assumptions are known before the AppHost change lands. | `dotnet build`; `dotnet test`; manual AppHost startup; verify the Aspire Dashboard no longer prompts for SQL Server or Keycloak passwords; verify seeded Keycloak sign-in still works | Restore the explicit password parameters and prior AppHost resource wiring if local startup or persisted-state transition proves unstable. | If local resources were previously initialized with the old explicit password setup, follow the documented reset steps before validating the new behavior. |
| Work Item 3: Align local-development and architecture guidance with the new startup behavior | Update the relevant `docs/wiki/` pages and work-package guidance so fresh-clone local startup no longer includes manual SQL or Keycloak password entry, and clearly document any required reset steps for previously persisted local state. | `NF3`, `NF5`, `SR3`, `TR3`, `OR2` | §4 (`NF3`, `NF5`, `SR3`, `TR3`, `OR2`), §5.3 step 9, §5.5, §8, §9 phase 3 | Depends on Work Items 1-2 so the documentation reflects the final AppHost behavior and reset guidance. | `dotnet build`; `dotnet test`; wiki link verification; manual local walkthrough using the updated instructions | Revert documentation changes together with the AppHost change if the final runtime behavior differs from the updated guidance. | Validate the updated walkthrough from a clean local environment and confirm it still distinguishes the seeded user password from AppHost-managed infrastructure credentials. |

### Work Item 1 details

- [x] Work Item 1: Prepare the credential-transition safety net
  - [x] Build and test baseline established
  - [x] Task 1: Confirm the current dependency surface
    - [x] Step 1: Inventory every AppHost reference to `sql-password`, `keycloak-admin-password`, and any related onboarding or test guidance.
    - [x] Step 2: Confirm whether any tests, local tooling, or documentation still assume those parameter names exist.
    - [x] Step 3: Record the behavior that must remain unchanged after the AppHost change, including Keycloak realm import, seeded users, stable local auth wiring, and SQL-backed persistence.
  - [ ] Task 2: Define the local-state transition rules
    - [x] Step 1: Identify what existing SQL Server data volumes, persistent containers, and local secret state may need to be reset when moving from explicit passwords to Aspire-managed defaults.
    - [x] Step 2: Define the minimum reset instructions needed for developers who have already run the current AppHost configuration.
    - [x] Step 3: Confirm that fresh-clone onboarding can proceed without manual password entry once the AppHost change is made.
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: confirm the exact current password-parameter usage and affected resource wiring.
    - `docs/wiki/local-development.md`: prepare for updated fresh-clone and reset guidance.
    - `docs/wiki/architecture.md`: prepare for any topology guidance updates that mention local infrastructure startup.
  - **Work Item Dependencies**: Must complete before the AppHost resource change so reset and compatibility assumptions are explicit.
  - **User Instructions**: Keep any existing local SQL and Keycloak state intact until the reset guidance is finalized.

### Work Item 2 details

- [x] Work Item 2: Adopt Aspire-managed default passwords for local resources
  - [x] Build and test baseline established
  - [x] Task 1: Update the AppHost resource composition
    - [x] Step 1: Remove the explicit SQL Server password parameter from `ConfigureInfrastructureResources` and let Aspire manage the SQL password lifecycle.
    - [x] Step 2: Remove the explicit Keycloak admin password parameter from `ConfigureInfrastructureResources` and let Aspire manage the Keycloak admin password lifecycle.
    - [x] Step 3: Reassess whether the explicit Keycloak admin username parameter is still needed once password handling is delegated to Aspire, keeping it only if it still provides a real local-development benefit.
  - [ ] Task 2: Preserve the delivered local auth behavior
    - [x] Step 1: Keep the stable local Keycloak port, realm import, and Web/API environment wiring unchanged unless a direct compatibility issue requires an adjustment.
    - [x] Step 2: Confirm the seeded local operator users and their shared local-only sign-in password remain unchanged.
    - [x] Step 3: Verify the Aspire Dashboard no longer prompts for SQL Server or Keycloak passwords during normal local startup.
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: remove explicit password parameters and preserve the existing resource topology.
    - `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`: confirm no package or secret-store change is required beyond the AppHost composition update.
    - affected authentication test projects under `test/`: adjust only if any coverage currently depends on the explicit parameter names or old startup assumptions.
  - **Work Item Dependencies**: Depends on Work Item 1 dependency and reset analysis.
  - **User Instructions**: If the first validation run uses previously persisted local resources, apply the documented reset steps before retrying AppHost startup.

### Work Item 3 details

- [x] Work Item 3: Align local-development and architecture guidance with the new startup behavior
  - [x] Build and test baseline established
  - [x] Task 1: Update developer guidance
    - [x] Step 1: Update local-development guidance to state that new developers should no longer be prompted for SQL Server or Keycloak passwords on initial AppHost startup.
    - [x] Step 2: Document the reset path for developers who already have local persisted SQL or Keycloak state from the old explicit-password configuration.
    - [x] Step 3: Clarify that the seeded Keycloak user password `LocalAuth!123` is for local sign-in validation only and is separate from Aspire-managed infrastructure admin credentials.
  - [ ] Task 2: Update architecture and work-package guidance
    - [x] Step 1: Update any affected architecture or work-package documentation that describes how the local auth infrastructure is started.
    - [x] Step 2: Verify affected `docs/wiki/` links still resolve after the updates.
    - [x] Step 3: Confirm the final guidance matches the actual fresh-clone experience in the Aspire Dashboard.
  - [x] Build and test validation

  - **Files**:
    - `docs/wiki/local-development.md`: update first-run and reset guidance.
    - `docs/wiki/architecture.md`: update local infrastructure composition notes if needed.
    - `docs/003-authentication-and-authorisation/`: update any work-package notes that describe local startup expectations.
  - **Work Item Dependencies**: Depends on the final AppHost behavior from Work Item 2.
  - **User Instructions**: Validate the updated onboarding steps from a clean local environment before considering the plan complete.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Tests**: `dotnet test`
- **Manual checks**:
  - Start the AppHost from a clean local environment and confirm the Aspire Dashboard no longer prompts for SQL Server or Keycloak passwords.
  - Verify SQL Server, Keycloak, the Web UI, and the API all start successfully.
  - Verify sign-in still works with the seeded `local-admin`, `local-operator`, `local-viewer`, and `local-norole` users.
  - Verify any required reset guidance for previously persisted local state works as documented.
  - Verify affected `docs/wiki/` links resolve after documentation updates.
- **Behavior-preservation checks**:
  - Local development still uses Keycloak orchestrated by Aspire.
  - The Web and API still use the same local authentication flow and seeded realm import.
  - The seeded local user password `LocalAuth!123` remains unchanged unless explicitly requested in later work.
  - SQL-backed local persistence remains the supported runtime path.
- **Security checks**:
  - Confirm `sql-password` and `keycloak-admin-password` are no longer required inputs for normal local AppHost startup.
  - Confirm no new secret values are checked into source control.
  - Confirm documentation clearly separates AppHost-managed infrastructure credentials from the seeded local operator sign-in credentials.

## Acceptance checklist

- [x] The plan removes the fresh-clone Aspire Dashboard password prompts for local SQL Server and Keycloak startup.
- [x] The plan preserves the delivered Keycloak-backed local authentication behavior.
- [x] The plan accounts for developers with previously persisted local SQL or Keycloak state.
- [x] Relevant `docs/wiki/` pages are updated to reflect the final local startup behavior.
- [x] Affected wiki links resolve after documentation updates.
- [ ] Rollback/backout guidance is documented for the AppHost change.

## Notes

- This plan is intentionally narrow: it changes how local infrastructure passwords are provisioned, not the delivered authentication and authorization feature set.
- The plan assumes Aspire-managed default passwords will remain stable across local runs through the AppHost secret store once the resources are initialized in run mode.
- The plan treats the seeded local Keycloak user password `LocalAuth!123` as a separate local validation credential and not as an infrastructure admin password.
- Because the current AppHost uses a SQL data volume and persistent local infrastructure, the first-run experience for existing developers may require cleanup steps even though fresh clones should no longer see the password prompts.
- Repository-wide validation now passes after the auth integration tests were updated to wait for API readiness before issuing protected requests under full-suite load.
