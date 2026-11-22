<!-- markdownlint-disable-file -->
# Clean Architecture Instruction Strategy Research

## Research Scope

Research how a future Clean Architecture instructions file should fit within the repository's GitHub Copilot customization ecosystem.

Questions:

* What existing rules overlap with, conflict with, or omit Clean Architecture concerns?
* Which `applyTo` strategy best balances relevance, enforcement, and token cost?
* What normative language, validation, and durable-lesson patterns should a future instruction follow?
* How should Clean Architecture coexist with Vertical Slice Architecture and CQRS without prescribing cargo-cult folder structures?
* Which of these strategies is preferable: standalone repository-wide rules, augmentation of existing architecture rules, or rules scoped by layer/file pattern?

## Evidence

### Customization ownership and authoring pattern

* `.github/copilot-instructions.md:8-19` makes the repository-wide file the technology and contribution-policy entry point. It already names SOLID, DRY, separation of concerns, and explicit dependencies, so a Clean Architecture file should specialize those principles rather than repeat them.
* `.github/copilot-instructions.md:24-47` requires scoped customization context, reusable behavior as instructions/agents/skills, existing scripts for repeatable validation, thin `Program.cs` startup orchestration, file-and-line evidence, concise responses, and durable-lesson upkeep.
* `.github/AGENTS.md:5-21` establishes instructions as the first-choice asset for stable repository rules and loads relevant scoped instructions before lessons and task-specific assets.
* `.github/AGENTS.md:55-58` and `.github/lessons.md:1-7` require stable learning to be recorded once and kept concise. The existing lesson explicitly favors a shared instruction over patching several agents or prompts.
* Applicable HVE prompt-builder guidance classifies `*.instructions.md` as automatically applied coding standards selected by `applyTo`, recommends scope/conventions/examples/avoidance/validation sections, and warns against broad `applyTo` patterns that consume context on unrelated work. This aligns with the repository's more specific token rules below.

### Existing architectural overlap

* `.github/instructions/architecture.instructions.md:1-20` already owns feature architecture for `src/**/*.cs, test/**/*.cs`, specifically Vertical Slice Architecture and CQRS-style request/response behavior.
* `.github/instructions/architecture.instructions.md:23-49` requires feature folders, one request/response pair per operation, thin endpoints, transport-only DTOs, business logic in handlers or domain services, validation at external boundaries, and direct handler/validator tests.
* `.github/instructions/architecture.instructions.md:67-87` prohibits business, persistence, and integration logic in endpoints; EF entities as DTOs; business behavior in DTOs; multi-operation handlers; business decisions in validators; and generic god abstractions. It validates with build and test.
* `.github/instructions/refactoring.instructions.md:22-43` already states several Clean Architecture principles: distinct UI/application/domain/infrastructure/test responsibilities, Single Responsibility, Dependency Inversion, explicit constructor dependencies, narrow abstractions, and testability through dependency injection.
* `.github/instructions/refactoring.instructions.md:45-68` resists cargo-cult architecture by requiring real boundaries or variation points and prohibiting speculative interfaces, base classes, hidden dependencies, and coupling of merely similar concepts.
* `.github/instructions/csharp.instructions.md:20-61` supplies language-level rules, intentional error handling, test updates for changed behavior, and build/test validation. A Clean Architecture instruction should not duplicate naming, nullability, comments, async, or general C# rules.

### Existing cross-cutting constraints

* `.github/instructions/configuration.instructions.md:20-44` requires SQL-backed operator configuration, protected/write-only secrets, configuration changes through the Blazor UI, and explicit application services or repositories rather than scattered persistence. Clean Architecture rules must preserve these business and security constraints while placing interfaces inward and implementations in adapters.
* `.github/instructions/auth.instructions.md:20-56` fixes environment-specific identity providers and standards-based flows while requiring externalized configuration. Provider wiring belongs in host/infrastructure adapters; provider-neutral authorization and application policy can remain inward.
* `.github/instructions/folders.instructions.md:14-46` preserves established top-level roots, places product/test code under `src/` and `test/`, mirrors production structure in tests where useful, and prohibits duplicate roots or bypassing build orchestration. This directly rules out prescribing new `Domain`, `Core`, or `SharedKernel` roots without demonstrated need.
* `.github/instructions/tests.instructions.md:20-70` defines unit, integration, E2E, and functional boundaries. These provide behavioral validation for Clean Architecture boundaries, but no current rule requires executable project-dependency or namespace-dependency tests.

