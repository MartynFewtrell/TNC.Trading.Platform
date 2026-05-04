# Delivery Plan

> Use this template to plan *when* and *in what increments* the technical specification will be delivered. Store the initial delivery plan as `plans/001-delivery-plan.md`. Each work item should reference relevant `FRx/NFx/SRx/...` and the sections in `../technical-specification.md` that implement them.

## Summary

- **Source**: See `../requirements.md` for canonical work metadata (work item, owner, dates, links) and scope. See `../../business-requirements.md` for project-level business context.
- **Status**: complete
- **Inputs**:
  - `../../business-requirements.md`
  - `../requirements.md`
  - `../technical-specification.md`

## Description of work

Deliver the authentication and authorisation work package for the platform's Blazor Web UI and API in incremental, testable slices. The plan covers local Keycloak composition through Aspire, environment-driven provider configuration, Blazor sign-in/sign-out and access-denied flows, shared role and policy enforcement across UI and API, delegated Web-to-API access, authentication observability and audit coverage, automated test additions, and the required `docs/wiki/` updates so runtime and operator guidance stay aligned with the implementation.

## Delivery approach

- **Delivery model**: single PR
- **Branching**: keep the work on `003-authentication-and-authorisation`, implement the work items in order on that branch, and merge the completed work package as one coordinated PR once the full build, test, documentation, and local validation gates are green.
- **Dependencies**: Aspire AppHost and Keycloak integration for local development; environment-provided identity configuration; Microsoft Entra ID-compatible configuration shape for Azure-aligned environments; shared Web/API authentication libraries; existing service defaults for health/readiness; `docs/wiki/` maintenance.
- **Key risks**: identity-provider misconfiguration blocking sign-in or token validation, mitigated by externalised configuration and early local validation; Web-to-API delegated token propagation complexity, mitigated by delivering the viewer baseline before higher-scope flows; inconsistent role enforcement between UI and API, mitigated by shared policy constants and role-matrix tests; secret or token leakage in logs, mitigated by explicit observability review and fail-closed validation.

## Delivery Plan

### Execution gates (required)

Before starting *any* work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

The final plan may include one or more work items.

