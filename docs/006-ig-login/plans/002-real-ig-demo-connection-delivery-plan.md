# IG Real Demo Connection Delivery Plan

> This plan uses the delivery-plan template and supersedes the foundation-only rollout described in `plans/001-delivery-plan.md` for the clarified work package objective: establish a real connection to the IG Demo environment and prove that the platform can pull back real read-only demo data safely.

## Summary

- **Source**: See `../requirements.md` for canonical work metadata and the existing work-package scope baseline. See `../../business-requirements.md` and `../../systems-analysis.md` for project-level business context and system use cases.
- **Status**: complete
- **Inputs**:
  - `../../business-requirements.md`
  - `../../systems-analysis.md`
  - `../requirements.md`
  - `../technical-specification.md`
  - `plans/001-delivery-plan.md`
  - `https://labs.ig.com/rest-trading-api-guide.html`

## Description of work

Deliver a real IG Demo integration for the trading application by replacing the current simulated broker-auth behavior with an application-owned IG REST client, establishing a real demo session during the existing startup-driven auth flow, maintaining that session through the inherited retry and trading-schedule rules, and proving the connection with a safe read-only demo-data retrieval path.

The implementation scope of this plan is intentionally limited to:

- real authenticated connectivity to `https://demo-api.ig.com/gateway/deal`
- secure handling of the IG API key, identifier, and password through the existing protected configuration path
- secret-safe capture of the non-secret login/session payload already expected by work package `006`
- one or more read-only demo-account queries that prove the platform can pull back real data from IG without placing or amending trades
- operator-visible status and troubleshooting guidance in the existing API and Blazor surfaces

This plan does **not** include live-environment enablement, order placement, instrument-management workflows, or streaming market data subscriptions. Read-only proof data should stay within low-risk authenticated reads such as account/session context and open-position snapshots so the work package proves real connectivity without expanding into trade execution.

## Delivery approach

- **Delivery model**: multiple PRs
- **Branching**: deliver as sequenced PRs or reviewable commits on branch `006-ig-login`, keeping outbound broker integration, runtime supervision changes, UI changes, and validation/docs updates separately reviewable.
- **Dependencies**:
  - `IG` demo account access, API key, and network connectivity
  - existing protected credential storage in SQL Server via the Blazor configuration workflow
  - existing runtime coordination in `PlatformStateCoordinator` and `PlatformAuthSupervisor`
  - existing status and history API/UI surfaces in the API and Blazor projects
  - existing auth foundation behavior from `../002-environment-and-auth-foundation/requirements.md`