### Token discipline and normative language

* `.github/instructions/token-discipline.instructions.md:14-18` says specific content rules win when multiple instructions apply.
* `.github/instructions/token-discipline.instructions.md:22-36` requires narrow context, bounded reads, patch edits, file-and-line evidence, concise outputs, and durable-lesson updates.
* `.github/instructions/token-discipline.instructions.md:38-51` prefers updating the existing owner instead of adding a parallel file and prohibits broad scanning or full rewrites for small changes.
* `.github/instructions/token-discipline.instructions.md:53-60` requires checking that new guidance does not contradict existing instructions.
* Existing instruction files consistently use `MUST`, `SHOULD`, and `MUST NOT` sections, followed by `Output and Validation (optional)`. A future rule should use `MUST` for dependency direction and boundary invariants, `SHOULD` for context-sensitive placement/design preferences, and `MUST NOT` for forbidden outward coupling or speculative structure.

### Implemented dependency shape

* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:1-16` references Application, Infrastructure, and ServiceDefaults. This is appropriate for an API composition root, although it also owns persistence packages that deserve later review.
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:1-21` references Application, showing an inward dependency from adapter implementation to application contracts.
* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13` has no project reference to API, Web, or Infrastructure. Its ASP.NET Core framework reference means a rule cannot equate Clean Architecture with zero framework references without first deciding whether shared authorization helpers belong there.
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-10` references Application and ServiceDefaults but not Infrastructure, consistent with an outer host consuming inward contracts.
* `docs/wiki/architecture.md:11-36` documents the current project graph: API depends on Application and Infrastructure; Infrastructure depends on Application; AppHost composes API and Web.
* `docs/wiki/architecture.md:93-147` assigns startup/composition to API, use cases and abstractions to Application, and persistence/integrations to Infrastructure. This is the repository-specific vocabulary a future instruction should encode rather than introducing generic folder names.

### Overlaps, conflicts, and omissions

| Classification | Finding | Consequence for future guidance |
| --- | --- | --- |
| Overlap | SOLID, separation of concerns, explicit dependencies, Dependency Inversion, thin endpoints, transport isolation, and narrow abstractions already appear in `.github/copilot-instructions.md:14-19`, `.github/instructions/architecture.instructions.md:23-49`, and `.github/instructions/refactoring.instructions.md:22-43`. | Reference or consolidate these rules; do not restate them in a parallel Clean Architecture file. |
| Potential conflict | Architecture requires a universal feature folder and request/response/handler shape at `.github/instructions/architecture.instructions.md:25-49`, while refactoring prohibits speculative abstractions at `.github/instructions/refactoring.instructions.md:58-68`. | Clarify that Vertical Slice artifacts are created when the operation needs them and that Clean Architecture does not justify empty layers, marker interfaces, or ceremonial projects. |
| Potential conflict | Folder guidance prohibits duplicate roots and boundary-bypassing projects at `.github/instructions/folders.instructions.md:36-46`. | Do not mandate folders named `Domain`, `Core`, `UseCases`, `Adapters`, or `SharedKernel`; describe responsibilities and allowed dependencies instead. |
| Omission | No current rule explicitly prohibits Application from referencing outer projects or defines the API/AppHost composition-root exception. | Add an allowed dependency direction and explicit composition-root wording. |
| Omission | Build and behavior tests are required, but no executable architecture check is specified. | Define a focused dependency validation expectation after selecting a tool or script. |
| Omission | Framework dependence inside Application and ownership of shared authentication policy are undocumented architectural decisions. | Avoid absolute framework-purity language until those decisions are researched and recorded. |
| Omission | `applyTo` excludes project files even though project references establish the strongest layer dependencies. | Include `src/**/*.csproj` and relevant test project files, or introduce one narrow project-dependency companion instruction. |