| Work item | Description | Traceability (requirements) | Traceability (spec sections) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Establish local auth foundation and sign-in-first entry flows | Add the Aspire-managed Keycloak local identity resource, seed the development realm/users/roles, externalise provider settings, register shared authentication/authorization services, and deliver the Web sign-in-first entry route, sign-in, sign-out, callback, and access-denied flows so local operator authentication can be exercised end-to-end. | FR1, FR2, FR3, FR4, FR7, FR8, FR10, NF1, NF2, NF3, NF5, SR1, SR2, SR3, SR4, IR1, OR1, OR2 | §3.1, §3.3, §4 (FR1, FR2, FR3, FR4, FR7, FR8, FR10), §5.1, §5.2, §5.3 steps 1-3 and 5, §5.4, §5.5, §6, §8, §9 phase 1 | No prior work item dependency; establishes the baseline required by all later work items. | `dotnet build`; `dotnet test`; local AppHost startup; manual sign-in/sign-out checks with seeded users; public surface verification for the entry route and health/readiness endpoints. | Revert AppHost auth resource wiring, realm import, and Web auth registration together; restore the sign-in-first entry flow if the end-to-end sign-in path is unstable. | Review local configuration values before running; validate the seeded `local-admin`, `local-operator`, `local-viewer`, and `local-norole` accounts against the documented local-only password. |
| Work Item 2: Protect baseline UI and API access with viewer-level operator flows | Apply shared authorization policies to protected Blazor routes and baseline protected API endpoints, expose minimal authenticated operator context to the UI, and implement delegated Web-to-API token acquisition and propagation for the baseline `platform.viewer` scope so signed-in viewers can reach the allowed protected surfaces and anonymous callers are challenged or denied correctly. | FR4, FR5, FR6, FR7, FR8, FR9, NF1, NF2, NF4, NF5, SR1, SR2, SR4, IR2, TR1, TR2 | §3.1, §3.3, §4 (FR4, FR5, FR6, FR7, FR8, FR9), §5.1, §5.3 steps 2, 4, 5, and 6, §5.4, §5.5, §6, §7, §8, §9 phase 2 | Depends on Work Item 1 foundation and seeded identity provider setup. | `dotnet build`; `dotnet test`; targeted anonymous-versus-authenticated UI and API checks; manual verification that viewer-level routes succeed after sign-in and protected API endpoints return `401`/`403` correctly. | Revert shared policy application, protected route metadata, and delegated viewer token propagation together; preserve Work Item 1 public and sign-in flows if viewer protection must be backed out. | Validate anonymous and viewer scenarios separately; confirm that health/readiness endpoints remain public while protected features require sign-in. |
| Work Item 3: Add higher-privilege scopes, role boundaries, and denial behavior | Expand the shared authorization model to enforce `Operator` and `Administrator` boundaries consistently across Blazor and API features, request higher delegated scopes only when privileged areas are entered, and harden signed-in denial behavior so insufficient-role users reach the dedicated access-denied experience in the UI and `403 Forbidden` from the API. | FR3, FR5, FR6, FR7, FR9, FR10, NF1, NF2, NF4, NF5, SR1, SR4, IR2, TR2, TR3 | §2.2 assumptions on seeded roles and no-role behavior, §3.1, §3.3, §4 (FR3, FR5, FR6, FR7, FR9, FR10), §5.1, §5.3 steps 2, 4, and 6, §5.4, §5.5, §6, §7, §8, §9 phase 2 | Depends on Work Item 2 viewer baseline and delegated token path. | `dotnet build`; `dotnet test`; role-matrix checks for Administrator, Operator, Viewer, and no-role users; manual verification of access-denied routing and API `403` behavior. | Revert privileged-area policy assignments and higher-scope acquisition changes while retaining the viewer baseline from Work Item 2 if role-boundary behavior is not stable. | Validate each seeded role account independently; confirm no-role users can authenticate but are redirected to the access-denied experience instead of protected features. |
| Work Item 4: Add auth observability, automated coverage, and documentation hardening | Add structured auth outcome logging and audit/event coverage, complete unit/integration/functional/E2E tests for anonymous, authenticated, and role-based scenarios, and update work-package and `docs/wiki/` guidance so local validation, runtime behavior, and testing expectations match the delivered implementation. | FR1, FR3, FR5, FR6, FR7, FR8, FR9, FR10, NF2, NF4, NF5, SR2, SR4, DR1, TR1, TR2, TR3, OR2 | §4 traceability table, §5.3 steps 7-9, §5.4, §6, §7, §8, §9 phase 3 | Depends on Work Items 1-3 because observability, tests, and docs must reflect implemented behavior. | `dotnet build`; `dotnet test`; review logs and audit/event outputs for secret redaction; manual local validation walkthrough covering sign-in, sign-out, denied access, session recovery, and wiki link verification. | Revert auth-specific telemetry, audit/event recording, and newly added tests/docs together if they introduce instability or inaccurate operator guidance. | Re-run the documented local validation checklist after implementation; verify the updated `docs/wiki/` links resolve and reflect the final behavior before considering the work package complete. |

### Work Item 1 details

