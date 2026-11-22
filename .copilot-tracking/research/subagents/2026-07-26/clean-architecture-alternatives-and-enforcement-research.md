<!-- markdownlint-disable-file -->
# Clean Architecture Alternatives and Enforcement Research

## Research status

Complete. Phase 2 alternatives analysis started and completed 2026-07-26.
This was research only; no production code, instructions, project files, or test
projects were changed.

## Research questions

* What production project-reference dependencies are explicitly allowed and forbidden for Application, Infrastructure, API, Web, AppHost, and ServiceDefaults?
* Which instruction owner is preferable: a standalone file, augmentation of the existing architecture instruction, or a scoped companion?
* Which executable enforcement approach fits this .NET repository: an MSBuild/project-reference script or test, NetArchTest, ArchUnitNET, or no executable check?
* How should guidance reconcile Application's current framework dependencies and composition-root exceptions?
* What implementation-ready instruction blueprint and adoption sequence avoid requiring an immediate Domain project or broad refactor?

## Inputs reviewed

* `.copilot-tracking/research/subagents/2026-07-26/uncle-bob-clean-architecture-primary-sources-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/repository-clean-architecture-alignment-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/clean-architecture-instruction-strategy-research.md`

## Repository evidence

### Current production graph

The six production projects and all test projects are listed in
`TNC.Trading.Platform.slnx:1-29`. The direct production graph is:

```text
Application      -> none
Infrastructure   -> Application
API              -> Application, Infrastructure, ServiceDefaults
Web              -> Application, ServiceDefaults
AppHost          -> API, Web
ServiceDefaults  -> none
```

Exact evidence:

* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13`
	contains no `ProjectReference`, but lines 9-11 reference the entire
	`Microsoft.AspNetCore.App` shared framework.
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:9-22`
	references ASP.NET Core, Application, Azure packages, and EF Core providers.
* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:1-25` references
	Application, Infrastructure, and ServiceDefaults at lines 3-7 and declares
	host/framework packages at lines 9-16.
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-19` references
	Application and ServiceDefaults at lines 3-6 and declares OIDC and Radzen
	dependencies at lines 8-11.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-20`
	references API and Web at lines 3-8 and owns Aspire orchestration packages.
* `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:1-21`
	has no project references and owns shared host observability, discovery,
	resilience, and OpenAPI dependencies.

The graph is acyclic and already exhibits the principal inward edge:
Infrastructure depends on Application, while Application does not depend on
Infrastructure. The repository has no architecture test package or dependency
validation script. The nearest prior inventory records the same graph and the
absence of an architecture-test project at
`.copilot-tracking/research/subagents/2026-07-26/solution-dependencies-research.md:136-190`
and
`.copilot-tracking/research/subagents/2026-07-26/solution-dependencies-research.md:365-374`.

### Boundary and composition evidence

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:16-70`
	is an outer adapter registration module. Lines 39-65 bind Application-owned
	ports to EF, notification, HTTP, encryption, and in-memory implementations.
* `src/TNC.Trading.Platform.Api/Program.cs:1-16` is the backend composition root:
	it references Application registration and concrete Infrastructure
	registration to construct the runtime object graph. Lines 18-51 also perform
	startup database and workflow orchestration, so API is not a pure adapter.
* `src/TNC.Trading.Platform.Web/Program.cs:1-22` is the UI composition root. It
	registers authentication, UI components, and HTTP clients. Web reaches API at
	runtime through HTTP/service discovery rather than an API project reference.
* `src/TNC.Trading.Platform.AppHost/AppHost.cs:1-13` is the deployment
	composition root. `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:7-48`
	composes API, Web, SQL, Mailpit, and readiness/runtime references.
* `src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs:1-6`
	and `src/TNC.Trading.Platform.Infrastructure/Properties/InternalsVisibleTo.cs:1-5`
	grant broad friend access. A project-reference check will not detect misuse of
	those internal surfaces, which defines the limit of the first enforcement step.

### Application framework reconciliation

Clean Architecture does not mean that every framework reference must disappear
before dependency direction can be adopted. The current Application project is
the innermost production assembly but is not framework-independent:

* `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35`
	consumes ASP.NET Core `AuthorizationOptions` to share host policy registration.
* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:1-29`
	consumes Microsoft DI and registers an ASP.NET hosted service at line 24.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:1-29`
	consumes `IConfiguration` and `ILogger` directly.

The instruction must distinguish two controls:

1. Production project references can be enforced immediately with the matrix
	 below.