## Strategy Evaluation

### Strategy 1: Standalone repository-wide Clean Architecture rules

Shape: add `.github/instructions/clean-architecture.instructions.md` with `applyTo: '**/*'` or similarly broad coverage.

Advantages:

* Makes dependency direction visible as a distinct architectural concern.
* Can cover C#, project files, documentation, infrastructure, and tests in one artifact.

Risks:

* Conflicts with `.github/instructions/token-discipline.instructions.md:38-40`, which prefers updating the existing owner.
* Duplicates global SOLID/separation rules and the existing architecture/refactoring files.
* A repository-wide glob loads Clean Architecture guidance for unrelated Markdown, infrastructure, or configuration work, increasing token cost.
* Encourages generic layer/folder prescriptions that may contradict `.github/instructions/folders.instructions.md:16-18` and `:40-46`.

Assessment: viable only if the file contains a genuinely cross-format dependency policy that cannot be owned coherently by the current architecture instruction. It is not the preferred initial strategy.

### Strategy 2: Augment the existing architecture rules

Shape: extend `.github/instructions/architecture.instructions.md` with a concise section on project roles, dependency direction, boundary contracts, adapters, and composition roots. Preserve its current `applyTo: 'src/**/*.cs, test/**/*.cs'`, or add `src/**/*.csproj` if project-reference edits must receive the rules automatically.

Advantages:

* Follows the repository lesson to encode a shared rule once and the token rule to update the existing owner.
* Keeps Vertical Slice/CQRS and Clean Architecture guidance together: slices govern feature cohesion and request flow; Clean Architecture governs dependency direction and responsibility boundaries.
* Minimizes duplicated normative text and makes conflicts easier to detect.
* Allows repository-specific language based on current projects and composition roots.

Risks:

* The architecture file is already prescriptive and will grow unless existing repeated material is tightened.
* Its current `applyTo` misses `src/**/*.csproj`, where project dependency violations are introduced.
* Rules that apply differently to Application and Infrastructure may become conditional prose, reducing precision.

Assessment: preferred near-term strategy. Add only invariant rules and broaden `applyTo` narrowly to `src/**/*.cs, src/**/*.csproj, test/**/*.cs, test/**/*.csproj` if project-reference guidance is included.

### Strategy 3: Scoped rules by layer or file pattern

Shape: split guidance by ownership surface, for example Application/core rules on `src/TNC.Trading.Platform.Application/**/*`, adapter rules on API/Web/Infrastructure paths, and project-dependency rules on `src/**/*.csproj` and `test/**/*.csproj`.

Advantages:

* Lowest irrelevant context cost and highest precision.
* Can state different obligations for inner policy code, outer adapters, composition roots, and architecture tests.
* Makes exceptions explicit, such as API referencing Infrastructure only to register concrete adapters.

Risks:

* Creates several assets and increases maintenance/routing complexity for a small solution.
* Cross-layer changes can load overlapping files whose wording may drift.
* Path-based rules can ossify current project names and turn architecture into folder compliance.
* Prematurely assumes a stable Domain/Application split that does not yet exist.

Assessment: viable after dependency rules become substantial or the solution gains more independently governed services. It is excessive for the current repository unless a single narrowly scoped companion file is limited to `*.csproj` dependency constraints.

### Recommended fit

Use Strategy 2 now. Treat Clean Architecture as dependency and responsibility guidance inside the existing architecture owner, not as a mandated directory template. Consider a small Strategy 3 companion only for executable project-reference constraints if `applyTo` complexity or instruction length becomes material.

The future rules should state these invariants:

