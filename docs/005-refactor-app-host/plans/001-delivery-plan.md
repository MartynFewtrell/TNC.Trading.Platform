# AppHost Composition Refactoring Mitigation Plan

This plan addresses the current complexity in the Aspire AppHost composition and the related test-topology drift where many AppHost-backed suites still rely on synthetic or in-memory substitute behavior. The goal is to simplify the AppHost into a clearer composition root and align distributed tests with real Aspire-managed integrations.

## Summary

- **Source review**: `AppHost complexity review in chat (2026-05-18)`
- **Work package**: `./docs/005-refactor-app-host/`
- **Status**: `completed`
- **Inputs**:
  - `../requirements.md`
  - `../technical-specification.md`
  - existing numbered plan files in this folder (for example `001-delivery-plan.md`)
  - `../../src/TNC.Trading.Platform.AppHost/AppHost.cs`

## Description of work

This mitigation plan reduces complexity in the current Aspire AppHost composition by separating infrastructure registration, project registration, and environment wiring into smaller, cohesive units while preserving the delivered runtime behavior. It also removes the synthetic runtime path from AppHost-backed distributed tests so those suites validate the real Aspire-managed topology instead of a lighter substitute path.

The work is intentionally behavior-preserving for the delivered local runtime experience. The AppHost must remain the composition root, the existing resource set and service relationships must remain intact, the current Keycloak-backed sign-in journey must continue to work, and protected API and Web authorization behavior must remain unchanged from the operator's perspective.

## Mitigation approach

- **Delivery model**: `phased refactoring`
- **Branching**: keep the work on `005-refactor-app-host` and deliver the mitigation items in sequence so the AppHost remains buildable and reviewable after each slice.
- **Dependencies**:
  - `src/TNC.Trading.Platform.AppHost/`
  - `src/TNC.Trading.Platform.Api/`
  - `src/TNC.Trading.Platform.Web/`
  - `test/TNC.Trading.Platform.Api/`
  - `test/TNC.Trading.Platform.Web/`
  - `docs/wiki/local-development.md`
  - `docs/wiki/testing-and-quality.md`
- **Behavior-preservation boundaries**:
  - The AppHost remains the single composition root for local distributed startup.
  - The existing `api`, `web`, `sql`, `platformdb`, `mailpit`, and `keycloak` resource names and relationships remain unchanged unless a direct compatibility issue requires adjustment.
  - The API and Web projects continue to receive equivalent authentication, authorization, and notification configuration.
  - The Keycloak-backed local sign-in flow and protected route behavior remain unchanged.
  - AppHost-backed distributed tests must run against Aspire-managed integrations instead of in-memory substitute runtime paths.
- **Key risks**:
  - AppHost composition extraction could accidentally change environment keys or dependency wait ordering.
    - **Mitigation**: keep the extraction behavior-preserving, add focused runtime validation, and verify the exposed endpoints and auth behavior after each slice.
  - Removing the synthetic runtime branch could break existing API or Web distributed tests that implicitly depend on it.
    - **Mitigation**: migrate those suites in a dedicated work item, introduce explicit real-runtime helpers first, and remove the branch only after the replacement safety net is in place.
  - Real Aspire-backed test runs may be slower or more operationally sensitive than the current synthetic path.
    - **Mitigation**: centralize startup and sign-in helpers, keep tests closed-box, and preserve smaller unit-test coverage where infrastructure is not required.

## Review findings to address