2. Framework/package dependencies inside Application require an incremental
	 exception policy. Existing ASP.NET Core and Microsoft.Extensions uses are a
	 documented baseline, not permission to add EF Core, database providers,
	 Radzen, Aspire, transport SDKs, or new host-specific types. New framework
	 coupling should require explicit justification and should prefer an
	 Application-owned abstraction or movement to an outer registration module.

This avoids falsely claiming present purity while still preventing further
outward drift. Existing exceptions can be reduced feature by feature without
creating a Domain project.

## Production dependency matrix

`Allowed` means a direct production `ProjectReference` is permitted. `Forbidden`
means no direct reference should be added. `Self` is not an edge. The matrix is
a closed allow-list: every unlisted future production project requires an
architecture decision before it can be referenced.

| From project | Application | Infrastructure | API | Web | AppHost | ServiceDefaults |
| --- | --- | --- | --- | --- | --- | --- |
| Application | Self | Forbidden | Forbidden | Forbidden | Forbidden | Forbidden |
| Infrastructure | Allowed | Self | Forbidden | Forbidden | Forbidden | Forbidden |
| API | Allowed | Allowed | Self | Forbidden | Forbidden | Allowed |
| Web | Allowed | Forbidden | Forbidden | Self | Forbidden | Allowed |
| AppHost | Forbidden | Forbidden | Allowed | Allowed | Self | Forbidden |
| ServiceDefaults | Forbidden | Forbidden | Forbidden | Forbidden | Forbidden | Self |

Interpretation and exceptions:

* Application owns use cases, policy, inward contracts, and outbound ports. It
	has no production project dependencies under the current six-project design.
* Infrastructure may depend on Application to implement ports. It must not
	depend on an executable host, UI, deployment root, or shared host defaults.
* API may reference Application to dispatch use cases, Infrastructure solely to
	compose concrete backend adapters and perform adapter-specific startup, and
	ServiceDefaults for host wiring. The API composition-root exception does not
	authorize business rules or general Infrastructure implementation use in
	endpoints.
* Web may reference Application for current shared application/authentication
	contracts and ServiceDefaults for host wiring. It must continue to call API
	over its runtime boundary rather than reference the API or Infrastructure
	assemblies.
* AppHost may reference API and Web because Aspire generates project metadata
	and orchestrates those executables. It must not become a business-policy or
	adapter implementation consumer.
* ServiceDefaults is an outer reusable host utility. It should remain leaf-like
	with no production project references; API and Web consume it, never the
	reverse.
* A direct edge can expose transitive references in modern .NET. Microsoft Learn
	documents that .NET Core and .NET 5+ project references are transitive. The
	allow-list therefore governs direct intent, while source-level checks remain
	useful for preventing API or Web from using transitive implementation types.
* A future Domain project is not implied. If one is later justified, add it by
	decision and update the matrix so Application depends inward on Domain and
	outer projects do not bypass Application without a demonstrated need.

## Instruction ownership alternatives

### Alternative 1: standalone Clean Architecture instruction

Shape: add `.github/instructions/clean-architecture.instructions.md`, either
repository-wide or across all source and test files.

Benefits:

* Gives the Dependency Rule and matrix a prominent, named home.
* Can explain Martin-specific terminology without enlarging the Vertical Slice
	instruction.

Costs and risks:

* Duplicates SOLID, dependency inversion, thin endpoints, transport isolation,
	and validation already owned by `.github/instructions/architecture.instructions.md:1-87`
	and refactoring guidance.
* Conflicts with the preference to update an existing owner in
	`.github/instructions/token-discipline.instructions.md:38-40`.
* A broad `applyTo` spends context on unrelated work; a narrow one starts to
	resemble the scoped companion alternative.

Assessment: not selected. A standalone file is justified only if the rules grow
into a distinct, substantial policy that the existing owner cannot express
concisely.

### Alternative 2: augment the existing architecture instruction

Shape: add one concise Clean Architecture section and the matrix to
`.github/instructions/architecture.instructions.md`; extend its current scope
from `.cs` files to production/test `.csproj` files as well.

Benefits:

* Keeps feature cohesion, CQRS-style operation contracts, responsibility
	placement, and source-dependency direction in one architecture owner.
* Follows `.github/AGENTS.md:5-21`, `.github/lessons.md:1-7`, and
	`.github/instructions/token-discipline.instructions.md:38-51`, which favor one
	stable owner and avoidance of parallel guidance.
* Makes the relationship explicit: Vertical Slice governs feature grouping;
	Clean Architecture governs dependency direction; CQRS governs command/query
	intent. None mandates a four-project folder diagram.

