# Work Package Test Mitigation Plan

> Use this template to plan how the issues identified in a work-package test review will be mitigated. Keep the plan actionable, traceable to the review findings, and aligned to the repository testing approach.

## Summary

- **Source review**: `../002-work-package-test-review-report.md`
- **Work package**: `./docs/003-authentication-and-authorisation/`
- **Status**: `draft`
- **Inputs**:
  - `../002-work-package-test-review-report.md`
  - `../requirements.md`
  - `../technical-specification.md`
  - existing numbered plan files in this folder (for example `001-delivery-plan.md`)

## Description of work

Strengthen the authentication and authorisation automated test suite by repairing the failing real AppHost plus Keycloak smoke, expanding thinner UI authorization coverage, adding missing configuration-validation coverage, and closing lower-level fail-closed and observability gaps identified in the test review. The mitigation work remains scoped to the review findings and allows only the minimum supporting implementation or documentation changes needed to make the resulting tests deterministic, traceable, and maintainable.

## Mitigation approach

- **Delivery model**: `phased hardening`
- **Branching**: keep the work on `003-authentication-and-authorisation`, deliver work items sequentially, and only mark the mitigation complete after build, targeted auth tests, and required documentation updates are green.
- **Dependencies**: `src/TNC.Trading.Platform.AppHost`; `src/TNC.Trading.Platform.Web`; `src/TNC.Trading.Platform.Api`; Web unit, functional, and E2E test projects; API integration test project; Aspire local orchestration; local Keycloak realm import; work-package docs; relevant `docs/wiki/` pages.
- **Key risks**:
  - Runtime endpoint discovery in the real AppHost smoke may remain brittle if it still depends on launch settings instead of AppHost-assigned endpoints.
  - UI role-matrix expansion may drift into slow browser-heavy coverage unless the functional layer remains the default proving ground.
  - Configuration-validation work may require small production guard clauses if current auth registration is too implicit to test deterministically.
  - Observability assertions may become brittle if they depend on incidental log formatting rather than event type, route, and redaction outcomes.
  - The exact affected `docs/wiki/` paths were not discoverable during planning and must be confirmed during execution before closure.

## Review findings to address

| Finding ID | Review area | Review assessment | Source evidence | Planned mitigation |
| --- | --- | --- | --- | --- |
| `F1` | `IR1`, `TR3` real local IdP smoke | Weak / failing | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs`; review finding that the test waits on a fixed launch-settings URL instead of the runtime Web endpoint | Replace fixed-port launch-settings discovery with runtime AppHost endpoint discovery, keep one resilient real Keycloak browser smoke, and defer extra real-provider breadth until the primary smoke is green. |
| `F2` | `FR5`, `FR7`, `FR9`, `TR2` UI role matrix | Partial | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs`; review finding that viewer, operator, administrator, and no-role outcomes are not fully covered across `/status`, `/configuration`, and `/administration/authentication` | Add a compact functional route matrix for seeded roles across the main protected routes and update test comments to preserve traceability and rationale. |
| `F3` | `NF4`, `DR1`, `SR2` auth observability | Partial | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthAuditClientTests.cs`; `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`; review finding that current tests bypass Web handlers and do not strongly prove secret-safe logs or end-to-end audit emission | Add lower-level and functional tests that drive Web sign-in, sign-out, denial, and scope-failure flows through the Web stack and assert resulting audit behavior and redaction-safe outputs. |
| `F4` | `NF1`, `NF3`, `OR1`, `SR3` configuration validation | Missing | `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs`; `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs`; review finding that missing authority, audience, provider selection, and startup-validation behavior are not directly tested | Add focused Web and API tests for provider-specific missing configuration and add small supporting startup-validation logic only if current registration is too implicit to test deterministically. |
| `F5` | `NF2`, `SR4`, `IR2` fail-closed edges | Partial | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAccessTokenProviderTests.cs`; `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformNavigationAccessCoordinatorTests.cs`; review finding that invalid callback handling, declined elevated-scope acquisition, and deeper session-expiry boundaries are not directly covered | Add lower-level tests for invalid callback rejection, denied elevation, and session-expiry boundaries before considering broader browser-path expansion. |