- [x] Work Item 1: Establish local auth foundation and sign-in-first entry flows
  - [x] Build and test baseline established
  - [x] Task 1: Compose the local identity provider and external configuration baseline
    - [x] Step 1: Add the Aspire Keycloak resource with stable local configuration and realm import support.
    - [x] Step 2: Create the repeatable realm import for the Web client, API client, delegated scopes, roles, and seeded development users.
    - [x] Step 3: Bind environment-driven authentication and authorization settings for local and Azure-aligned provider selection.
  - [x] Task 2: Register shared authentication and authorization services across the Web and API hosts
    - [x] Step 1: Add shared role, policy, claim-mapping, and scheme constants.
    - [x] Step 2: Register OIDC cookie authentication for the Blazor Web app with the `/signin-oidc` callback path.
    - [x] Step 3: Register bearer-token validation for the API without redirect behavior on protected endpoints.
  - [x] Task 3: Deliver entry-route and sign-in lifecycle surfaces in the Blazor Web app
    - [x] Step 1: Implement the sign-in-first UI entry behavior for anonymous users and the signed-in home overview for authenticated operators.
    - [x] Step 2: Add sign-in, sign-out, and access-denied endpoints or pages.
    - [x] Step 3: Confirm no-role users can authenticate and are routed to the access-denied experience.
  - [x] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.AppHost/`: add the local Keycloak resource, realm import wiring, and environment configuration.
    - `src/TNC.Trading.Platform.Web/`: add Web authentication registration plus sign-in, sign-out, landing page, and access-denied behavior.
    - `src/TNC.Trading.Platform.Api/`: add API authentication registration for bearer validation and public endpoint preservation.
    - `docs/003-authentication-and-authorisation/`: update work-package implementation notes as the slice lands.
    - `docs/wiki/`: update local development, architecture, and operator guidance pages affected by the auth foundation.
  - **Work Item Dependencies**: Baseline slice; complete before policy enforcement or delegated token propagation work starts.
  - **User Instructions**: Start the AppHost, verify Keycloak comes up with the imported realm, and validate first-access sign-in, sign-out, and re-authentication with the seeded local accounts before moving to later slices.

### Work Item 2 details

- [x] Work Item 2: Protect baseline UI and API access with viewer-level operator flows
  - [x] Build and test baseline established
  - [x] Task 1: Protect the baseline viewer surfaces consistently across UI and API
    - [x] Step 1: Apply shared viewer-capable policies to protected Blazor routes and components.
    - [x] Step 2: Apply shared viewer-capable policies to the initial protected API endpoints while leaving health/readiness anonymous.
    - [x] Step 3: Verify anonymous users are challenged in the UI and receive `401 Unauthorized` from protected APIs.
  - [x] Task 2: Expose minimal authenticated operator context to the Blazor UI
    - [x] Step 1: Map the minimum required claims into a display-name-first authenticated operator context.
    - [x] Step 2: Surface authenticated navigation and operator-aware landing page content based on the viewer baseline.
    - [x] Step 3: Confirm unauthenticated users do not receive authenticated operator context.
  - [x] Task 3: Implement delegated viewer token acquisition and Web-to-API propagation
    - [x] Step 1: Request the baseline `platform.viewer` delegated scope for signed-in operator API access.
    - [x] Step 2: Acquire and attach viewer tokens for protected Web-to-API calls.
    - [x] Step 3: Validate audience, issuer, and failure behavior for missing or invalid delegated access.
  - [x] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/`: add route protection, authenticated operator context services, and delegated viewer token handling.
    - `src/TNC.Trading.Platform.Api/`: add protected endpoint policy application and bearer validation rules.
    - `test/TNC.Trading.Platform.Web/`: add or extend Web tests for anonymous versus authenticated behavior.
    - `test/TNC.Trading.Platform.Api/`: add or extend API tests for `401`, `403`, and authorized viewer access.
    - `docs/wiki/`: update runtime behavior, API protection, and operator navigation guidance for the viewer baseline.
  - **Work Item Dependencies**: Requires Work Item 1 authentication foundation and working sign-in flow.
  - **User Instructions**: Validate that a signed-in viewer can access only the intended protected surfaces and that anonymous requests still fail closed.

### Work Item 3 details

