# Work Package Test Mitigation Plan

This plan turns the authentication and authorisation test review into execution-ready mitigation work. It prioritises lower-level deterministic coverage first, adds only minimal supporting implementation seams when tests cannot otherwise be written reliably, and keeps wiki guidance aligned with any resulting changes to testing approach or local validation.

## Summary

- **Source review**: `../work-package-test-review-report.md`
- **Work package**: `./docs/003-authentication-and-authorisation/`
- **Status**: `completed`
- **Inputs**:
  - `../work-package-test-review-report.md`
  - `../requirements.md`
  - `../technical-specification.md`
  - `001-delivery-plan.md`

## Description of work

Mitigate the review findings for work package `003-authentication-and-authorisation` by expanding deterministic auth coverage across the Web and API test suites, hardening the highest-risk gaps first, and updating repository wiki guidance where the testing approach or local validation guidance changes. The planned work focuses on missing sign-out and session-recovery coverage, protected-route challenge and denial behavior in the Blazor UI, delegated-scope and auth-helper unit coverage, API invalid-token and no-role fail-closed coverage, auth audit event coverage, and stability improvements for the retained real-infrastructure E2E smoke.

## Mitigation approach

- **Delivery model**: `phased hardening`
- **Branching**: keep the work on `003-authentication-and-authorisation`, implement the mitigation work items in order on that branch, and merge the completed hardening changes as one coordinated PR once the full build, test, documentation, and local validation gates are green.
- **Dependencies**: existing Web, API, and AppHost auth implementation; existing Web unit/functional/E2E projects; existing API unit/integration projects; Aspire local orchestration; local Keycloak test path; `docs/wiki/testing-and-quality.md`; `docs/wiki/local-development.md`; `docs/wiki/runtime-behavior.md`; `docs/wiki/operator-guide.md` when behavior or validation guidance changes.
- **Key risks**:
  - Expanding coverage through brittle browser-only tests could increase maintenance cost, mitigated by preferring unit and integration coverage before adding or expanding E2E coverage.
  - Some auth behaviors may need small supporting seams to be testable deterministically, mitigated by keeping any supporting production changes minimal, behavior-preserving, and limited to enabling reliable tests.
  - Documentation drift could leave the wiki inconsistent with the hardened testing approach or updated local validation flow, mitigated by explicit wiki-update tasks before the mitigation plan is considered complete.

## Review findings to address