| Finding ID | Review area | Review assessment | Source evidence | Planned mitigation |
| --- | --- | --- | --- | --- |
| `F1` | AppHost composition structure | Complexity / Weak cohesion | `src/TNC.Trading.Platform.AppHost/AppHost.cs` currently mixes builder setup, infrastructure resource registration, project registration, auth-mode selection, and ACS configuration in one file | Split composition into smaller focused files or methods so AppHost becomes a thin orchestration entry point. |
| `F2` | Infrastructure and project wiring | Coupling | `src/TNC.Trading.Platform.AppHost/AppHost.cs`, `src/TNC.Trading.Platform.AppHost/AppHostInfrastructure.cs` | Separate infrastructure modeling from application-project modeling and keep the shared wiring explicit. |
| `F3` | Environment configuration | Duplication / Complexity | `src/TNC.Trading.Platform.AppHost/AppHost.cs` repeats shared auth environment keys across API and Web project wiring | Centralize shared environment wiring into focused helper methods while keeping service-specific configuration separate. |
| `F4` | Test runtime topology | Boundary leakage / Testability | `src/TNC.Trading.Platform.AppHost/AppHost.cs`, `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs` introduce a synthetic runtime branch used by distributed tests | Remove the synthetic AppHost runtime path from Aspire-backed distributed test coverage and migrate those tests to the real integration topology. |
| `F5` | Distributed auth validation | Testability / Change safety | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs` set `AppHost__UseSyntheticRuntime=true` | Replace synthetic AppHost-backed auth checks with real Aspire-managed startup and authentication helpers. |
| `F6` | Validation coverage balance | Testability | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs` exercises the real AppHost-plus-Keycloak path, but most AppHost-backed tests do not | Rebalance the safety net so distributed suites validate the real AppHost path more broadly and consistently. |

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
| Work Item 1: Separate AppHost composition responsibilities | Extract infrastructure registration, project registration, and shared environment wiring into focused AppHost support files without changing runtime behavior. | `F1, F2, F3` | `FR1, FR2, FR4, NF1, NF2, NF3, NF4, OR1, OR2, TR1, TR3` | Baseline item; should happen before the synthetic-runtime removal so the topology is easier to reason about during test migration. | `dotnet build`; `dotnet test`; focused AppHost startup validation; confirm exposed endpoints and waits remain intact. | Revert the extracted AppHost support files and restore the previous single-file composition if startup behavior changes. | Review the resulting AppHost diff for preserved resource names, references, and wait ordering. |
| Work Item 2: Remove synthetic AppHost runtime from distributed tests | Eliminate the synthetic runtime branch from AppHost-backed distributed tests and move those suites to real Aspire integrations. | `F4, F5, F6` | `FR3, FR4, NF2, NF5, SR1, SR3, IR2, TR1, TR2, TR3` | Depends on Work Item 1 so the real topology is clearer before test migration begins. | `dotnet build`; `dotnet test`; targeted API integration, Web functional, and Web E2E runs using the real AppHost-plus-Keycloak path. | Restore the prior branch only as a temporary rollback if migration is incomplete; do not keep mixed-mode distributed validation as the final state. | Validate that AppHost-backed tests no longer set `AppHost__UseSyntheticRuntime=true`. |
| Work Item 3: Align supporting documentation and final validation | Update testing and local-development guidance so it reflects the refactored AppHost structure and the real Aspire integration test model. | `F4, F6` | `FR4, OR1, OR2, TR3` | Depends on Work Item 2 so the documentation matches the delivered test/runtime model. | `dotnet build`; `dotnet test`; wiki link review; manual AppHost walkthrough. | Revert documentation with the implementation if the delivered behavior differs from the updated guidance. | Re-run the documented local validation steps and confirm they match the delivered implementation. |

### Work Item 1 details