- [x] Work Item 3: Add higher-privilege scopes, role boundaries, and denial behavior
  - [x] Build and test baseline established
  - [x] Task 1: Enforce the Administrator, Operator, and Viewer role matrix consistently
    - [x] Step 1: Assign feature-level policies to privileged Blazor routes and UI actions.
    - [x] Step 2: Assign the same role boundaries to privileged API endpoints.
    - [x] Step 3: Validate that each seeded role receives only the permitted capability set.
  - [x] Task 2: Add incremental delegated scope acquisition for privileged areas
    - [x] Step 1: Map `platform.operator` and `platform.admin` scopes to the privileged surfaces that require them.
    - [x] Step 2: Trigger interactive acquisition only when the operator enters a privileged area.
    - [x] Step 3: Fail safely when elevated scope acquisition is declined or unavailable.
  - [x] Task 3: Harden signed-in denial and recovery behavior
    - [x] Step 1: Route insufficient-role UI access to the dedicated access-denied page.
    - [x] Step 2: Return `403 Forbidden` from the API for authenticated callers lacking the required role.
    - [x] Step 3: Re-validate session-expiry and re-authentication behavior for protected surfaces.
  - [x] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/`: extend privileged route protection, elevated-scope handling, and access-denied routing.
    - `src/TNC.Trading.Platform.Api/`: extend role-boundary protection for privileged endpoints.
    - `test/TNC.Trading.Platform.Web/`: add role-matrix and denial-behavior functional coverage.
    - `test/TNC.Trading.Platform.Api/`: add role-matrix integration coverage for protected endpoints.
    - `docs/wiki/`: update authorization model, privileged-area behavior, and denied-access guidance.
  - **Work Item Dependencies**: Builds on Work Item 2 viewer-level protection and delegated token plumbing.
  - **User Instructions**: Validate privileged areas with `local-admin`, `local-operator`, `local-viewer`, and `local-norole` to confirm correct access, denial, and re-authentication behavior.

### Work Item 4 details

- [x] Work Item 4: Add auth observability, automated coverage, and documentation hardening
  - [x] Build and test baseline established
  - [x] Task 1: Add secret-safe auth observability and audit/event recording
    - [x] Step 1: Emit structured logs for sign-in, sign-out, failures, token acquisition failures, and access denial without secret leakage.
    - [x] Step 2: Record the required auth audit events with correlation data and retention-aligned behavior.
    - [x] Step 3: Verify that sensitive protocol data is excluded from logs, events, and UI surfaces.
  - [x] Task 2: Complete automated test coverage for the delivered auth model
    - [x] Step 1: Add unit tests for policy registration, claim mapping, and authenticated operator context behavior.
    - [x] Step 2: Add integration and functional tests for anonymous, authenticated, denied, and role-specific scenarios.
    - [x] Step 3: Add Aspire-driven end-to-end coverage where practical for AppHost, Keycloak, Web, and API interaction.
  - [x] Task 3: Harden local validation guidance and implementation documentation
    - [x] Step 1: Update the work-package docs with final local validation and operational guidance.
    - [x] Step 2: Update the relevant `docs/wiki/` pages for architecture, runtime behavior, local development, and testing.
    - [x] Step 3: Verify affected wiki links still resolve after documentation updates.
  - [x] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/`: add or extend auth logging, denial instrumentation, and token-acquisition telemetry.
    - `src/TNC.Trading.Platform.Api/`: add or extend auth logging and access-denial instrumentation.
    - `test/TNC.Trading.Platform.Web/`: complete unit, functional, and E2E coverage for authentication and authorization.
    - `test/TNC.Trading.Platform.Api/`: complete integration coverage for protected API behavior.
    - `docs/003-authentication-and-authorisation/`: update local validation and delivery documentation.
    - `docs/wiki/`: update final implementation guidance and verify navigation/cross-links.
  - **Work Item Dependencies**: Final hardening slice after the core auth flows and role boundaries are implemented.
  - **User Instructions**: Use the updated validation guide to run the full local auth walkthrough and confirm logs, tests, and documentation all reflect the final implementation.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test`
- **Integration tests**: `dotnet test`
- **Manual checks**:
  - Start the AppHost and verify the Web, API, and Keycloak resources start successfully.
  - Verify that `/` redirects to sign-in for signed-out or stale sessions and that health/readiness endpoints remain anonymously accessible.
  - Verify sign-in, sign-out, access-denied, and session-recovery behavior with the seeded local accounts.
  - Verify viewer, operator, administrator, and no-role behavior across protected UI and API surfaces.
  - Verify affected `docs/wiki/` links resolve after documentation updates.
- **Security checks**:
  - Review configuration changes to confirm no authority, client ID, client secret, tenant, or realm secret is hard-coded in product code.
  - Review logs, audit events, and UI output to confirm tokens, secrets, and raw sensitive protocol data are not exposed.
  - Confirm protected API endpoints return `401` or `403` without browser redirects.

## Acceptance checklist

- [x] Work item aligns with `../business-requirements.md`.
- [x] All referenced `FRx` requirements are implemented and validated.
- [x] All referenced `NFx` requirements have measurements or checks.
- [x] All referenced `SRx` security requirements are implemented and validated.
- [x] Relevant `docs/wiki/` pages are updated to reflect the delivered implementation.
- [x] Affected wiki links resolve after documentation updates.
- [x] Rollback/backout plan documented for each work item.

## Notes

- This initial draft has been updated to optimize for a single coordinated PR while still sequencing the implementation as incremental work items for review and validation.
- Cross-cutting validation defaults to `dotnet build` and `dotnet test` at the repository root unless later delivery constraints require a narrower CI-compatible command set.
- The plan keeps local Keycloak and Azure-aligned Microsoft Entra ID compatibility explicit, but local delivery remains the first validation path for this work package.
- Automated build, unit, integration, functional, and E2E validation now pass with the local test provider used when AppHost infrastructure containers are disabled, including persisted operator auth audit coverage and dedicated Web auth unit tests.
