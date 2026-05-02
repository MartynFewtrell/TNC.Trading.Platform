# Work Package Refactoring Review Report

This report reviews the delivered authentication and authorisation work package and identifies evidence-backed refactoring opportunities to improve maintainability, reduce drift between runtime modes, and align the implementation with the now-required Docker plus Keycloak local development model.

## Review scope

- **Work package**: `./docs/003-authentication-and-authorisation/`
- **Review depth**: `standard`
- **Reviewer perspective**: `Senior Refactoring Architect`
- **Reviewed artifacts**:
  - `docs/003-authentication-and-authorisation/requirements.md`
  - `docs/003-authentication-and-authorisation/technical-specification.md`
  - `docs/003-authentication-and-authorisation/plans/001-delivery-plan.md`
  - `docs/wiki/architecture.md`
  - `docs/wiki/local-development.md`
  - `src/TNC.Trading.Platform.AppHost/AppHost.cs`
  - `src/TNC.Trading.Platform.Web/Program.cs`
  - `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs`
  - `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs`
  - `src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs`
  - `src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs`
  - `src/TNC.Trading.Platform.Api/Program.cs`
  - `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs`
  - `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
  - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebAuthenticationServiceCollectionExtensionsTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformAuthenticationE2ETests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`

## Executive summary

- **Overall refactoring urgency**: `high`
- **Overall maintainability assessment**: `mixed`
- **Top concerns**:
  1. The implementation now has two materially different local runtime paths, but the completed delivery and current development expectation require the Docker plus Keycloak path as the real local-dev baseline.
  2. Test-only authentication and persistence behavior remain embedded in product startup and endpoint code, increasing structural drift and making local behavior harder to reason about.
  3. Shared authentication policy and provider-resolution logic is duplicated across Web and API hosts, and documentation still describes an outdated lightweight mode.

## Refactoring opportunity matrix

