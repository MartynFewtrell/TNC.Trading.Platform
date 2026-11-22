<!-- markdownlint-disable-file -->
# Implementation Details: Portable Clean Architecture Guidance

## Context Reference

Sources: `.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md`, `.copilot-tracking/research/2026-07-26/clean-architecture-guidance-update-research.md`, and `.copilot-tracking/research/subagents/2026-07-26/portable-clean-architecture-guidance-research.md`.

## Implementation Phase 1: Confirm Instruction Activation

<!-- parallelizable: false -->

### Step 1.1: Verify multi-pattern instruction activation

Use the installed VS Code customization tooling to verify that `.github/instructions/architecture.instructions.md` activates for representative production and test C# source and project files.

Files:
* `.github/instructions/architecture.instructions.md` - Portable architecture instruction whose activation scope will expand.

Discrepancy references:
* `DD-01` - Resolves host-specific activation syntax before changing frontmatter.

Success criteria:
* The supported `applyTo` syntax covers `src/**/*.cs`, `src/**/*.csproj`, `test/**/*.cs`, and `test/**/*.csproj`.
* If one multi-pattern instruction is unsupported, use a narrow project-file companion without duplicating source guidance or broadening activation to `**/*`.

Context references:
* `.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md` (Lines 417-436) - Proposed ownership and activation scope.
* `.copilot-tracking/research/subagents/2026-07-26/portable-clean-architecture-guidance-research.md` (Lines 351-356) - Remaining activation decision.

Dependencies:
* Installed VS Code host and customization evaluation tooling.

## Implementation Phase 2: Author Portable Architecture Guidance

<!-- parallelizable: false -->

### Step 2.1: Broaden instruction ownership without naming a topology

Revise the description, title, overview, and scope so the instruction jointly owns Vertical Slice feature cohesion, CQRS-style operation contracts, and Clean Architecture dependency direction. State explicitly that architectural responsibilities may be separated by files, folders, assemblies, services, or another justified boundary and need not use canonical names.

Files:
* `.github/instructions/architecture.instructions.md` - Existing normative architecture owner.

Success criteria:
* The instruction is understandable without knowledge of TNC.Trading.Platform.
* No rule requires projects named Domain, Application, Infrastructure, API, Web, AppHost, or ServiceDefaults.
* Existing request, response, handler, validator, and thin-endpoint rules remain compatible with the portable boundary rules.

Context references:
* `.github/instructions/architecture.instructions.md` (Lines 1-19) - Existing frontmatter, title, overview, and scope.
* `.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md` (Lines 315-341) - Relationship between Vertical Slice, CQRS, and Clean Architecture.

Dependencies:
* Step 1.1 completion.

### Step 2.2: Add conceptual responsibility and Dependency Rule constraints

Add concise `MUST`, `SHOULD`, and `MUST NOT` rules using conceptual roles: enterprise/domain policy where present, application use cases, inbound adapters, outbound adapters, frameworks/drivers, and composition roots. Require source dependencies to point toward policy, outer calls to use inner-owned ports, and boundary representations to be translated into simple inward-owned data.

Files:
* `.github/instructions/architecture.instructions.md` - Portable normative rules.

Discrepancy references:
* `DD-02` - Supersedes the prior named six-project matrix.

Success criteria:
* Rules depend on responsibility and dependency direction rather than names or project count.
* Business policy and use cases do not name persistence, transport, UI, framework, provider, or hosting types.
* Inbound adapters bind external input and invoke use cases; outbound adapters implement ports owned by inner consumers.
* Composition roots may select concrete adapters only for registration, configuration, startup, and hosting.
* Guidance requires simple boundary data and translation at adapter boundaries.
* Guidance does not require exactly four layers, a Domain project, one interface per implementation, MediatR, repositories, microservices, or event sourcing.

Context references:
* `.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md` (Lines 63-150) - Dependency Rule, roles, boundary data, and composition.
* `.copilot-tracking/research/subagents/2026-07-26/portable-clean-architecture-guidance-research.md` (Lines 42-67) - Selected portable conceptual roles.

Dependencies:
* Step 2.1 completion.

### Step 2.3: Add portable examples, decision guidance, and validation limits

Use neutral examples such as `use case -> inner-owned port <- database adapter`, `HTTP adapter -> request DTO -> use case`, and `composition root -> concrete adapter registration`. Explain how Copilot should classify responsibilities before adding dependencies and how to proceed when a repository has not yet declared stable assembly boundaries.