- [x] Work Item 1: Separate AppHost composition responsibilities
  - [x] Build and test baseline established
  - [x] Task 1: Prepare the safety net
    - [x] Step 1: Confirm the current exposed endpoint links, environment keys, and dependency waits that must remain unchanged after extraction.
    - [x] Step 2: Confirm which AppHost settings are true product configuration versus test-only composition switches.
    - [x] Step 3: Record any assumptions that the current generated `Projects.*` references impose on the extraction shape.
  - [x] Task 2: Apply the refactor
    - [x] Step 1: Extract infrastructure resource registration from `AppHost.cs` into a focused support file or extension method.
    - [x] Step 2: Extract API and Web project registration into focused support files or extension methods.
    - [x] Step 3: Consolidate shared environment wiring so common authentication and notification settings are applied through small named helpers.
    - [x] Step 4: Keep `AppHost.cs` focused on builder creation, top-level composition calls, and `Build().Run()`.
  - [x] Task 3: Align supporting assets
    - [x] Step 1: Update any impacted comments or traceability notes in AppHost support files.
    - [x] Step 2: Update `docs/wiki/` only if the final structure changes architecture or local guidance materially.
    - [x] Step 3: Document any remaining AppHost constraints that still intentionally live at the composition root.
  - [x] Build and test validation

  Execution notes:
  - Preserved the existing `api`, `web`, `sql`, `platformdb`, `mailpit`, and `keycloak` resource names, the existing `Scalar UI`, `Operator UI`, `Mailpit UI`, and `Keycloak Admin Console` links, and the existing API/Web dependency waits while extracting composition helpers.
  - Confirmed `AppHostSettings.UseSyntheticRuntimeForTests` and `AppHostSettings.EnableInteractiveTestSignIn` are test-only composition switches; ACS endpoint, sender address, and connection string remain product configuration.
  - Kept the generated `Projects.TNC_Trading_Platform_Api` and `Projects.TNC_Trading_Platform_Web` references at the AppHost layer and retained builder creation, settings loading, top-level composition, and `Build().Run()` in `AppHost.cs` as the intended composition-root boundary.

  - **Files**:
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: reduce the top-level file to thin orchestration.
    - `src/TNC.Trading.Platform.AppHost/AppHostInfrastructure.cs`: keep infrastructure modeling aligned with the extracted composition helpers.
    - `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs`: retain only settings that belong to real AppHost composition after test-only behavior is removed.
    - `src/TNC.Trading.Platform.AppHost/*`: add focused AppHost support files as needed, one top-level type per file.
  - **Work Item Dependencies**: Must complete before removing the synthetic runtime branch so test migration is working against a clearer AppHost shape.
  - **User Instructions**: Compare the AppHost dashboard links and startup ordering before and after the refactor to confirm no user-visible topology drift.

### Work Item 2 details

