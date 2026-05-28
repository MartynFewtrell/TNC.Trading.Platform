# IG Login Delivery Plan

This plan describes how work package `006-ig-login` will be delivered in incremental, reviewable slices based on `../requirements.md` and `../technical-specification.md`.

## Summary

- **Source**: See `../requirements.md` for canonical work metadata (work item, owner, dates, links) and scope. See `../../business-requirements.md` for project-level business context.
- **Status**: draft
- **Inputs**:
  - `../../business-requirements.md`
  - `../requirements.md`
  - `../technical-specification.md`

## Description of work

Deliver startup-driven `IG` Test authentication, backend-maintained login state, secret-safe persistence of the full non-secret login payload, operator-visible current status on `/status`, retained daily login history, and requirement-traceable validation. The plan explicitly reuses the retry, schedule, degraded-state, and live-environment safeguards defined by `../002-environment-and-auth-foundation/requirements.md` and keeps current-state visibility separate from retained historical review.

## Delivery approach

- **Delivery model**: multiple PRs
- **Branching**: Deliver as sequenced PRs on branch `006-ig-login`, with each PR scoped to one work item and rebased onto the latest branch head before merge/review.
- **Dependencies**:
  - `../002-environment-and-auth-foundation/requirements.md` remains the source of truth for retry, trading-schedule, degraded-state, and live-environment guard behavior.
  - `IG` Test environment access, credentials, and login availability.
  - Existing platform status API/UI, startup coordination flow, SQL Server-backed persistence, and operational-record infrastructure.
  - Existing API, Web, Application, Infrastructure, and test projects in `TNC.Trading.Platform.slnx`.
- **Key risks**:
  - Secret-exposure risk when handling broker responses; mitigate with an explicit allow-list mapper, output inspection, and secret-safe contract tests.
  - State ambiguity risk between current session state and retained payload history; mitigate by delivering `/status` current-state support before history UI and by validating distinct UI labels and contracts.
  - Startup/runtime regression risk in shared auth supervision code; mitigate with sequenced backend-first changes, inherited retry/schedule reuse, and baseline/pre-completion build and test gates.
  - Documentation drift risk; mitigate by updating relevant `docs/wiki/` pages as part of each work item that changes delivered behavior or guidance.

## Delivery Plan

### Execution gates (required)