- **Key risks**:
  - secret-exposure risk when moving from simulated payloads to real broker responses; mitigate with explicit allow-list mapping, redaction tests, and inspection of logs/persistence/API/UI outputs.
  - external-dependency risk because IG is outside the repo and can reject, throttle, or delay requests; mitigate with deterministic automated tests using fake handlers plus an opt-in/manual real-IG verification step.
  - startup-regression risk because broker login now becomes real I/O in the background supervision path; mitigate with tight timeout/error classification, inherited retry behavior, and incremental rollout before UI changes.
  - quota/rate-limit risk; mitigate by limiting this work package to login plus low-frequency read-only proof calls, recording throttling events, and avoiding any polling loop that is not required by the clarified objective.
  - traceability drift risk because the current work-package documents describe a foundation-only outcome; mitigate by updating the work-package requirements/specification and affected wiki pages as part of the first work item before implementation is considered complete.

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
| Work Item 1: Clarify scope and add a real IG client foundation | Align the work-package docs to the clarified objective, define the approved real-data proof scope, introduce the application/infrastructure contracts for IG REST auth and read-only queries, and register a typed outbound client with explicit Demo-vs-Live base URL guards. | `BR1`, `BR2`, `BR3`, `BR11`, `BR12`, `UC2`, `UC4`, `UC9`, `AD3`, `RULE4`, existing `FR1`-`FR10`, `NF1`-`NF5`, `SR1`-`SR4` | Current sections 2.1-2.3, 3.1, 3.3, 4, 5.1, 5.3, 5.5, 6-9, updated in this work item to replace the simulated/foundation-only assumptions with real Demo connectivity and read-only proof data | Baseline gates; IG REST guidance; existing configuration and DI structure | `dotnet build`; `dotnet test`; unit tests for request construction, base-URL selection, header shaping, redaction, and error classification; documentation review for explicit scope alignment | Revert requirements/spec changes and new client-registration code together if the clarified objective or client shape is found to be wrong before runtime integration begins | Confirm that the proof-of-connection data set stays read-only and demo-only before approving this work item |
| Work Item 2: Replace simulated auth with real Demo session supervision | Replace the simulated login path in the runtime coordinator with a real IG Demo session flow, keep tokens out of persisted/operator-facing models, and preserve existing schedule, retry, degraded-state, and blocked-live safeguards. | `BR1`, `BR2`, `BR12`, `UC2`, `UC9`, `AD3`, existing `FR1`, `FR2`, `FR3`, `FR4`, `FR6`, `FR7`, `FR8`, `FR10`, `NF1`, `NF3`, `NF4`, `SR1`, `SR2`, `SR3`, `SR4`, `DR1`, `DR2`, `DR3`, `IR1`, `IR4`, `TR1`, `TR2`, `TR3`, `TR5`, `TR6`, `TR8`, `TR9`, `TR10`, `OR1`, `OR2` | Updated sections 3.1 items 1-3, 3.3, 4, 5.1, 5.2, 5.3 steps 1-4, 5.4, 5.5, 6-9 | Work Item 1 complete; existing protected credential workflow; existing runtime stores and event pipeline | `dotnet build`; `dotnet test`; unit/integration tests with fake IG responses for success, invalid credentials, rejected auth, timeout, blocked live path, retry reuse, and secret-safe persistence; manual/opt-in Demo login smoke using real credentials | Revert runtime coordinator integration, typed client usage, and any ephemeral token/session storage changes; if needed temporarily restore the simulated adapter while preserving unaffected status/UI code | Provide valid Demo credentials through the existing configuration UI before attempting the manual smoke verification |
| Work Item 3: Add read-only real-data retrieval and operator-visible proof of connection | After a successful Demo login, issue a minimal authenticated read-only data probe, persist the last successful proof data needed for operator review, and surface it through the existing status API/UI without introducing trade placement. | `BR2`, `BR3`, `BR11`, `BR12`, `UC2`, `UC4`, `UC9`, `AD3`, `RULE4`, existing `FR3`, `FR5`, `FR6`, `FR7`, `FR9`, `FR10`, `NF1`, `NF2`, `NF3`, `NF4`, `NF5`, `SR2`, `SR3`, `SR4`, `IR2`, `IR3`, `IR4`, `TR4`, `TR5`, `TR7`, `TR8`, `OR1`, `OR2`, `OR3` | Updated sections 3.1 items 3-4, 3.3 data flows, 4, 5.1, 5.2, 5.3 steps 4-6, 5.4, 6-9 | Work Item 2 complete; authenticated session working; agreed proof-data shape from Work Item 1 | `dotnet build`; `dotnet test`; API/Web tests for current-state display of real account/session proof data, empty-result handling, stale-failure handling, and continued secret-safety; manual status-page verification against a real Demo account | Revert proof-data query orchestration and UI/API contract additions while leaving the real auth flow intact if necessary | Reviewers should verify that the surfaced proof data is clearly marked read-only, current, and sourced from IG Demo rather than simulated local data |
| Work Item 4: Harden resilience, validation, and documentation for the real connection | Add throttling/error instrumentation, optional real-IG smoke coverage, final wiki updates, and operator/local-dev guidance so the new connection is supportable and reviewable. | `BR2`, `BR3`, `BR11`, `BR12`, `UC2`, `UC4`, `UC9`, `UC10`, `AD3`, `RULE4`, existing `NF1`-`NF5`, `SR1`-`SR4`, `TR1`-`TR10`, `OR1`-`OR3` | Updated sections 5.4, 5.5, 6, 7, 8, 9 and affected wiki/API/local-development/testing documentation | Work Items 1-3 complete; behavior stable enough to document | `dotnet build`; `dotnet test`; opt-in real-IG smoke execution when credentials are present; documentation link checks; manual verification of troubleshooting and rotation guidance | Revert only the new smoke-test harness and documentation updates if they cause issues, but do not mark the feature complete until the wiki and run guidance are correct again | Validate the documented local runbook against a real Demo login before considering the work package complete |