| Finding ID | Review area | Review assessment | Source evidence | Planned mitigation |
| --- | --- | --- | --- | --- |
| `F1` | Sign-out, session-loss, and recovery behavior | Missing / Partial | `docs/003-authentication-and-authorisation/work-package-test-review-report.md` executive summary and coverage matrix for `FR1`, `TR3`, `NF2`, `SR4` | Add Web functional coverage for sign-out, post-sign-out denial, and re-authentication after session loss; retain only a minimal E2E re-auth smoke if lower-level coverage cannot fully replace it. |
| `F2` | Blazor protected-route challenge and underprivileged denial behavior | Missing / Weak | Review findings for `FR3`, `FR5`, `FR7`, `FR9`, `FR10`, `TR1`, `TR2`; current route logic in `src/TNC.Trading.Platform.Web/Components/Authorization/RedirectToAuthorizationOutcome.razor` | Add route-first functional tests and lower-level coverage for redirect decisions, role-matrix denial, and no-role access-denied behavior. |
| `F3` | Delegated-scope, navigation, operator-context, and auth-helper coverage | Missing / Weak | Review findings for `FR4`, `FR8`, `IR2`; current auth helpers in `src/TNC.Trading.Platform.Web/Authentication/*` | Add unit tests for `PlatformNavigationAccessCoordinator`, `PlatformAccessTokenProvider`, `PlatformAuthAuditClient`, `RedirectToAuthorizationOutcome`, and operator-context edge cases; add minimal seams only if required. |
| `F4` | API fail-closed behavior for invalid, expired, tampered, and no-role tokens | Missing / Partial | Review findings for `FR6`, `FR7`, `FR9`, `SR4`; current API auth tests in `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs` | Extend API integration coverage for invalid issuer, audience, signature, expired token, no-role callers, and remaining protected endpoints. |
| `F5` | Auth audit and secret-safe observability coverage | Missing / Weak | Review findings for `NF4`, `DR1`, `SR2`; current audit coverage limited to one sign-out persistence test | Add integration coverage for supported auth audit event types and secret-safe assertions; add unit/integration coverage where helper behavior needs verification. |
| `F6` | Real-infrastructure E2E smoke fragility | Weak / Fragile | Review finding for hard-coded localhost ports in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs` | Keep one critical Keycloak/Aspire smoke, remove port assumptions, and avoid expanding E2E breadth beyond what lower-level suites cannot prove. |

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
| Work Item 1: Add deterministic Web auth helper and access-decision unit coverage | Add lower-level tests for operator-context mapping, delegated-scope handling, route redirection decisions, and auth-audit helper behavior so the most regression-prone Web auth logic is covered without relying on browser infrastructure. | `F2`, `F3`, `F5` | `FR4`, `FR5`, `FR8`, `FR10`, `NF2`, `NF4`, `SR1`, `SR2`, `SR4`, `TR1`, `TR2`, `IR2` | No prior work item dependency; establishes the fast deterministic baseline needed by later functional work. | `dotnet build`; targeted Web unit tests; `dotnet test` | Revert new Web unit tests and any minimal supporting seams together if they introduce instability or change auth behavior. | Review new test names and comments for requirement traceability and confirm any supporting seams remain behavior-preserving. |
| Work Item 2: Expand API auth negative-path and audit integration coverage | Add integration coverage for invalid, expired, tampered, and no-role bearer tokens; cover the remaining protected endpoints; and expand auth audit event persistence checks with secret-safe assertions. | `F4`, `F5` | `FR6`, `FR7`, `FR9`, `FR10`, `NF2`, `NF4`, `SR1`, `SR2`, `SR4`, `DR1`, `TR1`, `TR2`, `IR2` | Depends on Work Item 1 only where shared helper patterns or test utilities are reused; otherwise independent from browser work. | `dotnet build`; targeted API integration tests; `dotnet test` | Revert added API integration tests and any related test helper changes if new scenarios reveal unstable assumptions that cannot be resolved safely in the same slice. | Validate the expected `401` versus `403` matrix for each token variant and confirm audit payload assertions remain secret-safe. |
| Work Item 3: Expand Blazor functional coverage for sign-out, route protection, and session recovery | Add route-first functional coverage for anonymous challenge, signed-in denial, no-role access-denied behavior, sign-out redirect, post-sign-out denial, and delegated-scope recovery flows. | `F1`, `F2`, `F3` | `FR1`, `FR3`, `FR4`, `FR5`, `FR7`, `FR8`, `FR10`, `NF2`, `NF5`, `SR1`, `SR4`, `TR1`, `TR2`, `TR3`, `IR2`, `OR2` | Depends on Work Item 1 lower-level coverage and may reuse helpers or seams introduced there. | `dotnet build`; targeted Web functional tests; `dotnet test` | Revert newly added functional tests and any narrowly scoped implementation hooks if they create non-deterministic behavior or require broader auth-flow changes than intended. | Verify that route-first tests assert redirect destinations, returned content, and re-authentication outcomes rather than only page text. |
| Work Item 4: Harden the retained real-infrastructure auth smoke and update wiki guidance | Keep the minimal Aspire/Keycloak smoke, remove hard-coded port assumptions, and update wiki pages so the hardened auth test approach and local validation expectations are documented. | `F1`, `F5`, `F6` | `NF4`, `NF5`, `TR3`, `IR1`, `OR2` | Depends on Work Items 1-3 because the wiki and retained E2E scope should reflect the final mitigated test strategy. | `dotnet build`; targeted Web E2E tests; `dotnet test`; wiki link review | Revert E2E helper hardening and documentation changes together if the smoke becomes less reliable or the documentation no longer matches the delivered testing approach. | Run the retained real-infrastructure smoke only after lower-level suites are green and verify the updated wiki guidance matches the final validation sequence. |

### Work Item 1 details

- [x] Work Item 1: Add deterministic Web auth helper and access-decision unit coverage
  - [x] Build and test baseline established
  - [x] Task 1: Expand operator-context unit coverage
    - [x] Step 1: Add unauthenticated principal coverage for `PlatformOperatorContextAccessor`.
    - [x] Step 2: Add no-role authenticated principal coverage for `PlatformOperatorContextAccessor`.
    - [x] Step 3: Add viewer and operator role-shape edge cases, including duplicate role-claim handling and configured display-name fallback behavior.
    - [x] Step 4: Add or improve test comments so each new unit test captures requirement traceability, what is verified, the expected result, and why it matters.
  - [x] Task 2: Add deterministic scope and navigation helper coverage
    - [x] Step 1: Add unit tests for `PlatformAccessTokenProvider` covering missing access token, satisfied scope set, and missing-scope challenge behavior.
    - [x] Step 2: Add unit tests for `PlatformNavigationAccessCoordinator` covering unauthenticated redirect, missing-scope redirect, and authenticated success without introducing browser-only dependencies.
    - [x] Step 3: Introduce minimal supporting seams only if the current helper construction blocks reliable unit tests, keeping production behavior unchanged.
    - [x] Step 4: Add or improve test comments for the new scope and navigation tests.
  - [x] Task 3: Add access-denied and audit-helper lower-level coverage
    - [x] Step 1: Add lower-level coverage for `RedirectToAuthorizationOutcome` so anonymous users redirect to sign-in and authenticated users redirect to access-denied.
    - [x] Step 2: Add lower-level coverage for `PlatformAuthAuditClient` behavior, including missing-token and non-success-response handling.
    - [x] Step 3: Confirm helper-level assertions avoid relying on arbitrary waits or fragile environment assumptions.
    - [x] Step 4: Add or improve test comments for redirect and audit-helper coverage.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformOperatorContextAccessorTests.cs`: extend operator-context edge-case coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformNavigationAccessCoordinatorTests.cs`: add new unit tests for navigation and scope escalation decisions.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAccessTokenProviderTests.cs`: add new unit tests for delegated-token behavior.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthAuditClientTests.cs`: add new unit tests for audit helper behavior.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/RedirectToAuthorizationOutcomeTests.cs`: add new lower-level tests for route redirect decisions.
    - `src/TNC.Trading.Platform.Web/Authentication/*`: only if minimal behavior-preserving seams are needed to enable deterministic tests.
    - `src/TNC.Trading.Platform.Web/Components/Authorization/RedirectToAuthorizationOutcome.razor`: only if a minimal test seam is required without changing runtime behavior.
    - `docs/wiki/testing-and-quality.md`: update the documented auth testing approach if lower-level auth coverage structure changes.
  - **Work Item Dependencies**: Foundational slice; complete before the broader functional hardening work starts.
  - **User Instructions**: Prefer adding new unit-test files rather than overloading one file; keep one top-level test class per file and preserve the existing XML comment pattern for traceability.