Files:
* `.github/instructions/architecture.instructions.md` - Examples, references, and validation sections.

Success criteria:
* Examples use conceptual names rather than this repository's projects or features.
* The instruction distinguishes runtime call direction from source dependency direction.
* Copilot is told to preserve existing repository boundaries when valid, propose incremental movement when responsibilities are misplaced, and avoid inventing speculative projects or abstractions.
* Validation distinguishes graph integrity checks, repository-specific boundary checks when declared, source-level review, and independent policy tests.
* References include primary Clean Architecture, A Little Architecture, and publisher-authorized Dependency Rule material.

Context references:
* `.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md` (Lines 485-520) - Examples and validation requirements.
* `.copilot-tracking/research/subagents/2026-07-26/portable-clean-architecture-guidance-research.md` (Lines 120-140) - Rules not inferable from project references.

Dependencies:
* Step 2.2 completion.

### Step 2.4: Validate portability and activation

Run repository Markdown/frontmatter checks and Chat Customizations Evaluations against representative source and project files. Search the final instruction for TNC project names and current component-specific rules.

Validation actions:
* Run repository-supported Markdown and frontmatter validation.
* Run Chat Customizations Evaluations for representative `.cs` and `.csproj` files.
* Confirm any current project name appears only in a clearly non-normative repository example; prefer no current project names.

Dependencies:
* Step 2.3 completion.

## Implementation Phase 3: Add Topology-Neutral Graph Integrity Checks

<!-- parallelizable: false -->

### Step 3.1: Create the architecture integration-test project

Create a dependency-light `net10.0` xUnit integration-test project. The real repository assertion is an integration test because it reads project files; graph algorithm scenarios remain in-memory. Do not reference production projects or add an architecture analysis package.

Files:
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj` - Topology-neutral graph integrity suite.

Success criteria:
* The project follows the established xUnit and test SDK baseline.
* It contains no production `ProjectReference` and no fixed architecture package.
* Its purpose is graph integrity, not enforcement of the current project layout.

Context references:
* `global.json` (Lines 1-6) - SDK baseline.
* `.github/instructions/tests.instructions.md` (Lines 23-50) - Project taxonomy and file-system boundary.

Dependencies:
* .NET SDK `10.0.200`.

### Step 3.2: Implement generic project graph discovery and validation

Discover production project files under the repository's existing product root, parse `ProjectReference` items using `System.Xml.Linq`, normalize paths, and validate that targets exist and the discovered graph is acyclic. Do not classify projects into architectural layers or maintain an allowed-edge list.

Files:
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraph.cs` - Generic project and edge model.
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphLoader.cs` - Structured XML discovery and loading.
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs` - Missing-target and cycle validation.

Discrepancy references:
* `DD-03` - Defers project-level direction enforcement until stable boundaries are declared.

Success criteria:
* Validator code contains no current production project names, fixed project count, layer labels, or allowed-edge matrix.
* Project additions, removals, and renames pass when references resolve and the graph remains acyclic.
* Diagnostics identify the broken target or complete cycle path.
* XML is parsed with a structured API, never regular expressions.

Context references:
* `.copilot-tracking/research/subagents/2026-07-26/portable-clean-architecture-guidance-research.md` (Lines 89-118) - Portable executable invariants and mapping limits.

Dependencies:
* Step 3.1 completion.

### Step 3.3: Add repository and in-memory graph tests

Add one real-repository assertion and in-memory tests for missing references, direct cycles, transitive cycles, acyclic graphs, and project rename/addition scenarios. Fully document each test with expected outcome and architectural significance.

Files:
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/RepositoryProjectGraphTests.cs` - Real graph integrity assertion.
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidatorTests.cs` - In-memory graph behavior tests.

Success criteria:
* Test names follow `MethodName_StateUnderTest_ExpectedResult`.
* The current graph passes without being encoded as the expected graph.
* Tests prove topology changes are accepted when generic invariants remain valid.
* Tests do not mutate production project files.

Context references:
* `.github/copilot-instructions.md` (Lines 36-43) - Test naming and traceability requirements.
* `.copilot-tracking/research/subagents/2026-07-26/portable-clean-architecture-guidance-research.md` (Lines 142-174) - Revised architecture test decision.

Dependencies:
* Step 3.2 completion.

### Step 3.4: Register and validate the graph suite

Add the architecture test project to the solution and run the focused suite. Do not add a repository-specific boundary policy in this task.