Before starting *any* work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (requirements) | Traceability (spec sections) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Backend login capture and persistence | Implement secret-safe `IG` login result mapping, latest/daily snapshot persistence, startup-session supervision integration, and current status projection updates for backend state accuracy. | `FR1`, `FR2`, `FR3`, `FR4`, `FR6`, `FR8`, `FR10`, `NF1`, `NF3`, `NF4`, `SR1`, `SR2`, `SR3`, `SR4`, `DR1`, `DR2`, `DR3`, `IR1`, `IR4`, `TR1`, `TR2`, `TR3`, `TR5`, `TR6`, `TR8`, `TR9`, `TR10`, `OR1`, `OR2` | Sections 3.1, 3.3, 4, 5.1, 5.2, 5.3 steps 1-3, 5.4, 5.5, 6, 7, 8, 9 phase 1 | Baseline gates; existing auth foundation behavior; broker adapter and persistence infrastructure | `dotnet build`; `dotnet test`; backend unit/integration coverage for mapping, persistence, startup login, failed login, live guard, retry/schedule inheritance, and secret-safe outputs | Revert schema changes, snapshot persistence, mapper, and supervision integration together; restore prior status projection contract if backend state becomes unstable | Review backend contract changes before merge; provide valid `IG` Test credentials and environment configuration for manual verification if integrated validation is performed |
| Work Item 2: Current status API and `/status` UI | Extend the platform status contract and `/status` page to show current `IG` login state, schedule/retry context, and expandable latest non-secret payload details without adding a separate latest-payload read path. | `FR5`, `FR6`, `FR10`, `NF1`, `NF2`, `NF4`, `NF5`, `SR2`, `SR4`, `IR2`, `IR4`, `TR4`, `TR5`, `TR8`, `TR10`, `OR1` | Sections 2.2, 2.3, 3.1 items 3-4, 3.3, 4, 5.1, 5.3 step 4, 6, 7, 8, 9 phase 2 | Work Item 1 complete; existing status API/UI feature slices; stable latest snapshot projection | `dotnet build`; `dotnet test`; API contract tests for `GET /api/platform/status`; functional/UI tests for state visibility, expandable latest payload details, and failed vs out-of-schedule distinction; manual `/status` review | Revert status response additions and `/status` bindings while keeping backend snapshot persistence intact | Reviewers should validate `/status` labels and layout clearly distinguish current auth state, schedule state, retry state, and latest payload details |
| Work Item 3: Retained history API, UI, and operational records | Add retained daily history retrieval, dedicated history page, and secret-safe operational record extensions for notable login/session transitions and retention processing. | `FR7`, `FR9`, `NF2`, `NF3`, `NF5`, `SR2`, `SR3`, `SR4`, `DR3`, `IR3`, `TR5`, `TR7`, `OR2`, `OR3` | Sections 3.1 items 3-4, 3.3, 4, 5.1, 5.2, 5.3 steps 5-6, 5.4, 6, 7, 8, 9 phase 3 | Work Items 1-2 complete; retained snapshot data available; existing operational-record retention pipeline | `dotnet build`; `dotnet test`; integration tests for retained daily snapshot selection and 90-day cleanup; API tests for history endpoint; functional/UI tests for dedicated history page and separation from current state; inspection of operational records for secret-safe summaries | Revert history endpoint, history page, and operational-record extensions while retaining latest snapshot support from earlier work items | Reviewers should confirm history shows retained daily first-successful payloads only and does not duplicate the current-state latest payload UX |
| Work Item 4: Test completion, documentation, and wiki alignment | Complete remaining requirement-traceable tests, finalize documentation updates, and align repository wiki guidance for implemented behavior, operator use, local validation, and troubleshooting. | `TR1`-`TR10`, `NF1`-`NF5`, `SR1`-`SR4`, `OR1`-`OR3` | Sections 4, 5.3 steps 7-8, 6, 7, 8, 9 phase 4 | Work Items 1-3 complete; final API/UI behavior stable enough for documentation | `dotnet build`; `dotnet test`; targeted review of test comments for requirement traceability; documentation link checks; manual verification that wiki guidance matches delivered behavior and validation approach | Revert only documentation and nonessential test additions if they block completion, but do not mark the plan complete until docs/wiki alignment is restored | Reviewers should inspect requirement coverage, operator guidance, and local validation steps before sign-off |

### Work Item 1 details

