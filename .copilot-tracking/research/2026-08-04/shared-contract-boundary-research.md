<!-- markdownlint-disable-file -->
# Shared Contract Boundary Research

- Research date: 2026-08-04
- Scope: Research only. No production or test code was modified.
- Status: Complete
- Requested plan: Option B, add a deliberate shared contract boundary

## User requirement interpretation

The request refers to a contract boundary that removes an accidental host dependency while preserving the existing authentication behavior. The repository uses Option B in a narrower Web organization discussion as well, where it means moving shared Web API view models into `Client/Contracts` or `Contracts`. The two concerns overlap in vocabulary but are not the same change:

* The project-level boundary problem is the temporary `Web -> Application` reference required by shared authentication vocabulary.
* The Web folder-level option is a physical organization of 22 Web transport models and does not, by itself, remove the project reference or establish a new assembly boundary.

This research treats the project-level authentication boundary as the primary Option B target. The Web `Client/Contracts` move is recorded as a separate alternative and should not be combined with the first implementation slice.

## Verified current boundary problem

The Web project directly references Application in `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-8`. The Application project has no production project or package references and targets `net10.0` in `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-9`. The Web reference is therefore an inward-boundary exception, not a dependency required by the Web API client models.

The shared authentication surface currently lives in `src/TNC.Trading.Platform.Application/Authentication/`:

* `PlatformAuthenticationDefaults` is public and owns configuration section names, provider identifiers, cookie/OIDC/bearer scheme names, roles, authorization policies, OAuth scopes, claim names, and audit event names. See `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs:1-151`.
* `PlatformAuthenticationOptions` is public and owns the environment-bound authentication object graph, including Keycloak, Entra, test, and authorization options. See `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationOptions.cs:1-181`.
* `PlatformAuthenticationConfigurationResolver` is public and resolves or validates provider configuration, authority, audience, client ID, client secret, and minimum OIDC configuration. See `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationConfigurationResolver.cs:1-137`.

The API consumes all three types from `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs:1-110` and also uses defaults in `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:1-121` and `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditEventResolver.cs:1-38`.

The Web consumes the same surface across host startup and authentication components. Representative consumers are:

* `src/TNC.Trading.Platform.Web/Program.cs:1-74` uses `PlatformAuthenticationDefaults` for cookie sign-out and imports the Application authentication namespace.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:1-190` binds `PlatformAuthenticationOptions`, configures policies and schemes, and calls the resolver.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs:1-130`, `PlatformNavigationAccessCoordinator.cs:1-52`, and `PlatformOperatorContextAccessor.cs:1-50` use options, schemes, roles, claims, and providers.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:1-100`, `PlatformAuthAuditClient.cs:1-86`, `PlatformTestAuthenticationSignInHandler.cs:1-46`, `PlatformTokenScopeEvaluator.cs:1-70`, and `TestAuthenticationTokenFactory.cs:1-82` use shared scopes, roles, claims, schemes, providers, or audit event identifiers.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:1-129` uses shared scope names for its API calls.

The current architecture rules document this as transitional. `docs/wiki/architecture.md:1-12` says Web retains the Application reference because its authentication components consume Application-owned contracts, and `docs/wiki/architecture.md:184-198` records the explicit migration exception and its intended retirement after host-owned contracts and parity tests exist. `docs/wiki/application-overview.md:1` states the same transitional contract.

The exception is executable in the architecture tests. `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRoleValidatorTests.cs:42-60` asserts that a Web project referencing Application is accepted while shared authentication contracts remain there. The production policy identifies the Web project by name in `ProductionProjectRolePolicy.cs:3-9`; `ProductionProjectRoleValidator.cs:24-49` validates Application and Infrastructure rules but has no separate shared-contract project rule.

The underlying design issue is ownership, not a failing runtime path: host-specific authentication wiring in API and Web depends on a broad Application namespace because the stable cross-host vocabulary, options model, and provider-resolution logic were placed there. The current exception is intentionally allowed, but it keeps the Application-to-host dependency direction ambiguous and prevents retirement of the Web project reference.

## Affected files and types

### Production source

* Application authentication types: `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs`, `PlatformAuthenticationOptions.cs`, and `PlatformAuthenticationConfigurationResolver.cs`.
* API authentication composition and endpoint/audit consumers: `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs`, `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`, `PlatformAuthAuditEventResolver.cs`, and `RecordAuthAuditEventEndpointHandler.cs`.
* Web host and authentication consumers listed above, especially `src/TNC.Trading.Platform.Web/Program.cs`, `PlatformWebAuthenticationServiceCollectionExtensions.cs`, and `PlatformApiClient.cs`.
* Project references: `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-8`, `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:1-8`, and `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-9`.