Costs and risks:

* The file is already prescriptive and should be tightened while adding rules
	to avoid instruction growth.
* Its current frontmatter and Scope at
	`.github/instructions/architecture.instructions.md:1-20` omit `.csproj`, where
	the strongest dependency violations occur.
* Layer-specific conditions need precise wording to avoid treating every host
	reference as a violation.

Assessment: selected instruction owner.

### Alternative 3: scoped companion instruction

Shape: keep C# responsibility rules in the existing owner and add a small
companion applying only to `src/**/*.csproj` and possibly `test/**/*.csproj`.

Benefits:

* Gives project edits a precise matrix with minimal irrelevant context.
* Keeps executable validation language close to the files that introduce edges.

Costs and risks:

* Splits one architectural invariant across two assets and creates drift risk.
* Project references and source-level boundary ownership often change together,
	so cross-layer work would still need both instructions.
* It adds an asset before instruction length or activation behavior has shown a
	real need.

Assessment: reserve as a fallback. Use it only if broadening the architecture
instruction's `applyTo` is unreliable in the installed host or makes the owner
materially too large.

## Executable enforcement alternatives

Four alternatives were evaluated. Package versions and activity are observations
as of 2026-07-26.

| Approach | What it enforces best | Maturity and availability | Fit and limits | Decision |
| --- | --- | --- | --- | --- |
| MSBuild/project-reference script or test | Exact direct `.csproj` allow-list, unknown production projects, and cycles | Uses SDK/MSBuild XML already present; no runtime package. Microsoft documents `ProjectReference` as the project dependency item and its transitive behavior in .NET Core/.NET 5+. | Small, deterministic, runs without loading application assemblies, and tests the requested matrix directly. It does not inspect namespace/type dependencies, package references, reflection, or friend-assembly usage. | Selected first enforcement step |
| NetArchTest | Namespace/type dependencies and naming/convention rules in compiled assemblies | NuGet `NetArchTest.Rules` 1.3.2, .NET Standard 2.0, MIT, 13.3M total downloads; last NuGet update 2021-05-23. GitHub shows the latest release from 2021 and latest commit about three years old. | Familiar fluent API and broad adoption, but maintenance is dormant and it is indirect for `.csproj` edge validation. Adding it now creates package risk without solving the matrix more directly than XML inspection. | Not selected |
| ArchUnitNET | Rich compiled-code rules across types, members, namespaces, calls, and assemblies | NuGet `TngTech.ArchUnitNET` 0.13.3, .NET Standard 2.0/.NET Framework 4.6.2, Apache-2.0, 7.9M total downloads; updated about five months before research. GitHub shows active commits in July 2026, 57 releases, .NET 10 test activity, and xUnit/NUnit/MSTest/TUnit integrations. | Most capable and actively maintained library. It analyzes binaries, recommends Debug test runs, and has more API/maintenance overhead than needed for six direct project edges. It becomes attractive when enforcing namespace, member, call, or friend-access rules. | Preferred future library if source-level enforcement is justified |
| No executable check | Review-time instruction only | No dependency, setup, or runtime cost. | The current graph is correct, but `dotnet build` accepts a newly added forbidden reference. Code review and Copilot wording cannot reliably prevent drift. | Rejected |

Sources:

* Microsoft Learn, [Common MSBuild Project Items](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items?view=vs-2022),
	documents `ProjectReference`, metadata, resolution, and transitive references.
* Microsoft Learn, [MSBuild Task](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-task?view=vs-2022),
	documents project/solution invocation and `.slnx` support in MSBuild 17.12+.