### Work Item 2 details

- [x] Work Item 2: Expand API auth negative-path and audit integration coverage
  - [x] Build and test baseline established
  - [x] Task 1: Add invalid-token and no-role fail-closed coverage
    - [x] Step 1: Extend the API test token factory/helper pattern to generate wrong-issuer, wrong-audience, wrong-signature, expired, and no-role tokens.
    - [x] Step 2: Add integration tests proving protected endpoints fail closed with `401 Unauthorized` or `403 Forbidden` as appropriate for those token variants.
    - [x] Step 3: Add coverage for authenticated no-role callers against representative protected endpoints so signed-in-but-underprivileged API behavior is explicit.
    - [x] Step 4: Add or improve test comments for the new fail-closed scenarios.
  - [x] Task 2: Cover the remaining protected API surfaces directly
    - [x] Step 1: Add auth coverage for `PUT /api/platform/configuration`.
    - [x] Step 2: Add auth coverage for `POST /api/platform/auth/manual-retry`.
    - [x] Step 3: Add auth coverage for `GET /api/platform/events`.
    - [x] Step 4: Add compact role-matrix checks that strengthen administrator, operator, viewer, and no-role boundary expectations without duplicating existing happy-path tests unnecessarily.
  - [x] Task 3: Expand auth audit event persistence coverage
    - [x] Step 1: Add integration coverage for `SignInCompleted` audit persistence.
    - [x] Step 2: Add integration coverage for `AccessDenied` audit persistence.
    - [x] Step 3: Add integration coverage for `TokenAcquisitionFailed` audit persistence.
    - [x] Step 4: Strengthen assertions so persisted summaries and details are checked for secret-safe behavior, not only one excluded string.
    - [x] Step 5: Add or improve test comments for the audit-event matrix.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`: extend API auth coverage and audit-event matrix.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/TestJwtTokenFactory.cs`: add a dedicated helper if extracting token variants improves readability and reuse.
    - `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`: only if behavior-preserving testability seams are needed for deterministic assertions.
    - `docs/wiki/testing-and-quality.md`: update the documented API auth test matrix if the testing approach changes materially.
    - `docs/wiki/runtime-behavior.md`: update only if clarified deny/fail-closed runtime behavior becomes more explicit through mitigation work.
  - **Work Item Dependencies**: Can proceed once the baseline is green; reuse any shared helper patterns from Work Item 1 where helpful.
  - **User Instructions**: Keep the JWT negative-path matrix compact and readable by extracting reusable token builders rather than repeating token construction inline.

