<!-- markdownlint-disable-file -->
# Implementation Details: Option B Shared Contract Boundary

## Context Reference

Sources: .copilot-tracking/research/2026-08-04/shared-contract-boundary-research.md; .github/instructions/architecture.instructions.md; .github/copilot-instructions.md; docs/wiki/architecture.md; docs/wiki/testing-and-quality.md

## Implementation Phase 1: Contract Inventory and Project Design

<!-- parallelizable: false -->

### Step 1.1: Classify the current authentication surface

Create a member-level inventory for the three Application-owned authentication types. Classify constants, option records, nested provider options, validation rules, secret resolution, and provider-specific behavior as shared contract data or host/policy behavior. Record the result in the implementation work item before moving code. The matrix must include the type/member, current consumers, proposed owner, dependency direction, framework or secret dependencies, compatibility risk, and approving test. Do not create the contract project until the matrix has an explicit decision for every public member and an approval gate has been recorded in the planning log.

Files:
* src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs - shared authentication vocabulary
* src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationOptions.cs - bindable authentication configuration shape
* src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationConfigurationResolver.cs - mixed resolution and validation behavior
* .copilot-tracking/plans/logs/2026-08-04/option-b-shared-contract-boundary-log.md - record the approved ownership matrix outcome and gate

Discrepancy references:
* Addresses the resolver and nested-options research gap without assuming that all current Application code belongs in a contract assembly.

Success criteria:
* Every public member has an explicit destination or retention decision.
* The proposed project contains no host registration, endpoint routing, token handling, audit client, or provider framework code.

Context references:
* .copilot-tracking/research/2026-08-04/shared-contract-boundary-research.md (Verified current boundary problem, Existing patterns and constraints, Recommended path)

Dependencies:
* Research document reviewed
* Ownership matrix approved and recorded in the planning log

### Step 1.2: Select and register the contract project shape

Create a narrowly named contract project, preferring TNC.Trading.Platform.Authentication.Contracts unless an approved consumer matrix demonstrates that additional non-authentication contracts are stable and belong in a broader assembly. Target the repository SDK and keep the project framework-neutral with no ASP.NET Core, EF, Azure, or provider package references.

Files:
* src/TNC.Trading.Platform.Authentication.Contracts/TNC.Trading.Platform.Authentication.Contracts.csproj - new shared contract project
* TNC.Trading.Platform.slnx - add the project to solution orchestration
* test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRolePolicy.cs - classify the new project if the validator requires explicit role metadata
* test/TNC.Trading.Platform.Authentication.Contracts/TNC.Trading.Platform.Authentication.Contracts.UnitTests/TNC.Trading.Platform.Authentication.Contracts.UnitTests.csproj - test project for stable contract invariants

Success criteria:
* The project builds independently with only approved framework references.
* The project name communicates authentication-contract ownership and is not a generic Shared or Models project.
* Contract tests can assert stable defaults, option shape, and provider identifiers without depending on API or Web composition roots.

Context references:
* .copilot-tracking/research/2026-08-04/shared-contract-boundary-research.md (Implementation alternatives, Recommended path)
* .github/instructions/folders.instructions.md (top-level structure)

Dependencies:
* Step 1.1 classification

## Implementation Phase 2: Extract Stable Contracts and Rewire Consumers

<!-- parallelizable: false -->

### Step 2.1: Move stable authentication contracts

Move or recreate PlatformAuthenticationDefaults and the contract-shaped portions of PlatformAuthenticationOptions in the new project, preserving public type and member names initially to minimize configuration and test churn. Keep namespaces explicit and consistent with the new assembly. Split PlatformAuthenticationConfigurationResolver if its pure validation can be shared; otherwise retain provider/client-secret policy in the appropriate host or Application adapter and expose only contract data to both hosts.

Files:
* src/TNC.Trading.Platform.Authentication.Contracts/Authentication/PlatformAuthenticationDefaults.cs - extracted shared vocabulary
* src/TNC.Trading.Platform.Authentication.Contracts/Authentication/PlatformAuthenticationOptions.cs - extracted shared configuration shape
* src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationConfigurationResolver.cs - retained or reduced policy owner, according to Step 1.1
* src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs - remove after references are migrated
* src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationOptions.cs - remove after references are migrated

Discrepancy references:
* DD-01: resolver extraction is staged rather than moving the mixed policy type wholesale.

Success criteria:
* Shared types compile from the new project and Application no longer owns host authentication vocabulary.
* Provider-specific resolution and secret handling remain behind an adapter or host boundary.

Context references:
* .copilot-tracking/research/2026-08-04/shared-contract-boundary-research.md (Affected files and types, Recommended path)
* .github/instructions/configuration.instructions.md (secret handling boundary)

Dependencies:
* Phase 1 project shape and ownership decision

### Step 2.2: Rewire API, Web, and their tests

Reference the new project from API and Web, remove Web's direct Application reference once no Web source requires it, and update namespaces/usings and test project references. Preserve host-local authentication registration, endpoint routing, token providers, audit clients, and API client behavior.