### Tests

* API authentication configuration and resolver coverage: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs:1-210` and `PlatformAuthAuditEventResolverTests.cs:1-100`.
* Web authentication registration, token/scope, redirect, component, and client coverage: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthenticationRegistrationTests.cs:1-275`, `PlatformAccessTokenProviderTests.cs:1-316`, `PlatformWebAuthenticationServiceCollectionExtensionsTests.cs:1-270`, `PlatformComponentTestContext.cs:1-205`, and `PlatformApiClientTests.cs:1-180`.
* Architecture exception coverage: `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRoleValidatorTests.cs:42-60` and repository-wide invocation in `RepositoryArchitectureRuleTests.cs:1-30`.

### Related but distinct Web ViewModel surface

The prior Web research identifies 22 root `*ViewModel.cs` files, 17 read/response models and five configuration update models. `PlatformApiClient` serializes/deserializes them, while presenters, pages, shell state, `PlatformWebTestData`, and `PlatformApiClientTests` consume them. Evidence: `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-folder-structure-research.md:1-55` and `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-organization-alternatives-research.md:1-38`.

Moving those files to `Client/Contracts` could clarify the Web client boundary, but retaining the current `TNC.Trading.Platform.Web` namespace would not remove the Web-to-Application project reference. Changing namespaces would affect `Components/_Imports.razor`, `PlatformApiClient`, presenters/pages, test data, and client tests. The prior research therefore selected the current root boundary for now and made the physical move a conditional follow-on: `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-organization-alternatives-research.md:40-103`.

## Existing patterns and constraints

* The repository defines Clean Architecture by responsibility and inward source dependencies, not by mandatory project names or folder names. See `.github/instructions/architecture.instructions.md:1-76` and `docs/wiki/architecture.md:31-70`.
* Boundary data should be simple and owned by the inward consumer. External transport, provider, persistence, and UI models must be translated at adapters. This argues against placing host-specific authentication registration extensions in a shared contract assembly. See `.github/instructions/architecture.instructions.md:30-51`.
* Application must remain framework-neutral and must not gain ASP.NET Core, EF, Azure, or other outward package dependencies. The role policy and validator enforce this in `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRolePolicy.cs:10-22` and `ProductionProjectRoleValidator.cs:67-86`.
* The repository follows one top-level C# type per matching file and vertical feature organization. A new contract project should contain focused types, not a generic pass-through service or host registration layer.
* Local authentication is Keycloak-orchestrated and Azure authentication is Microsoft Entra ID. Any extracted contract must preserve the existing OIDC/OAuth vocabulary and provider-specific configuration behavior. The type defaults explicitly name Keycloak, Entra, and Test providers in `PlatformAuthenticationDefaults.cs:19-34`.
* The repository's prior planning decision `PD-09` allows temporary Application ownership, host-local duplication, or a narrowly named shared contract only when the contract is substantial and stable. See `.copilot-tracking/plans/logs/2026-07-26/clean-architecture-migration-log.md:101-103`.
* The Web ViewModel planning evidence rejects a generic `Models` or `ViewModels` folder and requires a reviewed consumer matrix before a named contract boundary is introduced. See `.copilot-tracking/research/subagents/2026-08-04/web-viewmodel-organization-alternatives-research.md:40-103`.

## Implementation alternatives

### A. Keep the current Application ownership

Leave the three authentication types in Application and retain the Web project reference. This is the lowest immediate risk and matches the current transitional architecture, but it leaves the documented exception in place and does not make host/contract ownership explicit.

### B1. Extract a small shared authentication-contract project

Create a deliberately named inward-neutral project, such as `TNC.Trading.Platform.Contracts` or `TNC.Trading.Platform.Authentication.Contracts`, containing only stable cross-host authentication vocabulary and configuration data. API and Web reference the new project. Application removes the three moved types and no longer owns host authentication vocabulary.

The likely first extraction set is `PlatformAuthenticationDefaults` and the option records/classes. `PlatformAuthenticationConfigurationResolver` needs a separate decision: it is shared today, but it contains provider configuration policy and client-secret resolution. It could remain in a host-owned adapter, be split into pure validation/resolution records plus host-specific resolution, or be retained temporarily in Application until parity tests prove the new ownership. Moving it wholesale into a contract assembly would make the assembly a policy/service bucket rather than a pure contract boundary.