- [x] Work Item 2: Remove synthetic AppHost runtime from distributed tests
  - [x] Build and test baseline established
  - [x] Task 1: Prepare the safety net
    - [x] Step 1: Inventory every AppHost-backed distributed test that currently sets `AppHost__UseSyntheticRuntime` or depends on synthetic test sign-in behavior.
    - [x] Step 2: Define the real-runtime replacements for protected API access, protected Web navigation, and seeded-user sign-in flows.
    - [x] Step 3: Add or strengthen focused helpers that start the AppHost and acquire real authenticated access without relying on in-memory persistence or synthetic provider branching.
  - [x] Task 2: Apply the refactor
    - [x] Step 1: Migrate API integration tests from synthetic AppHost mode to real Aspire-managed startup and auth flows.
    - [x] Step 2: Migrate Web functional tests from synthetic AppHost mode to real Aspire-managed startup and sign-in flows.
    - [x] Step 3: Remove `UseSyntheticRuntimeForTests` from `AppHostSettings.cs` and remove the synthetic branch from `AppHost.cs` once replacement coverage is in place.
    - [x] Step 4: Remove any AppHost-driven in-memory persistence substitution that exists only to support the synthetic distributed test path.
  - [x] Task 3: Align supporting assets
    - [x] Step 1: Update affected test comments to explain the real Aspire-backed validation intent.
    - [x] Step 2: Update `docs/wiki/` when the testing approach or local validation guidance changes.
    - [x] Step 3: Confirm no AppHost-backed distributed test still enables the synthetic runtime path.
  - [x] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: remove synthetic distributed runtime branching.
    - `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs`: remove synthetic-runtime settings that no longer belong to the product AppHost.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`: migrate to real AppHost-backed auth validation.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`: migrate to real AppHost-backed Web auth validation.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs`: migrate to real AppHost-backed protected-route validation.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/*.cs`: align reusable real-runtime startup or sign-in helpers as needed.
  - **Work Item Dependencies**: Depends on Work Item 1 extraction so the distributed topology is stable before the test-mode branch is removed.
  - **User Instructions**: Expect AppHost-backed distributed tests to require the real Aspire-managed integration path rather than an in-memory substitute path.

  Execution note:
  - Investigated a real-runtime migration path for the Web functional auth suite and confirmed the repository's direct AppHost process plus Keycloak browser flow works interactively, but the non-browser HTTP helper for replaying the live Keycloak login POST was not stable enough to keep in the repository. The probe was reverted so the current automated baseline remains green; Work Item 2 still needs a durable real-runtime helper strategy before the synthetic branch can be removed safely.
  - Added real AppHost-process plus Playwright coverage for the protected configuration route in the Web E2E suite, including the allowed operator path and the denied viewer path through the live Keycloak sign-in experience. Targeted validation for those new real-runtime cases passed, establishing a working browser-driven pattern for migrating protected Web auth checks away from the synthetic distributed path.
  - Converted `PlatformAuthenticationE2ETests` away from `DistributedApplicationTestingBuilder` synthetic mode to the same real AppHost-process plus Keycloak browser flow used by the dashboard auth suite. A follow-up grep confirms the remaining Web-side synthetic `AppHost__UseSyntheticRuntime=true` usages are now limited to the two functional auth suites, which narrows the remaining migration work to those distributed HTTP-based tests.
  - Revalidated the converted Web E2E auth suite with a focused `PlatformAuthenticationE2ETests` run and then a full `TNC.Trading.Platform.Web.E2ETests` run. Both passed after aligning the root re-entry assertions to the delivered Keycloak re-authentication behavior and the no-role access-denied URL shape.
  - Added a serialized authentication collection for the Web functional auth project so process-wide auth environment switches can be changed safely during migration. With that in place, the anonymous protected-route redirects (`/status`, `/configuration`, `/administration/authentication`) and the anonymous root-entry redirect (`/`) were moved to real AppHost runtime settings and validated with focused functional test runs.
  - Extended the real-runtime functional migration to two more anonymous cases in `PlatformAuthenticationFunctionalTests`: the legacy GET sign-out route now validates as `404 Not Found` against the real AppHost runtime, and the external `returnUrl` normalization check now validates the OIDC challenge path without reflecting the attacker URL. The remaining Web functional auth cases that still depend on the synthetic path are the ones that require successful authenticated session creation, sign-out completion, or audit verification through a non-browser HTTP client.
  - Added a Playwright-backed real-auth session helper plus an AppHost-process functional startup path so signed-in functional tests can establish real Keycloak-backed browser sessions against the fixed local redirect URI trusted by the seeded realm. Using that path, the Web functional suite now has focused passing coverage for real operator configuration access, sign-out antiforgery rejection, viewer sign-in audit visibility, configuration reauthentication after session loss, sign-out audit visibility, post-sign-out protected-route re-challenge, and the no-role access-denied redirect on `/status`.
  - Migrated the remaining protected-route role matrix and additional root/sign-out Web functional cases to the real Keycloak-backed path, replacing the last synthetic sign-in-page assertion with a direct OIDC-challenge assertion. The earlier stale-cookie functional case was removed after confirming it no longer exercised the intended missing-token branch; that boundary now remains covered by the lower-level token-usability tests plus the real-runtime browser re-entry functional check.
  - Consolidated the Web functional real-runtime startup path onto the shared `RealAppHostProcessFactory` helper so `PlatformAuthenticationFunctionalTests` no longer carries a duplicate local AppHost process bootstrap implementation.
  - Added a local-only Keycloak API test client in the seeded realm plus a real bearer-token helper for the API integration suite. After recycling the persistent Keycloak container so the updated realm import reapplied, focused API integration tests for viewer access to `/api/platform/status`, viewer denial on `/api/platform/configuration`, operator writes to `/api/platform/configuration`, operator access to `/api/platform/auth/manual-retry`, viewer access to `/api/platform/events?category=auth`, administrator access to `/api/platform/auth/administration`, operator denial on the administrator endpoint, no-role denial on `/api/platform/status`, and the authenticated auth-audit write/read flows all passed with real Keycloak-issued tokens instead of synthetic JWTs.
  - Migrated the invalid-signature API negative to the real Keycloak-backed runtime and validated that the protected API returns `401 Unauthorized` for a handcrafted bearer token even when the real provider branch is active. This leaves one proven real-runtime forged-token negative without changing the delivered API behavior.
  - Removed the class-level synthetic runtime default from `PlatformAuthenticationIntegrationTests` so the API integration suite now boots the real AppHost path by default. Only the remaining issuer, audience, and expiry negatives explicitly switch back to the synthetic runtime, which narrows the remaining test-harness dependency and keeps the mixed suite easier to reason about.
  - Split the remaining synthetic-token negatives into a dedicated `PlatformAuthenticationSyntheticTokenIntegrationTests` class and extracted shared readiness or runtime switching into a small helper. The main `PlatformAuthenticationIntegrationTests` class now contains only the real-runtime and real-runtime-probe cases, while the synthetic issuer, audience, and expiry checks are isolated in their own closed-box test slice.
  - Removed the redundant real-runtime wrapper from the main API integration class after that split. Those tests now execute the default real AppHost path directly, while the helper only remains for the intentionally synthetic token-negatives file.
  - Probed a real-runtime invalid-audience migration by adding a second local-only Keycloak test client without the API audience mapper, but the experiment had to be reverted because the currently persisted local Keycloak container could not be refreshed non-interactively in this environment. The focused test failed at token acquisition with `401 invalid_client`, which confirms the stale container state rather than an API authorization defect.
  - The remaining API auth tests still using the synthetic token path are now intentionally limited to the invalid issuer, invalid audience, and expired token negatives. The current decision is to keep those isolated and synthetic unless the repository later gains a refreshable Keycloak test environment or another controlled way to mint selectively invalid provider-shaped tokens; the real provider path does not expose a straightforward way to create those cases on demand.
  - After Docker health recovered, the post-split API auth validation passed for both files: the main real-runtime `PlatformAuthenticationIntegrationTests` slice passed in full and the isolated synthetic-token slice passed in full, confirming the split and wrapper removal did not change the expected auth outcomes.
  - Removed the remaining class-level synthetic runtime defaults and `ExecuteWithRealRuntimeSettingsAsync(...)` wrappers from `PlatformAuthenticationFunctionalTests` and `PlatformProtectedRouteFunctionalTests`. A residue grep now shows those Web functional auth files no longer set `AppHost__UseSyntheticRuntime=true`, and focused anonymous real-runtime checks for `/` and `/status` passed after the cleanup.
  - A follow-up full run of the two Web functional auth files still failed in the real signed-in browser-session path, including cookie-backed re-entry, protected-page retrieval, antiforgery extraction, and some role-matrix cases. That failure shape points to the existing real-session helper or session propagation path rather than the removed synthetic-mode wrappers, so the next Web slice should target `RealAuthenticationSessionFactory` and the downstream cookie/session assumptions rather than restoring the old env toggles.
  - A deeper retry against the real sign-in path showed the remaining blocker is broader than `CookieContainer` hand-off alone. The representative signed-in Web functional case still stalled around the post-login callback, and a neighboring real Web E2E sign-in case also failed to render the protected page after Keycloak authentication. The earlier wrapper cleanup remains valid and the anonymous real-runtime cases still pass, but the next investigation needs runtime-level evidence for the current `/signin-oidc` transition rather than more synthetic-toggle cleanup.
  - Browser-level inspection of a fresh `/signin-oidc` failure exposed the concrete runtime exception: the Web app was throwing `TimeoutRejectedException` from `PlatformAuthAuditClient.RecordAsync(...)` during `OnTicketReceived`, which turned the auth-audit side effect into a `500 Internal Server Error` on the OIDC callback. The Web auth path is now hardened so resilience-layer timeouts are caught in `PlatformAuthAuditClient`, and `OnTicketReceived` also logs and continues if audit recording still throws. That should let sign-in complete even when the auth-audit API side effect is degraded.
  - Stabilized the shared `RealAuthenticationSessionFactory` helper by waiting for the real platform session cookie before copying the browser session into the functional `CookieContainer`. With that handoff fixed, the previously failing signed-in configuration functional case passed against the live AppHost-plus-Keycloak path.
  - Revalidated the full `TNC.Trading.Platform.Web.FunctionalTests` project after the helper change. All 29 Web functional tests passed against the real AppHost-plus-Keycloak runtime, including the protected-route role matrix, sign-out, audit visibility, and reauthentication coverage.
  - Revalidated the full `TNC.Trading.Platform.Web.E2ETests` project after the helper change. All 12 browser-based auth E2E tests passed against the real AppHost-plus-Keycloak runtime, confirming the sign-in entry, protected route, access-denied, and sign-out flows remain stable.
  - Confirmed there are no remaining exact `AppHost__UseSyntheticRuntime=true` or `UseSyntheticRuntimeForTests` usages anywhere under `src/` or `test/`, so the distributed test suites no longer enable the synthetic AppHost runtime path.
  - Hardened `AppHostProcessHandle` so real-runtime Web functional discovery also consumes the AppHost process output and reuses announced listener URLs instead of relying only on TCP port polling. That removed the remaining listener-discovery timeout flake from the protected-route functional matrix.
  - Completed the Work Item 2 pre-completion validation gate with a successful `dotnet build` plus passing runs of `TNC.Trading.Platform.Application.UnitTests`, `TNC.Trading.Platform.Infrastructure.UnitTests`, `TNC.Trading.Platform.Api.UnitTests`, `TNC.Trading.Platform.Web.UnitTests`, `TNC.Trading.Platform.Api.IntegrationTests`, `TNC.Trading.Platform.Web.FunctionalTests`, and `TNC.Trading.Platform.Web.E2ETests`.