### Work Item 1 details

- [x] Work Item 1: Clarify scope and add a real IG client foundation
  - [x] Build and test baseline established
  - [x] Task 1: Align work-package documentation to the clarified objective
    - [x] Step 1: Update `../requirements.md` so the work package explicitly targets real IG Demo connectivity and a read-only real-data proof path instead of a simulation-only outcome.
    - [x] Step 2: Update `../technical-specification.md` so the proposed solution names the real IG REST client, read-only proof-data scope, and deterministic-vs-real validation strategy.
    - [x] Step 3: Record in the plan and spec which proof data will be retrieved in this package and which later broker capabilities remain out of scope.
  - [x] Task 2: Introduce explicit IG REST client contracts and environment guards
    - [x] Step 1: Expand `src/TNC.Trading.Platform.Application/Infrastructure/Ig/*` with request/response models for real session establishment and the chosen read-only proof query or queries.
    - [x] Step 2: Add an infrastructure adapter and typed `HttpClient` registration under `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/*` or a dedicated IG integration folder.
    - [x] Step 3: Resolve Demo and Live base URLs centrally, but keep live calls blocked by the existing platform-environment safeguard.
  - [x] Task 3: Define secret-safe outbound and inbound handling rules
    - [x] Step 1: Keep decrypted credentials and broker tokens inside infrastructure/runtime flow only and out of API/UI/persistence contracts.
    - [x] Step 2: Extend sanitization rules so `CST`, `X-SECURITY-TOKEN`, `Authorization`, API keys, and equivalent values cannot leak.
    - [x] Step 3: Add unit tests for request headers, versioning, JSON body shape, redaction, and error-code classification.
  - [x] Relevant `docs/wiki/` pages updated to reflect the implemented changes — no observable operator-facing behavior changed in WI1; wiki updates deferred to WI2 when the real session is live.
  - [x] Build and test validation — build clean; 193/193 unit tests pass (21 new tests added).

  - **Files**:
    - `docs/006-ig-login/requirements.md`: align the work package scope to the clarified real-connection objective.
    - `docs/006-ig-login/technical-specification.md`: replace simulated assumptions with real IG client and proof-data design.
    - `docs/006-ig-login/plans/002-real-ig-demo-connection-delivery-plan.md`: keep this plan current if sequencing changes.
    - `src/TNC.Trading.Platform.Application/Infrastructure/Ig/*`: request/response contracts and sanitization helpers.
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`: DI registration for the IG client.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/*` and `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/*`: low-cost contract and redaction tests.
  - **Work Item Dependencies**: First work item because later code changes need a stable, explicitly documented contract and proof-data boundary.
  - **User Instructions**: Review the updated requirements/specification wording before implementation proceeds to ensure the chosen proof data is sufficient.

### Work Item 2 details