- [ ] Work Item 1: Backend login capture and persistence
  - [ ] Build and test baseline established
  - [ ] Task 1: Define secret-safe `IG` login snapshot contracts and mapping
    - [ ] Step 1: Introduce application-owned models for the latest snapshot, retained daily snapshot, and status projection updates.
    - [ ] Step 2: Implement an explicit allow-list mapper for approved non-secret `IG` login response fields and reject or ignore protected fields.
    - [ ] Step 3: Add unit tests that verify full non-secret payload capture and exclusion of credentials, tokens, and equivalent secrets.
  - [ ] Task 2: Add persistence and retention support for latest and daily snapshots
    - [ ] Step 1: Add or update infrastructure persistence/schema support for `IgLoginSnapshot`, retained daily history, and related read models.
    - [ ] Step 2: Ensure only one first-successful snapshot per trading day is retained while the latest successful snapshot remains independently addressable.
    - [ ] Step 3: Add retention processing for removal of retained daily snapshots older than 90 days.
  - [ ] Task 3: Integrate startup login and runtime supervision with inherited behavior from work package `002`
    - [ ] Step 1: Extend startup coordination and broker auth workflow to capture successful login results during permitted schedule windows.
    - [ ] Step 2: Reuse inherited retry, degraded-state, schedule, and live-environment guard behavior rather than redefining it locally.
    - [ ] Step 3: Update the backend status projection so failed, retrying, active, signed-out, and out-of-schedule states remain accurate over time.
  - [ ] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [ ] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs`: extend startup/session supervision to capture and project `IG` login state.
    - `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/*`: update application status contracts and handlers for backend login projection support.
    - `src/TNC.Trading.Platform.Infrastructure/*`: add sanitized mapping, persistence, and retention support for login snapshots.
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs`: align retained snapshot cleanup if shared retention infrastructure is reused.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/*`: add mapping and state-transition unit tests.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*`: add backend login lifecycle integration coverage.
  - **Work Item Dependencies**: Must land before any UI work because it establishes the backend source of truth consumed by later API/UI slices.
  - **User Instructions**: Validate that backend behavior remains demo/Test-only where required and confirm any schema migration/recreation step used locally before running end-to-end checks.

### Work Item 2 details

- [ ] Work Item 2: Current status API and `/status` UI
  - [ ] Build and test baseline established
  - [ ] Task 1: Extend the status API contract for current `IG` state and latest payload details
    - [ ] Step 1: Add latest snapshot fields, auth state detail, schedule state, and retry context to the application and API `GetPlatformStatus` contracts.
    - [ ] Step 2: Keep the latest full non-secret payload inside the existing status response so the UI can expand details without another read call.
    - [ ] Step 3: Add contract and integration tests to verify the status endpoint remains secret-safe and accurately reflects backend-maintained state.
  - [ ] Task 2: Update `/status` to present current state and expandable latest payload details
    - [ ] Step 1: Update `Status.razor` and related UI models/components to display current login state, environment, schedule context, and retry context.
    - [ ] Step 2: Add an expandable details area for the latest non-secret payload and clearly label it as current latest successful login information.
    - [ ] Step 3: Add functional/UI tests to confirm operators can distinguish failed, retrying, active, and out-of-schedule states without ambiguity.
  - [ ] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [ ] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/*`: extend application status query/response models.
    - `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/*`: extend API response models and mappings.
    - `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`: surface current `IG` status and expandable latest payload details.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*`: add status endpoint coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/*`: add `/status` functional checks.
  - **Work Item Dependencies**: Depends on Work Item 1 backend projection and persisted latest snapshot support.
  - **User Instructions**: Review `/status` for readability and confirm the latest payload details are operator-friendly without exposing protected values.

### Work Item 3 details

- [ ] Work Item 3: Retained history API, UI, and operational records
  - [ ] Build and test baseline established
  - [ ] Task 1: Add retained history query and endpoint
    - [ ] Step 1: Implement the `GetIgLoginHistory` application slice and API endpoint for retained daily first-successful payloads.
    - [ ] Step 2: Ensure returned history distinguishes retained daily payloads from the latest successful payload used on `/status`.
    - [ ] Step 3: Add integration tests for ordering, retention-window filtering, and secret-safe response shape.
  - [ ] Task 2: Add dedicated `IG` login history UI
    - [ ] Step 1: Create the history page and navigation entry in the Blazor UI.
    - [ ] Step 2: Render retained daily payload summaries and details in a format that is clearly historical rather than current-state status.
    - [ ] Step 3: Add functional/UI tests for page load, empty-state behavior, and retained-history display.
  - [ ] Task 3: Extend secret-safe operational records for login/session transitions
    - [ ] Step 1: Record startup login attempts, success/failure transitions, invalidation, recovery, and out-of-schedule transitions using existing operational-record patterns.
    - [ ] Step 2: Verify logs and operational records contain summary context only and no protected values.
    - [ ] Step 3: Align retention processing for retained daily snapshots and related review records as needed.
  - [ ] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [ ] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Application/Features/GetIgLoginHistory/*`: new application query slice for retained history.
    - `src/TNC.Trading.Platform.Api/*`: new history endpoint and mappings.
    - `src/TNC.Trading.Platform.Web/*`: new dedicated history page and related navigation/view models.
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs`: update retention behavior if shared processing is used.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*`: add history and operational-record coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/*`: add dedicated history page coverage.
  - **Work Item Dependencies**: Depends on Work Item 1 persistence and Work Item 2 current-state/status foundations.
  - **User Instructions**: Confirm the history view is only for retained daily payload review and does not become the source of truth for current auth state.

### Work Item 4 details

- [ ] Work Item 4: Test completion, documentation, and wiki alignment
  - [ ] Build and test baseline established
  - [ ] Task 1: Complete requirement-traceable automated test coverage
    - [ ] Step 1: Fill any remaining unit, integration, and functional test gaps across `TR1`-`TR10`.
    - [ ] Step 2: Ensure test names follow repository conventions and add comments capturing requirement traceability, expected outcomes, and behavioral importance.
    - [ ] Step 3: Re-run full validation and resolve any regressions introduced by earlier slices.
  - [ ] Task 2: Finalize work-package and wiki documentation
    - [ ] Step 1: Update `docs/006-ig-login/technical-specification.md` and this delivery plan if implementation details materially changed during delivery.
    - [ ] Step 2: Create or update relevant `docs/wiki/` pages for architecture, API/status behavior, operator workflow, local validation guidance, and troubleshooting for `IG` login.
    - [ ] Step 3: Verify updated wiki links resolve and that documentation clearly distinguishes current status, latest payload details, and retained daily history.
  - [ ] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [ ] Build and test validation

  - **Files**:
    - `docs/006-ig-login/technical-specification.md`: align any implementation-driven adjustments.
    - `docs/006-ig-login/plans/001-delivery-plan.md`: keep final delivery guidance accurate if sequencing or validation changes.
    - `docs/wiki/*`: create or update relevant implementation documentation pages for `IG` login behavior and operator guidance.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/*`: finalize remaining unit coverage.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*`: finalize remaining integration coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/*`: finalize remaining functional coverage.
  - **Work Item Dependencies**: Final work item after the feature behavior and UI surfaces are stable enough to document and sign off.
  - **User Instructions**: Do not consider the work package complete until automated validation passes and the affected `docs/wiki/` guidance has been updated and link-checked.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test`
- **Integration tests**: `dotnet test`
- **Manual checks**:
  - Verify backend startup produces the expected `IG` Test login state during an in-schedule period.
  - Verify `/status` shows current auth state, environment, schedule state, retry context, and expandable latest non-secret payload details.
  - Verify the dedicated history page shows retained daily first-successful payloads only and clearly distinguishes them from current state.
  - Verify failed login and out-of-schedule states are distinct in the UI.
- **Security checks**:
  - Inspect persisted records, API responses, UI output, logs, and operational records to confirm credentials, session tokens, and equivalent protected values are absent.
  - Confirm blocked live-environment behavior still applies in constrained/Test environments.
  - Confirm retained-history cleanup preserves the 90-day window without exposing deleted or protected content.

## Acceptance checklist

- [ ] Work item aligns with `../business-requirements.md`.
- [ ] All referenced `FRx` requirements are implemented and validated.
- [ ] All referenced `NFx` requirements have measurements or checks.
- [ ] All referenced `SRx` security requirements are implemented and validated.
- [ ] Relevant `docs/wiki/` pages are updated to reflect the delivered implementation.
- [ ] Affected wiki links resolve after documentation updates.
- [ ] Rollback/backout plan documented for each work item.

## Notes

- This plan is derived primarily from `../requirements.md` and `../technical-specification.md`, with project-level context from `../../business-requirements.md` and `../../systems-analysis.md`.
- No existing `docs/wiki/` pages were discovered during planning; create or update the relevant wiki pages before marking the plan complete if they do not already exist at implementation time.
- Validation defaults to repo-root `dotnet build` and `dotnet test` as required by repository guidance.