Files:
* src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj - reference shared contracts as needed
* src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj - replace the temporary Application reference
* src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs - consume extracted contracts
* src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs - consume extracted contracts and retained resolver adapter
* src/TNC.Trading.Platform.Web/Program.cs - consume shared defaults without Application reference
* src/TNC.Trading.Platform.Web/PlatformApiClient.cs - preserve shared scope usage
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/*.cs - update project references/usings and preserve resolver/audit coverage
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/*.cs - update project references/usings and preserve authentication/client coverage

Success criteria:
* API and Web compile using the deliberate contract boundary.
* Existing authentication behavior, configuration binding, scopes, claims, policies, and audit identifiers remain unchanged.
* No Web production source or test requires a direct Application reference.

Context references:
* .copilot-tracking/research/2026-08-04/shared-contract-boundary-research.md (Affected files and types)

Dependencies:
* Step 2.1 completed

### Step 2.3: Validate the extraction slice

Run focused builds and API/Web unit tests after rewiring. Correct only isolated namespace, project-reference, or test expectation issues within this slice before changing architecture rules.

Validation commands:
* dotnet build src/TNC.Trading.Platform.Authentication.Contracts/TNC.Trading.Platform.Authentication.Contracts.csproj --no-restore - contract project
* dotnet build src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --no-restore - API consumers
* dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore - Web consumers
* dotnet build src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj --no-restore - remaining Application consumers
* dotnet test test/TNC.Trading.Platform.Authentication.Contracts/TNC.Trading.Platform.Authentication.Contracts.UnitTests/TNC.Trading.Platform.Authentication.Contracts.UnitTests.csproj --no-restore - shared defaults, options, and provider identifier invariants
* dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore - API authentication and audit tests
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore - Web authentication and client tests

Protocol compatibility checks:
* Assert that Keycloak, Entra, and Test provider identifiers, scheme names, role/claim names, authorization policy names, OAuth scopes, and audit event names are unchanged.
* Assert that configuration binding still produces the expected provider option shape and that resolver validation preserves existing authority, audience, client ID, client secret, and minimum OIDC configuration behavior.
* Record OIDC/OAuth 2.0 compatibility as covered by the existing API/Web tests; record SAML 2.0 as unchanged contract compatibility unless a repository SAML implementation is discovered during Step 1.1.

## Implementation Phase 3: Retire the Temporary Architecture Exception

<!-- parallelizable: false -->

### Step 3.1: Replace the Web-to-Application exception

Update the production project role policy and validator so the named contract project is permitted as a neutral dependency, while Web-to-Application is rejected. Add or update architecture tests to prove the accepted API/Web-to-contract references and the rejected old edge. Ensure the contract project has no production references that invert the dependency direction.

Files:
* test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRolePolicy.cs - define contract project role
* test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRoleValidator.cs - enforce dependency direction
* test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRoleValidatorTests.cs - replace transitional exception coverage
* test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/RepositoryArchitectureRuleTests.cs - retain repository-wide rule invocation if required
* docs/wiki/architecture.md - document the deliberate contract boundary and retired exception
* docs/wiki/application-overview.md - remove transitional Web/Application contract wording

Discrepancy references:
* DD-02: Web ViewModel relocation is excluded because it does not control the project-level dependency and would expand this slice.

Success criteria:
* Architecture tests fail if Web references Application for authentication contracts.
* Architecture tests pass when API and Web reference only the named contract project for shared authentication types.
* Wiki documentation describes the new ownership and remaining resolver policy boundary.

Context references:
* .copilot-tracking/research/2026-08-04/shared-contract-boundary-research.md (Verified current boundary problem, Existing patterns and constraints)
* docs/wiki/architecture.md (transitional exception and intended retirement)

Dependencies:
* Phase 2 focused validation passes

## Implementation Phase 4: Final Validation and Handoff

<!-- parallelizable: false -->

### Step 4.1: Run full project validation

Execute the solution build, solution tests, architecture tests, and the focused commands from Phase 2. Confirm the SDK selected by global.json is available and preserve any unrelated working-tree changes.

Validation commands:
* dotnet build TNC.Trading.Platform.slnx --no-restore
* dotnet test TNC.Trading.Platform.slnx --no-restore
* dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj --no-restore

Success criteria:
* Full build and tests pass, including the architecture rule that retires the old exception.
* No Web ViewModel files are moved as part of this plan.
* Any blocker beyond isolated corrections is recorded for follow-on planning.

Dependencies:
* Phases 1 through 3 complete

## Dependencies

* .NET SDK selected by global.json
* Existing restore assets or a permitted restore operation
* Contract ownership decision for nested options and resolver behavior
* Existing API, Web, and architecture test projects

## Success Criteria

* A named, framework-neutral authentication contract assembly is consumed by both API and Web.
* The temporary Web-to-Application project dependency is removed and protected by architecture tests.
* Authentication behavior and configuration semantics remain compatible with local Keycloak and Azure Entra ID flows.
* Documentation and validation commands reflect the new boundary.