### Work Item 3 details

- [x] Work Item 3: Align supporting documentation and final validation
  - [x] Build and test baseline established
  - [x] Task 1: Prepare the safety net
    - [x] Step 1: Confirm which local-development and testing wiki pages still describe synthetic or substitute distributed runtime expectations.
    - [x] Step 2: Confirm the final AppHost shape and test workflow so documentation updates describe the delivered result instead of the intended result.
  - [x] Task 2: Align supporting assets
    - [x] Step 1: Update `docs/wiki/testing-and-quality.md` to describe AppHost-backed distributed validation through Aspire-managed integrations.
    - [x] Step 2: Update `docs/wiki/local-development.md` if the refactored AppHost changes the documented local validation or startup expectations.
    - [x] Step 3: Update any affected work-package notes if the final validation model changes materially.
  - [x] Task 3: Final validation
    - [x] Step 1: Re-run the full build and relevant automated suites.
    - [x] Step 2: Run one manual AppHost validation through API, Web, Keycloak, and Mailpit.
    - [x] Step 3: Verify affected wiki links still resolve after the updates.
  - [x] Build and test validation

  - **Files**:
    - `docs/wiki/testing-and-quality.md`: document the distributed Aspire integration test model clearly.
    - `docs/wiki/local-development.md`: align local startup or validation notes if needed.
    - `docs/005-refactor-app-host/plans/001-delivery-plan.md`: keep the plan aligned with delivered sequencing if implementation details shift.
  - **Work Item Dependencies**: Depends on Work Item 2 so documentation reflects the final AppHost and distributed testing model.
  - **User Instructions**: Re-run the documented local validation steps after implementation to confirm the wiki still matches the delivered runtime.

  Execution notes:
  - Reused the successful Work Item 2 pre-completion validation gate as the Work Item 3 baseline because no code, test, configuration, or documentation changes had been made since that green build-plus-test run.
  - Confirmed `docs/wiki/testing-and-quality.md` and `docs/wiki/local-development.md` are the primary wiki pages affected by this work item because they still describe the distributed AppHost validation approach and local runtime expectations.
  - Confirmed the delivered AppHost shape remains a thin composition root with extracted infrastructure, project registration, and shared environment wiring support files, while AppHost-backed distributed validation now uses the real Aspire-managed AppHost-plus-Keycloak runtime rather than the removed synthetic runtime branch.
  - Updated `docs/wiki/testing-and-quality.md` to document the delivered real-runtime distributed validation model more explicitly, including Docker-backed infrastructure coverage, listener discovery from AppHost output, and the remaining manual checks that still add value.
  - Updated `docs/wiki/local-development.md` to document the final manual validation sequence through AppHost-exposed API, Web, Keycloak, Mailpit, and Scalar surfaces and to state explicitly that AppHost-backed local validation no longer has a supported synthetic runtime path.
  - Re-ran the required validation gate after the documentation updates: `dotnet build` succeeded; `TNC.Trading.Platform.Application.UnitTests`, `TNC.Trading.Platform.Infrastructure.UnitTests`, `TNC.Trading.Platform.Api.UnitTests`, `TNC.Trading.Platform.Web.UnitTests`, `TNC.Trading.Platform.Api.IntegrationTests`, and `TNC.Trading.Platform.Web.FunctionalTests` all passed via `dotnet test`; and `TNC.Trading.Platform.Web.E2ETests` passed in full through the test runner after a terminal cancellation interrupted the direct command invocation.
  - Started the live AppHost with `aspire run` and validated the delivered runtime surfaces from the running local topology: API liveness and readiness returned `200`, the protected API status endpoint returned `401` while signed out, the Web root and protected route returned `302` into the sign-in flow while signed out, Keycloak returned its expected redirect on `http://localhost:8080/`, Scalar returned `200` on `/scalar/v1`, and Mailpit returned `200` on the dynamically published AppHost container port.
  - Verified the affected wiki cross-links still resolve by confirming the referenced `docs/wiki/README.md`, `docs/wiki/application-overview.md`, `docs/wiki/operator-guide.md`, `docs/wiki/runtime-behavior.md`, and `docs/wiki/local-development.md` files are present after the documentation updates.