- [x] Work Item 2: Replace simulated auth with real Demo session supervision
  - [x] Build and test baseline established
  - [x] Task 1: Integrate the real IG login flow into backend supervision
    - [x] Step 1: Replace the simulated `IgAuthenticateResponse` creation in `PlatformStateCoordinator` with a real call to the IG Demo session endpoint.
    - [x] Step 2: Keep inherited schedule gating, retry timing, degraded transitions, and blocked-live behavior as the single source of truth.
    - [x] Step 3: Persist only approved non-secret session/account metadata while retaining runtime knowledge needed to reuse the active session safely.
  - [x] Task 2: Handle failure and recovery paths safely
    - [x] Step 1: Classify invalid credentials, rejected logins, expired sessions, throttling, and unreachable-IG failures into the existing runtime-state model.
    - [x] Step 2: Record redacted operational events with enough detail for troubleshooting without exposing protected values.
    - [x] Step 3: Ensure stale success is cleared promptly when IG rejects or invalidates the session.
  - [x] Task 3: Add deterministic automated coverage plus a real Demo verification path
    - [x] Step 1: Add integration coverage using fake or recorded IG responses so repository-default `dotnet test` remains deterministic.
    - [x] Step 2: Add an opt-in or explicitly documented real-IG smoke path guarded by user secrets or environment variables.
    - [x] Step 3: Validate that startup login, runtime recovery, and blocked-live rules still behave correctly with the new outbound dependency.
  - [x] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [x] Build and test validation — build clean; 201/201 unit tests pass (8 new tests added)

  - **Files**:
    - `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs`: replace simulated login orchestration with real broker integration.
    - `src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs`: preserve scheduler-driven supervision behavior.
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs`: extend with safe read access for runtime use if needed.
    - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/*Ig*`: real auth client, token/session handling, and event mapping.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/*`: runtime-state and redaction tests.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*`: backend auth-lifecycle contract tests.
  - **Work Item Dependencies**: Depends on Work Item 1 contracts and documentation alignment.
  - **User Instructions**: Provide Demo credentials only through the existing `/configuration` flow; do not add secrets to source-controlled config files.

### Work Item 3 details

- [x] Work Item 3: Add read-only real-data retrieval and operator-visible proof of connection
  - [x] Build and test baseline established
  - [x] Task 1: Add the authenticated read-only proof-data query path
    - [x] Step 1: Implement one or more low-risk IG read queries that prove the platform can retrieve real Demo data after login.
    - [x] Step 2: Keep the proof query frequency low and tied to meaningful runtime events so the package stays within safe quota usage.
    - [x] Step 3: Persist the last successful proof result in an application-owned, secret-safe model if it must survive process restarts or UI reconnects.
  - [x] Task 2: Expose proof data through existing operator-facing surfaces
    - [x] Step 1: Extend the platform status contract and related mappings with the new proof-data projection.
    - [x] Step 2: Update the Blazor status page to show whether the displayed data came from a real IG Demo read and when it was last refreshed.
    - [x] Step 3: Preserve clear distinction between current runtime state, retained login history, and the new read-only proof data.
  - [x] Task 3: Validate empty, degraded, and recovered behaviors
    - [x] Step 1: Cover empty positions or empty account-data scenarios without treating them as failures.
    - [x] Step 2: Cover proof-query failures so the UI shows degraded or unavailable proof data without falsely implying the session is healthy.
    - [x] Step 3: Confirm recovery updates the status surface after IG becomes reachable again.
  - [x] Relevant `docs/wiki/` pages updated to reflect the implemented changes — deferred to Work Item 4 wiki hardening per plan scope boundaries
  - [x] Build and test validation — build clean; 209/209 unit tests pass (8 new tests added)

  - **Files**:
    - `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/*`: status contracts and handler updates.
    - `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/*`: API response and mapping changes.
    - `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`: consume the updated status contract.
    - `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`: render real Demo proof data and timestamps.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*`: status/proof-data API tests.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/*` and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/*`: UI rendering and requirement-driven functional coverage.
  - **Work Item Dependencies**: Depends on Work Item 2 because proof data must be retrieved from a real authenticated session.
  - **User Instructions**: Review the status page after implementation to confirm it shows real Demo data, the refresh timestamp, and the degraded/unavailable states clearly.

### Work Item 4 details

- [x] Work Item 4: Harden resilience, validation, and documentation for the real connection
  - [x] Build and test baseline established
  - [x] Task 1: Add operational hardening for an external broker dependency
    - [x] Step 1: Add timeout, retry-boundary, and throttling instrumentation around outbound IG calls without duplicating the higher-level auth retry policy.
    - [x] Step 2: Record IG rejection or quota-related events in a redacted, operator-reviewable form.
    - [x] Step 3: Confirm the proof-data path does not create an unsafe polling pattern.
  - [x] Task 2: Finalize automated and manual validation guidance
    - [x] Step 1: Document how default deterministic tests differ from the opt-in real-IG smoke verification.
    - [x] Step 2: Add or update test comments so requirement traceability remains explicit.
    - [x] Step 3: Re-run full validation and resolve regressions before sign-off.
  - [x] Task 3: Update wiki and local operator guidance
    - [x] Step 1: Update `docs/wiki/application-overview.md`, `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, and `docs/wiki/api-reference.md` for the real broker connection.
    - [x] Step 2: Update `docs/wiki/operator-guide.md`, `docs/wiki/local-development.md`, and `docs/wiki/testing-and-quality.md` with credential setup, validation, troubleshooting, and quota-aware run guidance.
    - [x] Step 3: Verify affected links resolve and that the docs no longer describe the implementation as simulation-only.
  - [x] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [x] Build and test validation

  - **Files**:
    - `docs/wiki/application-overview.md`: remove the simulation-only limitation after the real connection lands.
    - `docs/wiki/architecture.md`: document the new outbound broker client and runtime token-handling boundary.
    - `docs/wiki/runtime-behavior.md`: update startup and recovery behavior for real IG calls.
    - `docs/wiki/api-reference.md`: document any new or expanded status fields for proof data.
    - `docs/wiki/operator-guide.md`: explain how operators verify a real Demo connection and interpret failures.
    - `docs/wiki/local-development.md`: document credential setup and the real-IG manual smoke path.
    - `docs/wiki/testing-and-quality.md`: document deterministic-vs-real integration coverage.
  - **Work Item Dependencies**: Final work item after the real connection and proof-data surface are stable.
  - **User Instructions**: Do not consider the work package complete until the wiki and local runbook accurately describe the real Demo connection workflow.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test`
- **Integration tests**: `dotnet test`
- **Manual checks**:
  - Start the platform through the AppHost, save valid IG Demo credentials through `/configuration`, and verify the runtime reaches an active Demo-auth state during an in-schedule window.
  - Confirm `/status` shows a real IG Demo login outcome and the selected read-only proof data with a recent refresh time.
  - Confirm invalid or incomplete credentials produce a failed/degraded state without exposing protected values.
  - Confirm blocked-live behavior still prevents unsupported live calls in the Test platform environment.
  - Confirm retained login history remains distinct from the current real-session and proof-data views.
- **Security checks**:
  - Inspect logs, database records, API responses, UI output, and operational events to verify `X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN`, passwords, and equivalent values are absent.
  - Confirm only the Demo base URL is used in Test mode and that live calls remain blocked where required.
  - Confirm proof-data retrieval remains read-only and quota-aware.
  - Confirm opt-in real-IG tests require explicit credential/configuration setup and are not enabled accidentally in normal runs.

## Acceptance checklist

- [x] Work item aligns with `../business-requirements.md`.
- [x] All referenced `FRx` requirements are implemented and validated.
- [x] All referenced `NFx` requirements have measurements or checks.
- [x] All referenced `SRx` security requirements are implemented and validated.
- [x] Relevant `docs/wiki/` pages are updated to reflect the delivered implementation.
- [x] Affected wiki links resolve after documentation updates.
- [x] Rollback/backout plan documented for each work item.

## Notes

- This plan is intentionally repository-aligned: deterministic automated coverage remains the default, while real IG Demo validation is explicit and opt-in because it depends on external credentials and network availability.
- Because `plans/001-delivery-plan.md` and the current work-package documents describe a foundation-only outcome, Work Item 1 updates those documents before code delivery is considered complete.
- The proof-of-connection slice should stay read-only. Any trading, streaming, instrument-discovery, or broader market-data work belongs to later work packages unless the requirements and technical specification are explicitly expanded.
- Wiki maintenance is mandatory for this plan because the delivered behavior, architecture, API surface, runtime behavior, operator workflow, local development guidance, and testing approach will all change.