### Work Item 3 details

- [x] Work Item 3: Expand Blazor functional coverage for sign-out, route protection, and session recovery
  - [x] Build and test baseline established
  - [x] Task 1: Add route-first anonymous-versus-authenticated coverage
    - [x] Step 1: Add functional coverage for anonymous navigation to `/status` and verify redirect to `/authentication/sign-in` with preserved `returnUrl`.
    - [x] Step 2: Add functional coverage for anonymous navigation to `/configuration` and verify redirect to `/authentication/sign-in` with preserved `returnUrl`.
    - [x] Step 3: Add functional coverage for anonymous navigation to `/administration/authentication` and verify redirect to `/authentication/sign-in` with preserved `returnUrl`.
    - [x] Step 4: Add functional coverage for signed-in no-role access to protected routes and verify the dedicated access-denied experience.
    - [x] Step 5: Add or improve test comments for the route-protection matrix.
  - [x] Task 2: Add sign-out and recovery coverage
    - [x] Step 1: Add functional coverage for `/authentication/sign-out` redirecting the operator to `/`.
    - [x] Step 2: Add functional coverage proving protected routes require a new sign-in after sign-out.
    - [x] Step 3: Add functional coverage for recovery after cleared or lost auth state using a deterministic test approach rather than arbitrary time-based waits.
    - [x] Step 4: Add or improve test comments for sign-out and recovery scenarios.
  - [x] Task 3: Add delegated-scope recovery coverage
    - [x] Step 1: Add functional coverage proving a missing elevated scope redirects the operator back through sign-in with the requested scope preserved.
    - [x] Step 2: Add compact role-matrix coverage for representative viewer, operator, administrator, and no-role navigation outcomes.
    - [x] Step 3: Introduce only minimal supporting implementation seams if current functional harnesses cannot clear or vary auth state deterministically.
    - [x] Step 4: Add or improve test comments for delegated-scope recovery coverage.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`: extend route-first, sign-out, and recovery coverage.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs`: add a dedicated file if separating route-matrix scenarios improves readability.
    - `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs`: only if a minimal behavior-preserving seam is required for deterministic sign-out or recovery assertions.
    - `src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs`: only if a minimal behavior-preserving seam is required for deterministic delegated-scope recovery assertions.
    - `docs/wiki/testing-and-quality.md`: update the documented functional auth coverage approach.
    - `docs/wiki/local-development.md`: update the documented local validation sequence if sign-out or recovery checks become more explicit.
    - `docs/wiki/operator-guide.md`: update only if operator-facing auth recovery guidance changes.
  - **Work Item Dependencies**: Builds on Work Item 1 and should be completed before deciding whether additional E2E auth recovery coverage is still necessary.
  - **User Instructions**: Keep route-first functional tests focused on observable outcomes such as final URL, HTTP status, redirect destination, sign-in choice surface, and access-denied content rather than indirect assumptions.

### Work Item 4 details