## Mitigation Plan

### Execution gates (required)

Before starting *any* mitigation work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (review findings) | Traceability (requirements) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Repair the real AppHost authentication smoke | Replace brittle fixed-port discovery in the dashboard E2E path with runtime endpoint discovery and re-establish one reliable real Keycloak sign-in smoke for `/status`. | `F1` | `IR1`, `TR3`, `FR1`, `FR5`, `NF2` | Requires working AppHost plus Keycloak local orchestration. Complete before broadening more browser-path coverage. | Repo build plus targeted Web E2E execution must pass with the repaired smoke. | Revert endpoint-discovery changes and retain the prior shape only if the replacement discovery strategy proves less stable. | Run the repaired E2E test where Aspire infrastructure containers are enabled and confirm the viewer sign-in reaches `/status`. |
| Work Item 2: Add the Web protected-route role matrix | Add compact functional coverage for viewer, operator, administrator, and no-role outcomes across the main protected Blazor routes. | `F2` | `FR3`, `FR5`, `FR7`, `FR9`, `FR10`, `TR2` | Depends on the existing functional test harness. Independent of Work Item 1 once route behavior is deterministic under the test provider. | Repo build plus targeted Web functional tests must pass for all added role-route combinations. | Revert newly added route-matrix tests if they reveal unsupported behavior and split the matrix into smaller slices before retrying. | Validate each seeded user independently and keep route expectations aligned to the documented role boundaries. |
| Work Item 3: Add authentication configuration-validation coverage | Add missing tests for provider selection and startup validation in Web and API auth registration, with minimal supporting implementation hardening if required. | `F4` | `NF1`, `NF3`, `OR1`, `SR3`, `SR4` | Can run after Work Item 2. May require small changes in auth registration methods to make invalid configuration fail deterministically. | Repo build plus targeted Web unit tests and API integration or unit-level validation coverage must pass. | Revert new validation checks if they break legitimate existing test-provider scenarios, then narrow validation to provider-specific branches only. | Re-check local and Azure-aligned configuration assumptions after the new validation rules are in place. |
| Work Item 4: Strengthen fail-closed and observability coverage | Add lower-level and functional tests for invalid callback handling, declined scope elevation, session-expiry boundaries, audit emission from Web flows, and secret-safe auth observability. | `F3`, `F5` | `NF2`, `NF4`, `SR2`, `SR4`, `DR1`, `IR2`, `TR1`, `TR3` | Depends on stable route and auth configuration behavior from Work Items 1-3. | Repo build plus targeted Web unit, functional, and API integration coverage must pass; security checks must confirm no secret-leakage assertions regress. | Revert only the newly added tests and any supporting instrumentation hooks if the observability seam proves too invasive, then replace them with narrower assertions. | Review audit and logging outputs with the new tests to confirm assertions remain format-tolerant and redaction-focused. |
| Work Item 5: Re-run package validation and update implementation guidance | Re-run the full auth suite, capture the post-mitigation outcome, and update work-package and wiki documentation affected by the strengthened testing approach or any supporting behavior changes. | `F1`, `F2`, `F3`, `F4`, `F5` | `OR2`, `TR1`, `TR2`, `TR3`, `NF4` | Depends on completion of Work Items 1-4. | Full repo build and the auth unit, integration, functional, and E2E suites must pass; documentation links must resolve. | Revert documentation-only changes separately if validation wording becomes inaccurate, but do not close the plan until docs match actual behavior. | Update the review evidence in a new report file if a refreshed review is needed and confirm affected documentation paths resolve. |

### Work Item 1 details