* [NetArchTest NuGet package](https://www.nuget.org/packages/NetArchTest.Rules)
	and [NetArchTest source](https://github.com/BenMorris/NetArchTest).
* [ArchUnitNET NuGet package](https://www.nuget.org/packages/TngTech.ArchUnitNET)
	and [ArchUnitNET source](https://github.com/TNG/ArchUnitNET).

The lightweight check can be implemented either as a repository script that
parses each production `.csproj` with an XML API or as a focused automated test.
It should normalize paths, compare every direct edge with the closed allow-list,
fail on an unknown production project, and detect cycles. It should not use
regular expressions to parse XML. A build-integrated MSBuild target offers the
earliest feedback but can be harder to diagnose and maintain than a focused
test/script; prototype both forms before choosing the permanent entry point.

## Selected approach

Select a two-part approach:

1. Augment `.github/instructions/architecture.instructions.md` as the single
	 normative owner. Cover C# and project files, state the closed production
	 dependency matrix, define boundary ownership and composition-root exceptions,
	 and document Application's current framework baseline without normalizing it.
2. Add a dependency-light, XML-based project-reference allow-list check and run
	 it with normal validation. Prefer an existing repeatable validation surface;
	 because none exists today, prototype a focused test versus an MSBuild check
	 and retain the version with the clearest local and CI failure output.

Rationale:

* It enforces the highest-value invariant already represented by assemblies and
	catches the likely regression before introducing a new architecture library.
* It aligns with the repository preference for existing owners and repeatable
	scripts or tasks in `.github/copilot-instructions.md:34-39` and
	`.github/instructions/token-discipline.instructions.md:22-36`.
* It preserves legitimate composition: API may know concrete Infrastructure at
	startup, Web may wire UI/authentication frameworks, and AppHost may reference
	executable projects. Inner policy still cannot source-depend on those roots.
* It does not demand a Domain project, rename existing projects, move every
	policy immediately, or remove all framework references in one change.
* ArchUnitNET remains a credible second-stage tool if later work needs to detect
	forbidden namespaces, direct type calls, broad `InternalsVisibleTo` use, or
	framework types crossing Application boundaries. NetArchTest is not preferred
	for new adoption because its public release activity is stale.

## Implementation-ready instruction blueprint

### Ownership and scope

Update `.github/instructions/architecture.instructions.md`. Recommended
frontmatter intent:

```yaml
description: 'Enforces Vertical Slice, CQRS-style contracts, and inward Clean Architecture dependencies.'
applyTo: 'src/**/*.cs, src/**/*.csproj, test/**/*.cs, test/**/*.csproj'
```

Before implementation, verify that the installed customization host activates
all comma-separated patterns. If it does not, use a supported brace/array form
or the scoped `.csproj` companion rather than broadening to `**/*`.

### MUST

* MUST keep direct production project references within the matrix in this
	research. Treat it as a closed allow-list and update it through an explicit
	architecture decision when projects are added or responsibilities change.
* MUST keep Application independent of Infrastructure, API, Web, AppHost, and
	ServiceDefaults projects. Keep use cases, policy, inward boundary models, and
	ports in Application while no Domain project exists.
* MUST place persistence, broker, notification, identity-provider, HTTP SDK, and
	other external adapter implementations in Infrastructure or the responsible
	outer host. Implement Application-owned ports there.
* MUST translate HTTP, UI, persistence, provider, and SDK representations at
	boundaries. Do not pass EF entities/change tracking, `HttpContext`, MVC results,
	Radzen models, database rows, or external SDK response types into use cases.
* MUST keep API, Web, and AppHost composition roots limited to registration,
	configuration, adapter selection, startup orchestration, and runtime hosting.
	Their permission to reference inward contracts or concrete adapters does not
	move business decisions outward.
* MUST keep runtime call direction separate from source dependency direction.
	When Application must invoke an outer adapter, Application owns the narrow
	interface and the outer project implements it.
* MUST preserve Vertical Slice cohesion inside the responsible layer. Clean
	Architecture controls boundary direction; it does not require one project or
	folder per use case.

### SHOULD

* SHOULD keep new Application code free of ASP.NET Core and
	Microsoft.Extensions host types. Prefer plain inputs, `TimeProvider`, and
	narrow Application-owned abstractions when they protect policy from host
	details.
* SHOULD treat current Application ASP.NET Core, DI, configuration, and logging
	dependencies as named migration candidates. Reduce them when touching the
	owning feature, but do not block unrelated work on a broad refactor.
* SHOULD define an interface only where a boundary, substitution need, or
	dependency inversion requires it. Keep the interface with its consumer.
* SHOULD test Application policy and use cases without starting ASP.NET Core,
	Aspire, a database, a container, a broker, or a UI. Test adapters and host
	wiring separately.
* SHOULD extract a Domain project only when independently stable domain policy,
	reuse, or compile-time isolation justifies the new boundary.

### MUST NOT

* MUST NOT add Application references to outer production projects or add
	ServiceDefaults references to product/application projects.
* MUST NOT interpret the API composition-root exception as permission for
	endpoints to query EF Core, call Infrastructure services directly, or contain
	business workflows.
* MUST NOT add EF Core, database providers, Aspire, Radzen, transport SDKs, or
	provider-specific authentication types to Application contracts.
* MUST NOT require canonical `Domain`, `Entities`, `UseCases`, `Adapters`,
	`Core`, or `SharedKernel` projects/folders without a demonstrated boundary.
* MUST NOT equate Clean Architecture with CQRS libraries, MediatR,
	microservices, event sourcing, repositories for every entity, or exactly four
	projects.
* MUST NOT add speculative ports, marker interfaces, or pass-through services
	solely to make the project graph resemble a diagram.

### Validation checks

* Run the project-reference allow-list check and fail on a forbidden edge,
	unknown production project, or cycle.
* Build `TNC.Trading.Platform.slnx` to validate all project references and
	composition roots.
* Run focused tests for changed Application behavior and affected adapter/host
	wiring. Build success alone does not prove dependency direction.
* Review changed Application `.csproj` package/framework references and source
	imports for new outer-framework coupling.
* Review public/internal Application boundary types for framework, persistence,
	UI, and SDK types.
* Review API/Web handlers and composition modules separately: concrete adapter
	access may be valid in composition but invalid in request handling.

### Examples

Allowed dependency inversion:

```text
Application: IPlatformConfigurationStore
Infrastructure: SqlPlatformConfigurationStore implements IPlatformConfigurationStore
API composition root: registers SqlPlatformConfigurationStore for IPlatformConfigurationStore
```

Forbidden reversal:

```text
Application handler -> Infrastructure SqlPlatformConfigurationStore
Application request -> Entity Framework entity
Web project -> API project for shared transport models
```

Allowed composition-root exception:

```text
API Program.cs -> Application registration + Infrastructure registration
AppHost -> API and Web executable project metadata
```

The exception applies to wiring and startup only. It does not make API or
AppHost an owner of application policy.

## Incremental sequencing

1. Record the matrix and composition-root exceptions in the existing
	architecture instruction. Broaden its scope only enough to cover `.csproj`
	edits and add the stable lesson that dependency invariants, not canonical
	folder names, define adoption.
2. Add and run the XML-based production project-reference allow-list check
	against the graph that already passes today. Include unknown-project and
	cycle failures so the check cannot silently age out.
3. Document Application's existing ASP.NET Core/Microsoft.Extensions uses as a
	temporary baseline. Add a review rule against new framework categories rather
	than failing immediately on existing references.
4. When modifying an affected feature, move host registration, options binding,
	logging/configuration adaptation, or infrastructure-owned policy one small
	responsibility at a time. Keep behavior tests green after each move.
5. Reassess ArchUnitNET only when project-edge enforcement proves insufficient,
	for example when broad friend access or namespace/type dependencies cause an
	actual regression. Add a narrow rule set rather than a general convention
	suite.
6. Consider a Domain project only after cohesive policy has a demonstrated need
	for independent compilation, reuse, release, or protection from Application
	orchestration. Update the matrix and wiki through a deliberate architecture
	change at that time.

This sequence establishes a ratchet: no new reversed project edges now, no new
unjustified Application framework coupling, and opportunistic reduction of
existing exceptions. It does not demand an immediate Domain project, broad
namespace migration, host rewrite, or package purge.

## Gaps and clarifying questions

* The installed VS Code host's exact support for comma-separated `applyTo`
	patterns was not tested. This affects whether augmentation alone covers
	`.csproj` files or needs a scoped companion.
* CI workflow entry points were not inspected in this bounded phase. The
	permanent allow-list check should be attached to an existing build/test task
	if one already runs for every pull request.
* Research did not prototype the XML check, so failure-message quality and the
	best home (MSBuild target, focused test, or repository script) remain to be
	compared empirically.
* The team has not declared whether the current Application ASP.NET dependency
	is a deliberate permanent compromise or migration debt. The blueprint safely
	treats it as a baseline exception either way.
* A project-edge check cannot detect forbidden source use enabled by transitive
	references or `InternalsVisibleTo`. ArchUnitNET or a Roslyn analyzer may be
	warranted if those become demonstrated failure modes.
* Package activity and download counts are time-sensitive and should be
	rechecked immediately before adding a third-party dependency.

## Recommended next research

* [ ] Prototype an XML-based allow-list check as both a focused test and an
	MSBuild target; compare local/CI failure output, maintenance cost, and speed.
* [ ] Verify `applyTo` activation for `.cs`, `.csproj`, and test files in the
	installed VS Code version before editing the instruction frontmatter.
* [ ] Inventory CI workflows and existing validation tasks to select the
	permanent execution point for the dependency check.
* [ ] Create a bounded inventory of Application's ASP.NET Core and
	Microsoft.Extensions uses and classify retain/move/abstract decisions.
* [ ] Prototype one ArchUnitNET rule only if a source-level boundary that the
	project-reference check cannot cover is selected for enforcement.