### Work Item N details (copy/paste)

Copy the **Work Item 1 details** section for each additional work item.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**:
  - `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj`
  - `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj`
  - `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`
- **Integration tests**:
  - `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`
- **Functional tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`
- **E2E tests**:
  - `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj`
- **Manual checks**:
  - Start the AppHost and confirm the API, Web UI, Keycloak, Mailpit, and Scalar links still resolve correctly.
  - Validate seeded-user Keycloak sign-in reaches the protected operator experience.
  - Validate protected API access and protected route behavior through the real Aspire-managed runtime path.
- **Behavior-preservation checks**:
  - The AppHost remains composition-only and continues modeling the same services and resource relationships.
  - API liveness and readiness endpoints remain public and stable.
  - Web sign-in, sign-out, access-denied, and protected route behavior remain unchanged from the operator perspective.
  - No AppHost-backed distributed test depends on in-memory substitute runtime behavior.
- **Security checks**:
  - Confirm no new secrets, connection strings, or machine-specific defaults are hard-coded.
  - Confirm AppHost-backed distributed tests do not use `AppHost__UseSyntheticRuntime=true`.
  - Confirm no AppHost-backed distributed validation uses in-memory persistence as a substitute for real Aspire integrations.

## Acceptance checklist

- [x] Every planned mitigation maps back to one or more findings in the AppHost complexity review.
- [x] High-priority maintainability and change-safety issues are addressed before lower-priority cleanup.
- [x] The plan prefers the smallest safe refactoring that resolves each confirmed issue.
- [x] Required safety-net tests or validation steps are defined for each work item.
- [x] Relevant `docs/wiki/` pages are updated to reflect delivered implementation, architecture, or testing changes.
- [x] Affected wiki links resolve after documentation updates.
- [x] Rollback/backout plan documented for each work item.

## Notes

- This plan is intentionally scoped to the AppHost composition and the connected distributed test model, rather than to broader authentication refactoring already captured in earlier work-package plans.
- The plan assumes AppHost-backed distributed tests should validate the real Aspire-managed topology and that in-memory substitute runtime behavior is no longer acceptable for that layer.
- Smaller isolated unit tests can remain unit tests where infrastructure is not part of the contract under test; the constraint here is specifically that distributed AppHost-backed validation must use Aspire integrations.