- [x] Work Item 1: Repair the real AppHost authentication smoke
  - [x] Build and test baseline established
  - [x] Task 1: Replace brittle endpoint discovery in the AppHost E2E flow
    - [x] Step 1: Inspect `PlatformDashboardAuthenticationE2ETests` and any supporting AppHost process helpers to isolate the fixed-port dependency on Web launch settings.
    - [x] Step 2: Implement runtime discovery of the live Web endpoint from the running AppHost or Aspire-exposed metadata instead of `launchSettings.json`.
    - [x] Step 3: Keep the test focused on one resilient viewer sign-in path to `/status` without adding arbitrary waits.
  - [x] Task 2: Preserve traceability and determinism in the repaired smoke
    - [x] Step 1: Update the test comment so requirement traceability, expected runtime-discovery behavior, and the reason for the smoke remain explicit.
    - [x] Step 2: Confirm the test fails clearly when the AppHost endpoint cannot be discovered or the Web app never becomes reachable.
  - [x] Task 3: Extend real-provider smoke only if the primary path is green
    - [x] Step 1: Evaluate whether a minimal real-provider sign-out or re-authentication smoke can be added without duplicating the repaired sign-in path.
    - [x] Step 2: Defer extra browser breadth if the repaired sign-in smoke is still the higher-value stabilisation target.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs`: replace fixed-port discovery and keep the smoke focused.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessHandle.cs`: adjust helper behavior if runtime endpoint capture belongs in the process handle.
    - `src/TNC.Trading.Platform.AppHost/`: touch only if a stable AppHost-exposed endpoint signal is required for deterministic discovery.
  - **Work Item Dependencies**: First work item because it restores the strongest local IdP evidence and unblocks confidence in `IR1`.
  - **User Instructions**: Execute the repaired Web E2E auth suite in an environment where Aspire infrastructure containers are allowed and confirm the viewer sign-in reaches `/status`.

### Work Item 2 details

- [x] Work Item 2: Add the Web protected-route role matrix
  - [x] Build and test baseline established
  - [x] Task 1: Add compact route-matrix functional coverage
    - [x] Step 1: Extend `PlatformProtectedRouteFunctionalTests` to cover `local-viewer`, `local-operator`, `local-admin`, and `local-norole` across `/status`, `/configuration`, and `/administration/authentication`.
    - [x] Step 2: Prefer parameterized or compact test structure only if it remains readable and keeps each expected behavior explicit.
    - [x] Step 3: Assert both allowed outcomes and denied outcomes, including redirects to sign-in or access-denied where applicable.
  - [x] Task 2: Align route expectations to the documented role boundaries
    - [x] Step 1: Cross-check each route expectation against `FR7`, `FR9`, `FR10`, and the technical specification role model before locking test assertions.
    - [x] Step 2: Add or update test comments so each new or changed test states the traced requirement, expected route behavior, and why the check matters.
  - [x] Task 3: Keep browser-path breadth minimal
    - [x] Step 1: Do not add more E2E coverage for the route matrix unless functional tests cannot prove the behavior.
    - [x] Step 2: Reuse the existing functional test-provider path to keep the UI authorization matrix fast and deterministic.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs`: add the seeded-role route matrix and richer assertions.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`: extend if route-setup helpers or sign-in-journey helpers are needed.
    - `src/TNC.Trading.Platform.Web/Components/Authorization/`: confirm current route metadata only if tests reveal ambiguity.
  - **Work Item Dependencies**: Follows the real smoke repair but can execute independently from browser-path work once the functional harness is stable.
  - **User Instructions**: Re-run the functional test suite and verify the expected route outcome for each seeded role matches the documented capability boundaries.

### Work Item 3 details