* Application policy and use cases MUST NOT depend on API, Web, AppHost, or Infrastructure implementations.
* Infrastructure and host adapters MAY depend on Application contracts to implement or invoke them.
* Composition roots MAY reference inner contracts and concrete outer adapters solely to register and wire dependencies; business decisions MUST remain inward.
* External protocol, persistence, identity-provider, and broker details MUST be translated at adapter boundaries and MUST NOT leak into application contracts unless they are genuine domain concepts.
* Interfaces SHOULD be owned by the layer that consumes the capability, and MUST NOT be introduced only to satisfy a layer diagram.
* New projects or folders MUST follow demonstrated responsibility and dependency needs, not canonical Clean Architecture names.
* Vertical slices SHOULD remain cohesive within the owning layer. A use-case slice may contain request, response, validation, and handler behavior while calling inward domain policy and outward ports implemented by Infrastructure.
* CQRS in this repository means explicit operation-specific contracts and separated command/query intent. It MUST NOT imply separate databases, a mediator library, event sourcing, or one project per use case without a requirement.

This model resolves the apparent tension: Vertical Slice Architecture describes how behavior is grouped across an operation, Clean Architecture describes which direction dependencies cross boundaries, and CQRS describes operation contracts and command/query intent. They are orthogonal constraints, not competing folder trees.

## Key Discoveries

* The ecosystem already has a clear architecture owner. A new standalone file would duplicate responsibility unless it is restricted to project dependency enforcement.
* Clean Architecture's missing value is explicit inward dependency direction and composition-root exceptions, not another statement of SOLID or another folder hierarchy.
* Current project references already provide a useful baseline: Application is inward, Infrastructure implements Application-facing contracts, and API composes both.
* Existing Vertical Slice rules are stricter than Clean Architecture requires. Future language should preserve feature cohesion while avoiding universal four-file scaffolding where an operation does not need every artifact.
* Configuration and authentication rules are not architectural exceptions. They are boundary requirements: provider/database details stay in adapters while application-facing policy and ports stay inward.
* Validation should include focused build/tests plus a dependency check. `dotnet build` and `dotnet test` detect compilation and behavior regressions but do not reliably prevent forbidden references when both projects compile.
* A durable lesson should be added when implementation occurs: architecture instructions should encode dependency invariants and composition-root exceptions, not canonical folder names. This research-only task does not modify `.github/AGENTS.md` or `.github/lessons.md`.

## Unresolved Gaps

* The repository has no evident automated architecture-test package or project-reference validation script. A decision is needed between a lightweight MSBuild/script check and a test library such as NetArchTest or ArchUnitNET.
* There is no dedicated Domain project. Research does not establish that one is needed; current domain-like models and policy live in Application. The team must decide whether domain independence is a current requirement or a future extraction trigger.
* Application references `Microsoft.AspNetCore.App`. Shared authentication policy helpers may justify this today, but the desired framework-dependency boundary is not explicitly documented.
* API directly references EF Core and Azure packages while Infrastructure also owns those packages. It is unclear whether this is temporary composition/startup ownership or architectural leakage requiring migration.
* The current architecture instruction mandates request, response, handler, and optional validator files for every operation. A future edit should decide whether this remains universal or becomes a default shaped by operation complexity.

## Recommended Next Research

* Inventory all project references, package references, and cross-project namespaces to produce an explicit allowed-dependency matrix.
* Identify why API owns EF Core/Azure package references and whether concrete usage remains outside Infrastructure.
* Evaluate a minimal executable architecture check against the allowed-dependency matrix and estimate maintenance cost.
* Review Application's ASP.NET Core framework dependency and shared authentication helpers before imposing framework-purity rules.
* Test proposed `applyTo` globs in VS Code for C# and project-file edits, including whether comma-separated patterns activate as expected in the installed host.
* Draft a concise augmentation to `.github/instructions/architecture.instructions.md`, then run customization diagnostics and check it against token-discipline and prompt-builder requirements.