Files:
* `TNC.Trading.Platform.slnx` - Solution registration.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`

Success criteria:
* The focused suite passes against the current graph.
* Root solution test discovery includes the architecture suite.
* No test failure requires retaining, renaming, or constraining the current production projects.

Dependencies:
* Step 3.3 completion.

## Implementation Phase 4: Align Supporting Documentation

<!-- parallelizable: false -->

### Step 4.1: Update the architecture wiki without freezing the topology

Add the portable Dependency Rule, conceptual responsibilities, boundary translation, runtime-versus-source dependency explanation, and composition constraints. Keep the current topology section as an implemented snapshot but label its mapping as transitional and non-normative.

Files:
* `docs/wiki/architecture.md` - Current implementation and architectural direction.

Success criteria:
* The page does not publish the old six-project matrix as a target.
* It states that project names and count may change while dependency direction and responsibility rules remain.
* Current API, AppHost, ServiceDefaults, and Web descriptions remain factual without being elevated into universal Clean Architecture roles.

Context references:
* `docs/wiki/architecture.md` (Lines 5-42) - Current graph and style.
* `.copilot-tracking/research/subagents/2026-07-26/portable-clean-architecture-guidance-research.md` (Lines 260-281) - Conditional wiki scope.

Dependencies:
* Phase 2 completion.

### Step 4.2: Document generic architecture testing

Add the architecture integration-test project, its focused command, its missing-target and cycle checks, and explicit limitations. Do not claim it enforces inward dependencies or stable component roles.

Files:
* `docs/wiki/testing-and-quality.md` - Test strategy and execution source of truth.

Success criteria:
* Documentation says the graph suite tolerates valid project additions, removals, and renames.
* Direction, boundary data, policy placement, and composition constraints remain review and behavior-test responsibilities until stable boundaries are declared.

Context references:
* `docs/wiki/testing-and-quality.md` (Lines 5-20) - Test project inventory.
* `docs/wiki/testing-and-quality.md` (Lines 192-211) - Validation commands.

Dependencies:
* Phase 3 completion.

### Step 4.3: Record the portable adoption lesson

Add one concise lesson stating that dependency direction and responsibility ownership, not project names, project count, or canonical folders, define Clean Architecture adoption.

Files:
* `.github/lessons.md` - Durable repository customization learning.

Success criteria:
* The lesson is concise and reusable.
* `.github/AGENTS.md` remains unchanged because its ownership guidance already applies.

Dependencies:
* Phase 2 completion.

### Step 4.4: Validate documentation consistency

Run Markdown/frontmatter validation and confirm the instruction, architecture wiki, testing wiki, and lesson consistently distinguish portable principles from transitional repository structure.

Dependencies:
* Steps 4.1 through 4.3 completion.

## Implementation Phase 5: Full Validation and Handoff

<!-- parallelizable: false -->

### Step 5.1: Run executable validation

Execute:
* `dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
* `dotnet build TNC.Trading.Platform.slnx`
* `dotnet test TNC.Trading.Platform.slnx`
* Repository Markdown, frontmatter, and customization evaluation checks.

### Step 5.2: Verify portability and boundary guidance

Confirm no instruction or validator rule hard-codes the current project set or assigns universal architecture meaning to current names. Review the guidance for inward dependency direction, simple boundary data, inner-owned ports, adapter translation, composition-only concrete dependencies, and independently testable policy.

### Step 5.3: Fix minor validation issues

Apply isolated corrections and rerun the narrow failing check before full validation.

### Step 5.4: Report deferred boundary enforcement

Record that project-level direction enforcement requires a future repository-specific boundary declaration after refactoring establishes stable roles. Do not create speculative policy configuration in this task.

### Step 5.5: Report blocking validation failures

If build, test, Markdown, frontmatter, or customization validation cannot be resolved through isolated corrections, document the failing command, relevant diagnostics, affected files, and recommended next action. Do not mark the implementation complete while blockers remain undisclosed.

## Dependencies

* .NET SDK `10.0.200` from `global.json`.
* Installed VS Code customization evaluation tooling.
* Existing xUnit test package baseline.
* Docker-backed infrastructure when required by the full solution test run.

## Success Criteria

* The instruction can guide an unrelated future project without TNC project knowledge.
* Clean Architecture rules are expressed through responsibilities and source dependency direction.
* Generic graph tests catch missing references and cycles while accepting valid topology changes.
* Documentation presents current project structure as transitional implementation context, not normative architecture.
* No fixed layer projects, project count, allowed-edge matrix, or speculative repository boundary policy is introduced.