- [x] Work Item 3: Add authentication configuration-validation coverage
  - [x] Build and test baseline established
  - [x] Task 1: Add Web auth registration validation tests
    - [x] Step 1: Extend `PlatformWebAuthenticationServiceCollectionExtensionsTests` to cover missing authority, missing Web client identifier, invalid provider selection, and provider-specific branch behavior.
    - [x] Step 2: Verify test-provider registration continues to avoid accidental OpenID Connect registration requirements.
  - [x] Task 2: Add API auth registration validation tests
    - [x] Step 1: Add focused validation coverage for missing API audience, missing authority, and provider-specific API client identifier requirements.
    - [x] Step 2: Prefer a lower-level test seam if the current API registration logic can be exercised without full distributed startup.
  - [x] Task 3: Add minimal supporting implementation hardening only where required
    - [x] Step 1: Introduce explicit guard clauses or provider-specific validation failures in the Web and API authentication registration code if current behavior is too implicit to test deterministically.
    - [x] Step 2: Ensure validation messages identify missing configuration keys or provider branches without exposing secret values.
    - [x] Step 3: Update test comments for all new validation tests so requirement traceability and rationale remain explicit.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs`: add only the smallest deterministic validation hardening needed.
    - `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs`: add only the smallest deterministic validation hardening needed.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebAuthenticationServiceCollectionExtensionsTests.cs`: add provider and startup validation coverage.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/`: add focused configuration-validation coverage if no existing lower-level API auth test project exists.
  - **Work Item Dependencies**: Should follow the route-matrix work so baseline behavior remains stable before tightening startup validation.
  - **User Instructions**: Re-check both local Keycloak and Azure-aligned configuration examples after the new validation rules land to ensure legitimate configurations still pass.

### Work Item 4 details

- [x] Work Item 4: Strengthen fail-closed and observability coverage
  - [x] Build and test baseline established
  - [x] Task 1: Add fail-closed lower-level coverage for missing auth edge cases
    - [x] Step 1: Add tests for invalid or tampered callback handling in the Web auth flow.
    - [x] Step 2: Add tests for declined or unavailable elevated-scope acquisition paths so privileged navigation fails closed.
    - [x] Step 3: Add tests for session-expiry boundaries beyond simple cookie removal where the current design exposes a deterministic seam.
  - [x] Task 2: Add Web-driven observability and audit coverage
    - [x] Step 1: Extend Web-level tests so sign-in, sign-out, access-denied, and token-acquisition-failure flows drive audit emission through the real Web handler path.
    - [x] Step 2: Assert persisted auth event types and secret-safe summaries rather than brittle full payload formatting.
    - [x] Step 3: Add log-redaction assertions where a deterministic logging seam exists, focusing on absence of tokens, secrets, and sensitive protocol payloads.
  - [x] Task 3: Keep tests maintainable and traceable
    - [x] Step 1: Reuse existing seams such as `PlatformAccessTokenProvider`, `PlatformNavigationAccessCoordinator`, and `PlatformAuthAuditClient` before adding heavier browser scenarios.
    - [x] Step 2: Update all new or changed test comments to record the traced requirements, expected fail-closed result, and why the behavior matters.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAccessTokenProviderTests.cs`: add denied-elevation and session-boundary coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformNavigationAccessCoordinatorTests.cs`: extend protected-navigation failure coverage where applicable.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthAuditClientTests.cs`: strengthen audit and redaction assertions.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/`: add Web-driven sign-in, sign-out, denial, callback, and scope-failure coverage.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`: extend auth-event verification only where API evidence is still needed.
    - `src/TNC.Trading.Platform.Web/Authentication/`: touch only if a small seam is required to make invalid-callback or observability behavior testable.
  - **Work Item Dependencies**: Depends on deterministic routing and configuration validation from earlier work items.
  - **User Instructions**: Review the new assertions for format tolerance and ensure the tests prove secret absence rather than implementation-specific logging text.

### Work Item 5 details