- [x] Work Item 4: Harden the retained real-infrastructure auth smoke and update wiki guidance
  - [x] Build and test baseline established
  - [x] Task 1: Reduce E2E smoke fragility
    - [x] Step 1: Remove hard-coded localhost port assumptions from the Aspire dashboard auth smoke.
    - [x] Step 2: Keep the retained dashboard/Keycloak smoke focused on one or two critical auth journeys that lower-level suites cannot replace.
    - [x] Step 3: Add or improve test comments so the retained E2E smoke clearly documents why it exists and what lower-level suites already cover.
  - [x] Task 2: Reassess whether extra E2E auth recovery coverage is still required
    - [x] Step 1: Review the completed unit, integration, and functional mitigation work before adding any new E2E auth scenario.
    - [x] Step 2: Add one targeted E2E re-authentication smoke only if the remaining confidence gap cannot be closed reliably at lower test levels.
    - [x] Step 3: Avoid arbitrary time-based waits as the primary strategy for auth recovery validation.
  - [x] Task 3: Update wiki guidance for the hardened auth test approach
    - [x] Step 1: Update `docs/wiki/testing-and-quality.md` to reflect the final layered auth test strategy and validation commands.
    - [x] Step 2: Update `docs/wiki/local-development.md` with the final local validation expectations for sign-in, sign-out, and recovery if the mitigation changes that guidance.
    - [x] Step 3: Update `docs/wiki/runtime-behavior.md` and `docs/wiki/operator-guide.md` if the delivered mitigation clarifies operator-visible auth denial or recovery behavior.
    - [x] Step 4: Verify affected wiki links still resolve after documentation updates.
  - [x] Relevant `docs/wiki/` pages updated to reflect the delivered testing or implementation changes
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs`: harden the real-infrastructure smoke.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformAuthenticationE2ETests.cs`: extend only if one additional targeted E2E smoke remains justified after lower-level hardening.
    - `docs/wiki/testing-and-quality.md`: update the final auth testing strategy.
    - `docs/wiki/local-development.md`: update the final local validation sequence where needed.
    - `docs/wiki/runtime-behavior.md`: update if runtime denial or recovery behavior is clarified.
    - `docs/wiki/operator-guide.md`: update if operator-facing guidance changes.
  - **Work Item Dependencies**: Final hardening slice after deterministic lower-level and functional coverage is in place.
  - **User Instructions**: Do not broaden the E2E suite unless a concrete residual gap remains after Work Items 1-3; the retained smoke should stay minimal, stable, and high-value.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`
  - `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`
- **Integration tests**:
  - `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`
- **Functional tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`
- **E2E tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj`
- **Manual checks**:
  - Start the AppHost and verify sign-in, sign-out, and protected-route recovery with the seeded local users after the final mitigation slice lands.
  - Verify anonymous navigation to protected Blazor routes reaches the sign-in entry point and preserves the intended return URL.
  - Verify no-role and underprivileged users reach the access-denied experience in the UI and receive `403 Forbidden` from protected APIs where applicable.
  - Verify the retained Aspire dashboard/Keycloak smoke still reaches the intended protected UI surface without relying on fixed ports.
  - Verify affected wiki links resolve after documentation updates.
- **Security checks**:
  - Review new auth tests to confirm tokens, secrets, client secrets, and raw sensitive protocol values are not written into assertions, logs, or committed fixtures.
  - Confirm invalid, expired, tampered, and no-role auth scenarios fail closed with the expected `401` or `403` outcomes.
  - Confirm audit-event assertions verify secret-safe summaries and details for the covered auth event types.
  - Confirm any supporting implementation seams remain behavior-preserving and do not weaken runtime auth requirements.

## Acceptance checklist

- [x] Every planned mitigation maps back to one or more findings in `work-package-test-review-report.md`.
- [x] High-priority missing or weak coverage is addressed before lower-priority improvements.
- [x] The plan prefers lower-level automated tests before higher-level tests where practical.
- [x] Required validation steps are defined for each work item.
- [x] Relevant `docs/wiki/` pages are updated to reflect the delivered testing or implementation changes.
- [x] Affected wiki links resolve after documentation updates.
- [x] Rollback/backout plan documented for each work item.

## Notes

- The selected plan path is `docs/003-authentication-and-authorisation/plans/002-work-package-test-mitigation-plan.md`, following the existing `001-delivery-plan.md` numbering.
- No solution file was discovered during planning, so repo-root `dotnet build` and explicit project-level `dotnet test` commands are used as the validation baseline.
- The mitigation plan intentionally keeps product-code changes minimal and limited to enabling reliable deterministic tests where the current design otherwise blocks coverage.
- `docs/wiki/testing-and-quality.md` should be treated as the minimum required wiki update because this mitigation changes the documented testing approach even if runtime behavior remains unchanged.
- Final validation passed with `dotnet build` plus the Web unit, API unit, API integration, Web functional, and Web E2E projects after Work Items 3 and 4 completed.