| Area / requirement | Current implementation evidence | Issue type | Impact assessment | Recommendation |
| --- | --- | --- | --- | --- |
| `IR1`, `OR2` / local auth runtime | `src/TNC.Trading.Platform.AppHost/AppHost.cs`, `docs/wiki/local-development.md`, `docs/wiki/architecture.md`, `docs/003-authentication-and-authorisation/plans/001-delivery-plan.md` | Coupling / Boundary leakage | High: the repository now treats Docker plus Keycloak as the real local runtime requirement, but AppHost and docs still preserve an alternate non-container auth path. | Remove the lightweight local auth branch from normal runtime composition and make Docker-backed Keycloak the single documented local development path. |
| `NF3`, `OR1`, `OR2` / persistence and startup behavior | `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs` | Complexity / Hidden behavior | High: the API silently swaps to in-memory persistence in Development when `platformdb` is absent, which no longer matches the intended local environment. | Remove or isolate the in-memory persistence fallback from production startup and keep it available only through explicit test composition. |
| `FR7`, `FR9`, `NF3` / shared auth registration | `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs`, `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs` | Duplication / Coupling | Medium: policy registration and provider validation logic is duplicated across hosts and can drift over time. | Extract shared authorization policy registration and shared provider-resolution helpers into a common authentication registration module. |
| `TR1`, `TR2`, `TR3` / refactoring safety net | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformAuthenticationE2ETests.cs`, `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs` | Testability / Boundary leakage | High: many automated tests force the non-container `Test` provider branch, so the main safety net does not primarily validate the now-required Docker plus Keycloak local runtime. | Rebalance the test pyramid so test-only branches are isolated and the required local runtime has clearer dedicated coverage and documentation. |
| `NF5`, `OR2` / documentation alignment | `docs/wiki/local-development.md`, `docs/wiki/architecture.md` | Naming / Boundary leakage | High: current documentation still presents Docker as optional and describes in-memory SQL plus test auth as a useful local mode, which conflicts with the delivered auth architecture. | Update the wiki to describe Docker plus Keycloak as required for local development and remove outdated in-memory and lightweight mode guidance. |

## Existing implementation strengths

- Shared constants in `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs` centralize schemes, roles, policies, claims, and scopes well.
- The work package added good requirement-traceable auth tests with clear XML comments in Web and API test projects.
- The AppHost plus Keycloak path is not purely theoretical; `PlatformDashboardAuthenticationE2ETests` exercises a real Docker-backed Keycloak sign-in flow.
- Protected API endpoints are grouped and consistently protected in `PlatformEndpoints`, which keeps authorization intent readable at the endpoint boundary.

## Refactoring findings

### Structural and architectural issues

- **F1**: The AppHost still models local development as two different application topologies. In `src/TNC.Trading.Platform.AppHost/AppHost.cs`, `AppHost:EnableInfrastructureContainers` controls whether SQL Server, Mailpit, and Keycloak are started, and the non-container branch injects `Authentication__Provider=Test` instead of the delivered Keycloak path. This was a useful bootstrap step, but it now conflicts with the completed delivery and the stated local-dev requirement that Docker is needed because Keycloak is part of the runtime boundary.
- **F2**: Persistence startup behavior still contains a hidden development-only branch. In `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`, missing `platformdb` causes the API to fall back to `UseInMemoryDatabase` in Development. Given the current requirement that Docker-backed infrastructure is the local-dev baseline, this fallback no longer supports the intended runtime and instead creates a second unrepresentative behavior path.
- **F3**: Test-only authentication is still embedded inside the product Web host. `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs` contains HTML generation for a local test sign-in page and branches between real OIDC and a synthetic cookie sign-in flow based on configuration. This mixes test harness behavior into normal endpoint registration and makes the Web runtime harder to reason about.

### Duplication and cohesion issues

- **F4**: Shared authorization policy registration is duplicated between Web and API hosts. Both `PlatformWebAuthenticationServiceCollectionExtensions` and `PlatformApiAuthenticationServiceCollectionExtensions` register the same Viewer, Operator, and Administrator policies independently. This increases the risk of policy drift and violates DRY for a security-sensitive rule set.
- **F5**: Provider validation and authority or audience resolution logic is also duplicated across Web and API authentication registration. `ValidateProviderSupported`, `ResolveAuthority`, `ResolveEntraAuthority`, and `ResolveKeycloakAuthority` appear in both host-specific extension classes with only small differences. This duplication makes auth-provider evolution harder and increases the chance that a future provider or config change will be implemented in one host but not the other.

### Complexity and readability issues

- **F6**: `src/TNC.Trading.Platform.AppHost/AppHost.cs` has accumulated too many responsibilities in one top-level file. It currently decides runtime topology, wires SQL, Mailpit, Keycloak, ACS settings, Web auth settings, API auth settings, and test-provider settings in one flow. Even though the file is still short enough to read, it now mixes infrastructure composition, environment selection, auth configuration, and test-mode composition in a way that makes future changes riskier than necessary.
- **F7**: The repository documentation still describes a lightweight local mode that no longer appears to be a strategic runtime. `docs/wiki/local-development.md` describes Docker as optional, and `docs/wiki/architecture.md` still documents a local default topology where the API uses in-memory EF and the Web and API use the test auth provider. That documentation now conflicts with the completed auth delivery and the explicit note that Docker is required because of Keycloak.

### Testability and regression-risk issues

- **F8**: The test safety net is split between a real Keycloak smoke and a broader suite that mainly targets the test-provider branch. `PlatformAuthenticationFunctionalTests`, `PlatformAuthenticationIntegrationTests`, and `PlatformAuthenticationE2ETests` all set `AppHost__EnableInfrastructureContainers=false` in static constructors, while only `PlatformDashboardAuthenticationE2ETests` exercises the Docker plus Keycloak flow. If the repository refactors toward the now-required local runtime, most of the auth test surface will continue validating an implementation branch that local developers should no longer rely on.
- **F9**: Some lower-level tests now validate startup behavior around the synthetic provider instead of the required runtime boundary. For example, `PlatformWebAuthenticationServiceCollectionExtensionsTests` explicitly asserts that the OpenID Connect scheme is absent for the `Test` provider. That is fine as a test-harness check, but it also shows how much behavior is still centered on a product-visible test path rather than on clearly separated test composition.

## Recommendations to improve the current design

1. **Collapse local runtime composition to one development baseline**.
   - Treat Docker plus Keycloak as the required local-dev topology.
   - Remove the AppHost branch that turns the product Web and API into a `Test` provider runtime during normal local execution.
   - Remove or isolate the Development-time in-memory database fallback from application startup.
2. **Extract shared authentication registration logic into reusable building blocks**.
   - Move shared role-policy registration into one common method used by both hosts.
   - Move shared provider validation and authority-resolution logic into a common helper or shared service registration layer so Web and API no longer drift independently.
3. **Move test-only runtime behavior out of product endpoints and startup**.
   - Keep the synthetic sign-in and JWT test provider available for automated tests only through explicit test composition.
   - Do not make the normal AppHost runtime double as both a production-like Keycloak runtime and a test harness.
4. **Refactor AppHost composition into focused methods or modules**.
   - Separate infrastructure resource creation, auth environment wiring, notification wiring, and project composition into dedicated methods.
   - This is a readability and change-safety refactor that should happen after the runtime-mode cleanup so the extracted code reflects the intended topology.
5. **Align wiki and local development guidance to the delivered architecture**.
   - Remove the outdated lightweight mode narrative.
   - State that Docker is required because Keycloak is part of the local auth stack.
   - Remove or clearly demote any mention of in-memory SQL as a local application runtime option.

## Recommended refactoring work items

| Priority | Area | Refactoring type | Recommendation | Expected benefit | Risk / caution |
| --- | --- | --- | --- | --- | --- |
| High | Local runtime topology | Remove duplication / Simplify flow | Remove the AppHost lightweight auth branch and make Keycloak plus Docker the only supported local runtime path. | Reduces runtime ambiguity, aligns implementation with delivered behavior, and simplifies onboarding and support. | Will require coordinated updates to tests and docs that currently assume `AppHost__EnableInfrastructureContainers=false`. |
| High | Persistence startup | Simplify flow / Move responsibility | Remove the Development-time in-memory EF fallback from `AddPlatformInfrastructure` and require SQL-backed configuration for the real app runtime. | Prevents local persistence behavior from diverging from the intended delivered environment. | Some tests may need explicit test-only composition or direct DbContext setup after the fallback is removed. |
| High | Test-only auth composition | Move responsibility / Extract component | Move synthetic test sign-in and test-provider composition out of product endpoint registration and into explicit test harness setup. | Strengthens separation of concerns and makes production startup paths easier to reason about. | Needs careful migration so the current lower-level test suite remains fast and deterministic. |
| Medium | Shared auth registration | Remove duplication / Introduce abstraction | Extract shared policy registration and provider-resolution helpers used by both Web and API auth extensions. | Reduces duplicated security logic and lowers the risk of policy drift. | Keep the shared abstraction narrow and focused on genuine duplication only. |
| Medium | AppHost composition | Extract method / Split responsibility | Break `AppHost.cs` into focused helper methods for infrastructure resources, project registration, and auth environment wiring. | Improves readability and makes future auth or infrastructure changes easier to review. | Do this after the runtime-mode cleanup so the extracted code models the intended final topology. |
| Medium | Wiki and local-dev docs | Simplify flow / Remove dead guidance | Update `docs/wiki/local-development.md` and `docs/wiki/architecture.md` to remove the optional lightweight mode and in-memory SQL guidance. | Reduces operator and contributor confusion and keeps documentation aligned with the delivered system. | Ensure any remaining test-only runtime guidance is clearly described as test harness behavior, not app runtime behavior. |

## Validation and safety recommendations

- Preserve observable behavior for protected routes, delegated token flow, sign-in, sign-out, access-denied handling, and protected API status codes while refactoring internal composition.
- Before removing the current lightweight runtime branch, capture which tests truly need a synthetic auth provider and move those tests to explicit test-host setup rather than relying on AppHost local runtime behavior.
- Keep `dotnet build` and `dotnet test` as the minimum automated gates.
- Retain at least one Docker plus Keycloak end-to-end validation path for the real local runtime after refactoring.
- Re-run manual local validation for the seeded Keycloak users after any runtime-topology or startup refactor.
- Update affected `docs/wiki/` pages before considering any refactoring plan complete.

## Assumptions and missing information

- This review assumes the completed `plans/001-delivery-plan.md` reflects the intended post-delivery architecture more accurately than the older lightweight-mode wiki guidance.
- This review treats the user's note as authoritative for current repository direction: Docker is now required for local development because Keycloak is part of the local runtime, and the local in-memory SQL option is redundant.
- This review assumes retaining a synthetic auth provider for some automated tests may still be useful, but only if it is explicitly isolated from product runtime composition.
- No separate existing refactoring review report was found in the work package folder at review time.

## Suggested next steps

1. Create a refactoring mitigation plan focused first on removing the lightweight local runtime branch and the Development-time in-memory database fallback.
2. Add a follow-on work item to extract shared Web and API authentication registration logic into a common module after the runtime topology is simplified.
3. Update `docs/wiki/local-development.md` and `docs/wiki/architecture.md` as part of the first refactoring slice so contributor guidance matches the supported local runtime.