- [ ] Work Item 5: Re-run package validation and update implementation guidance
  - [ ] Build and test baseline established
  - [ ] Task 1: Re-run the full auth package validation set
    - [ ] Step 1: Run the full build and targeted auth unit, functional, integration, and E2E commands listed in this plan.
    - [ ] Step 2: Confirm the previous failing E2E path is green and that added coverage for `F2`-`F5` passes consistently.
    - [ ] Step 3: Capture the final pass/fail state and any residual gaps that must remain explicitly deferred.
  - [ ] Task 2: Update documentation affected by the mitigation work
    - [ ] Step 1: Update this work-package documentation if the strengthened tests changed the stated validation approach, assumptions, or residual risks.
    - [ ] Step 2: Update the relevant `docs/wiki/` pages to reflect changes to the testing approach, local validation guidance, or supporting runtime behavior.
    - [ ] Step 3: Verify affected wiki navigation and cross-links still resolve.
  - [ ] Task 3: Prepare follow-on review evidence
    - [ ] Step 1: Create a new review or mitigation outcome report file rather than overwriting the existing review report if refreshed review evidence is required.
    - [ ] Step 2: Note any remaining assumptions or open risks separately from confirmed completed mitigation evidence.
  - [ ] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [ ] Build and test validation

  - **Files**:
    - `docs/003-authentication-and-authorisation/002-work-package-test-review-report.md`: reference only; do not overwrite without creating a new report if refreshed review evidence is required.
    - `docs/003-authentication-and-authorisation/`: update mitigation-adjacent documentation only if the implemented validation story changes.
    - `docs/wiki/`: update the affected implementation, runtime, local-development, and testing pages before closure.
  - **Work Item Dependencies**: Final closure step after all mitigation work items land.
  - **User Instructions**: Use the documented command set below to re-run the auth package and confirm the final evidence is recorded in a new report artifact if the review is refreshed.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`
- **Integration tests**: `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`
- **Functional tests**: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`
- **E2E tests**: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj`
- **Manual checks**:
  - Start the AppHost with infrastructure containers enabled and confirm the Aspire dashboard, Web app, API, and Keycloak resources become reachable.
  - Validate seeded sign-in outcomes for `local-viewer`, `local-operator`, `local-admin`, and `local-norole` across `/status`, `/configuration`, and `/administration/authentication`.
  - Validate sign-out and re-authentication behavior after session loss.
  - Verify any updated `docs/wiki/` links resolve after documentation changes.
- **Security checks**:
  - Confirm tests asserting observability or audit content do not permit tokens, client secrets, signing keys, or raw sensitive protocol payloads in logs, responses, or persisted auth events.
  - Confirm invalid, missing, expired, or under-scoped auth state still fails closed in Web and API paths.
  - Confirm protected API endpoints continue to return `401` or `403` without browser redirects.

## Acceptance checklist

- [ ] Every planned mitigation maps back to one or more findings in `work-package-test-review-report.md`.
- [ ] High-priority missing or weak coverage is addressed before lower-priority improvements.
- [ ] The plan prefers lower-level automated tests before higher-level tests where practical.
- [ ] Required validation steps are defined for each work item.
- [ ] Relevant `docs/wiki/` pages are updated to reflect the delivered testing or implementation changes.
- [ ] Affected wiki links resolve after documentation updates.
- [ ] Rollback/backout plan documented for each work item.

## Notes

### Confirmed evidence

- The review report identifies five stable mitigation themes: `F1` real AppHost plus Keycloak smoke instability, `F2` incomplete UI role-matrix coverage, `F3` incomplete Web-driven observability coverage, `F4` missing configuration-validation coverage, and `F5` missing fail-closed edge coverage.
- The current delivery plan already places authentication testing, observability, and documentation hardening in scope for this work package.
- The current E2E smoke reads the Web URL from `src/TNC.Trading.Platform.Web/Properties/launchSettings.json`, which matches the review finding that runtime endpoint discovery is the brittle part of the test.
- Existing lower-level Web seams already exist for token acquisition, navigation coordination, operator context, and audit recording, so mitigation can add coverage without defaulting to more browser tests.
- Existing Web and API auth registration code already contains provider-dependent validation branches that are suitable candidates for targeted configuration-validation tests.

### Assumptions and missing information

- This plan uses sequence number `003-` so the existing `002-` plan remains unchanged.
- Repository `docs/wiki/` paths were not discoverable during planning, but repository instructions require wiki updates before completion when testing guidance or implementation behavior changes. Execution should confirm the correct wiki locations or create the required follow-up if the wiki structure has moved.
- The plan assumes the current auth test projects and command paths remain valid and runnable from the repository root.
- If configuration-validation coverage reveals missing production guard clauses, small implementation changes are expected and are intentionally in scope for the mitigation.