This is the strongest interpretation of “deliberate shared contract boundary” because it removes the project-level exception instead of only renaming a folder.

### B2. Move shared Web API models to `Client/Contracts`

Move the 22 Web `*ViewModel` files under a named Web client folder, initially preserving namespaces. This makes `PlatformApiClient` ownership visible and reduces root clutter, but it does not address the Application authentication dependency. It should be a separate Web organization change after the project-level contract decision.

### C. Duplicate authentication constants and options in API and Web

Remove the project reference by duplicating values or creating host-local equivalents. This avoids a new project, but it creates drift risk for schemes, roles, scopes, claim mappings, provider names, and configuration binding. It is inconsistent with the stated need for shared vocabulary and should be rejected unless the product deliberately makes API and Web authentication contracts independent.

### D. Move all authentication code into a new shared project

Move defaults, options, resolver, registration extensions, endpoint routing, token handling, and audit clients into one project. This would produce a larger dependency and framework surface, blur composition-root ownership, and likely violate the framework-neutral Application boundary. Reject it as broader than the contract problem.

## Recommended path

Recommend **B1 in two explicit slices**, with the project named only after an API/architecture review. Prefer `TNC.Trading.Platform.Authentication.Contracts` if the project is limited to authentication, or `TNC.Trading.Platform.Contracts` only if additional stable cross-host contracts are already approved. Do not create a generic `Shared` project.

1. Inventory the three current types and classify each member as shared vocabulary, bindable configuration data, pure resolution policy, or host-specific behavior.
2. Extract only the stable cross-host contract content first. Preserve public type names and member names initially to reduce serialization/configuration and test churn.
3. Keep API and Web authentication registration extensions, endpoint routing, token handling, audit client behavior, and provider-specific framework code in their current host projects.
4. Decide separately whether `PlatformAuthenticationConfigurationResolver` belongs in the contract project. The safer default is to split or retain its host-specific/provider policy rather than move it wholesale.
5. Add the new project references to API and Web, remove Web's direct Application reference if no other Web source usage remains, then update namespaces/usings and test project references as needed.
6. Replace the temporary architecture exception with a rule that permits API/Web references to the named contract project and does not permit Web to reference Application. Update `ProductionProjectRoleValidatorTests` to prove the new rule and reject the old edge.
7. Run focused API, Web, and architecture tests before widening to solution validation. Keep the Web ViewModel `Client/Contracts` move out of this slice.

The recommendation is deliberately incremental because the current resolver mixes contract-shaped options with behavior. A new project should represent a real stable boundary, not become a second Application project under a different name.

## Unresolved questions

* Is the intended Option B the project-level authentication boundary or only the Web `Client/Contracts` folder move? The repository contains evidence for both labels, and they solve different problems.
* Should the extracted project be authentication-specific or a broader platform contract assembly? No approved consumer matrix currently establishes a broader stable contract set.
* Are `PlatformAuthenticationOptions` and its nested provider options intended as shared configuration schema, or should API and Web bind separate host-specific options while sharing only defaults and claim/policy vocabulary?
* Should `PlatformAuthenticationConfigurationResolver` remain in Application, move to a host adapter, or split into pure shared validation and host-specific resolution? The current implementation resolves Web client secrets and API audiences, which are not equally relevant to both hosts.
* Should the new boundary expose public types to both hosts, or should internal types plus explicit friend assemblies be retained? Existing tests rely on project-level access patterns, but no new assembly visibility policy is recorded.
* What exact architecture rule should replace the current Web exception, and should the validator assert that Application has no references to the new contract project or that the contract project has no production references at all?
* Does the future Web ViewModel organization change remain desired after the project-level contract boundary is established? It should not be inferred from this authentication extraction.

## Validation commands

Run from `D:\Repos\TNC.Trading\TNC.Trading.Platform` after implementation. These commands are research-derived and were not run for this research-only task.

```powershell
dotnet build src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj --no-restore
dotnet build src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --no-restore
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore

dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj --no-restore

dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet test TNC.Trading.Platform.slnx --no-restore
```

The repository's documented focused architecture/API commands are recorded in `docs/wiki/testing-and-quality.md:43-84`. The SDK is pinned through `global.json:1-6` to .NET SDK `10.0.200` with `latestFeature` roll-forward.

## Research gaps

No source or test behavior was changed, so the recommendation remains architectural rather than implementation-proven. The main gap is the absence of an approved consumer matrix and ownership decision for the resolver and nested provider options. A follow-on implementation plan should close those questions before creating a project or changing the architecture validator.
