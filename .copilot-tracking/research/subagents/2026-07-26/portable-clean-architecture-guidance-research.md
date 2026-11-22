---
title: Portable Clean Architecture guidance research
description: Reassessment of Clean Architecture planning for project-agnostic guidance and executable invariants
ms.date: 2026-07-26
ms.topic: concept
---

## Research questions

* How must the existing Clean Architecture research, plan, details, and log change
  after removing the assumption that the current six-project production graph is
  permanent?
* Which Clean Architecture invariants can be executed without hard-coding
  project names?
* Should a dedicated architecture test project remain?
* Which durable documentation should change, and which repository-specific
  documentation should wait for an implemented topology?

## Status

Complete. The new direction invalidates the selected closed six-project matrix
as a normative or executable architecture rule. It does not invalidate the
primary-source findings about dependency direction, policy ownership, boundary
translation, composition, acyclic components, or independent policy testing.

The planning log referenced by the current plan does not exist at
`.copilot-tracking/plans/logs/2026-07-26/clean-architecture-guidance-update-log.md`.
The revised work must create it before implementation or remove the dangling
reference. Creating it is preferred because the direction change is a material
planning discrepancy.

## Evidence assessed

* `.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md`
* `.copilot-tracking/research/2026-07-26/clean-architecture-guidance-update-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/uncle-bob-clean-architecture-primary-sources-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/repository-clean-architecture-alignment-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/clean-architecture-alternatives-and-enforcement-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/clean-architecture-plan-support-research.md`
* `.copilot-tracking/plans/2026-07-26/clean-architecture-guidance-update-plan.instructions.md`
* `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md`
* `.github/instructions/architecture.instructions.md`

The primary-source research says the four circles are schematic, permits more
or fewer boundaries, and identifies the Dependency Rule rather than project
names as the invariant. It also supports caller-owned ports, simple boundary
data, composition at the edge, testable policy, and acyclic components. The
repository assessment maps those concepts onto the current projects, but also
states that project names do not map cleanly to one circle. The later planning
artifacts converted that temporary mapping into a permanent six-project
allow-list. That conversion is incompatible with the new direction.

## Selected path

Use `.github/instructions/architecture.instructions.md` as the portable,
project-agnostic normative owner. Express rules through conceptual roles and
boundary relationships, not repository project names:

* policy and use-case code
* inbound adapters such as HTTP, messaging, CLI, or UI
* outbound adapters such as persistence, identity, broker, notification, or
  external service integrations
* frameworks, hosts, deployment, and composition roots

Do not require those roles to be separate projects or to use those labels.
Require the repository to declare its concrete boundary mapping when executable
direction checks are desired.

Retain a dedicated architecture test project, but change its purpose from
freezing six named projects to hosting reusable graph validation plus a small
repository-owned architecture policy. Keep project identities out of validation
code. A policy file or equivalent declarative fixture may identify current
components and allowed direct dependencies, but that mapping is configuration,
not portable instruction content and not a claim that the current graph is
permanent.

Prefer a declarative policy beside the architecture tests because it can change
atomically with a project refactor and does not require adding architecture
metadata to every production project. The policy should declare:

* the production discovery scope
* component identities by relative project path or stable policy key
* each component's conceptual role or boundary rank where applicable
* allowed direct project references or permitted dependency directions
* explicit composition-root exceptions with a reason
* treatment of newly discovered projects, selected as fail closed or
  unclassified warning by repository policy

The generic test engine should parse project XML with `System.Xml.Linq`, load
this policy, and produce diagnostics in terms of source component, target
component, and violated invariant. No project name belongs in test logic.

## Portable executable invariants

### Executable without a repository role mapping

The following checks are project-agnostic and can run over a discovered
production project graph:

* Every direct `ProjectReference` resolves to an existing project.
* The production project-reference graph is acyclic.
* Production projects do not reference projects classified as tests.
* The architecture policy is internally valid: unique component identities,
  resolvable paths, no duplicate edges, and no references to undeclared policy
  keys.
* Every project found within the configured production discovery scope is
  accounted for according to the selected unknown-project policy.

The production discovery scope and test classification remain repository
configuration. The validation algorithm does not depend on names such as
`Application`, `Infrastructure`, `API`, or `Web`.

### Executable only after roles or boundaries are declared

The following rules cannot be inferred reliably from project names, folders, or
references alone:

* Source dependencies point toward higher-level policy.
* Policy and use-case components do not depend on adapters, hosts, UI,
  persistence, or external SDK components.
* Outbound adapters implement ports owned by the consuming inner boundary.
* Composition roots may depend on concrete adapters only for registration,
  configuration, startup, adapter selection, and hosting.
* A direct edge is an approved boundary edge or a documented composition
  exception.

A declarative component policy can enforce project-level direction once the
repository has selected its post-refactor boundaries. The check should compare
role ranks or an explicit allowed-edge set. It must not infer that a project is
inner because its name contains `Application`, or outer because its name
contains `Infrastructure`.

### Not reliably executable through project references alone

These invariants require source analysis, compiled-assembly tooling, focused
behavior tests, or manual review:

* framework, persistence, transport, UI, or provider types do not cross inward
  boundaries
* business decisions remain in policy or use-case code rather than adapters or
  composition roots
* an inner consumer owns a narrow outbound port
* adapters translate outer representations into inward-owned boundary data
* policy can be tested without a web host, database, container, UI, broker, or
  external service
* composition-root access to concrete adapters is not used by request handlers
  or business workflows

Do not claim the XML project graph test enforces these source-level properties.
Add source-level tooling only after selecting a precise rule with acceptable
false-positive behavior.

## Architecture test project decision

Keep the architecture test project, subject to one naming decision. It remains
valuable because graph integrity, cycles, declared-boundary direction, policy
completeness, and actionable negative fixtures are executable and independent
of the production topology.

Change its planned design as follows:

* Replace `ProductionProjectDependencyRules` containing six hard-coded projects
  with a generic graph validator and a declarative policy loader.
* Keep the real-repository assertion as an integration test because it reads
  project files.
* Keep rule behavior tests in memory, covering missing targets, cycles, invalid
  policy, unknown projects, forbidden direction, and composition exceptions.
* Do not add production project references to the test project.
* Do not encode project names, a fixed project count, or fixed layer labels in
  validator source.
* Permit the policy mapping to be revised or temporarily minimized while
  Application and Infrastructure are refactored.

The project should be named for architecture validation rather than a specific
layer topology. The already planned
`TNC.Trading.Platform.Architecture.IntegrationTests` name is acceptable. A
single project can contain the repository-file integration assertion and
in-memory rule tests, although the repository test taxonomy must document that
mixed scope clearly.

Dropping the project entirely is not recommended. It would discard cheap,
portable checks for cycles and broken references and would leave declared
boundary direction as prose only. Freezing the current six-project graph is
also not recommended because it would obstruct the expected refactor and turn
transitional names into architecture semantics.

## Exact changes to existing artifacts

### Primary research synthesis

Update
`.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md`
as a superseded decision record or add a prominent direction-change addendum.
Do not erase its repository assessment or primary-source evidence.

* Remove the assumption that the current six production projects remain in
  place.
* Replace the executive decision to add a closed six-project matrix with the
  selected portable instruction plus declarative repository policy.
* Relabel the current graph and matrix as a point-in-time assessment, not the
  target architecture.
* Replace all normative `Application`, `Infrastructure`, `API`, `Web`,
  `AppHost`, and `ServiceDefaults` rules with conceptual role rules.
* Retain current project examples only as non-normative illustrations of the
  repository at the research date.
* Replace the XML six-project allow-list design with generic graph checks and
  policy-driven boundary checks.
* Update the adoption sequence so boundary discovery and policy declaration
  follow or accompany the Application and Infrastructure refactor.

### Consolidated planning research

Update
`.copilot-tracking/research/2026-07-26/clean-architecture-guidance-update-research.md`.

* Delete the closed production project-reference table from selected decisions.
* Remove constraints to retain six production projects and fail on every
  unknown project through a hard-coded set.
* Select portable normative guidance, generic graph validation, and a mutable
  declarative repository policy.
* Change documentation scope from publishing the six-project matrix to
  documenting the implemented boundary mapping only after it is stable.
* Keep focused architecture tests, solution build, and solution tests as
  validation categories, but do not prescribe a final test path until the
  architecture test project naming decision is confirmed.

### Planning support research

Update
`.copilot-tracking/research/subagents/2026-07-26/clean-architecture-plan-support-research.md`
or mark it superseded by this research.

* Replace every six-project allow-list requirement with policy-driven component
  discovery and boundary validation.
* Remove statements assigning permanent policy ownership to Application or
  adapter ownership to Infrastructure.
* Reclassify current project mappings as migration evidence.
* Revise wiki recommendations so current topology is not documented as a
  permanent rule before the refactor settles.
* Replace negative fixtures for six named projects with generic graph and policy
  fixtures.

### Implementation plan

Revise
`.copilot-tracking/plans/2026-07-26/clean-architecture-guidance-update-plan.instructions.md`
before execution.

* Change the overview and derived objectives to reject a fixed project graph.
* Remove every objective, checklist item, success criterion, and dependency
  that freezes six production projects.
* In Phase 2, author conceptual role, dependency direction, boundary data,
  composition, and testability rules without repository project names.
* In Phase 3, retain the architecture test project but implement a generic graph
  model, policy loader, policy validation, and repository assertion.
* Add a planning step to choose the policy representation and unknown-project
  behavior.
* Make repository-specific boundary policy conditional on the architecture
  boundaries known at implementation time. At minimum, execute missing-target
  and cycle checks without waiting for the refactor.
* In Phase 4, update wiki pages only for behavior and topology that have been
  implemented and verified.
* Replace the success criterion for an explicit six-project graph with generic
  graph integrity plus policy-driven direction checks.

### Implementation details

Revise
`.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md`
to match the new plan.

* Step 2.2 must no longer prescribe a closed matrix or assign responsibilities
  to named projects.
* Step 3.2 must define generic types such as `ProjectGraph`,
  `ArchitecturePolicy`, `ArchitecturePolicyLoader`, and
  `ArchitecturePolicyValidator`; final names may follow local conventions.
* Step 3.3 must cover missing targets, cycles, invalid policy, unknown project
  handling, forbidden direction, and reasoned composition exceptions.
* Step 3.4 must validate the current repository against only the policy approved
  for the refactor state.
* Step 4.1 must not publish the old matrix as the implemented architecture.
* Steps 5.1 and 5.2 must distinguish generic executable checks,
  policy-dependent checks, and manual source-boundary review.

### Planning log

Create
`.copilot-tracking/plans/logs/2026-07-26/clean-architecture-guidance-update-log.md`
because the current plan references it but no file exists.

Record a discrepancy with these exact decisions:

* The prior plan assumed a stable six-project production graph.
* New user direction permits Application and Infrastructure refactoring and
  rejects treating likely surviving project names as architecture layer names.
* The closed matrix, hard-coded project set, and named-role rules are
  superseded.
* Portable conceptual guidance and policy-driven executable validation are
  selected.
* The architecture test project remains, but the policy representation,
  unknown-project behavior, and timing of a complete boundary map remain open.

## Documentation scope

### Required when implementing the guidance

* Update `.github/instructions/architecture.instructions.md` as the portable
  normative owner.
* Update `.github/lessons.md` with the stable lesson that dependency direction
  and responsibility ownership, not project names or project count, define
  Clean Architecture adoption.

The instruction should retain compatible Vertical Slice and CQRS guidance but
must not require a particular application-layer project, infrastructure-layer
project, Domain project, host name, or four-circle assembly layout.

### Required if the architecture test project is implemented

* Update `docs/wiki/testing-and-quality.md` with the architecture test purpose,
  focused command, generic graph checks, policy-driven checks, and limitations.
* Register the architecture test project in the solution so normal test
  discovery includes it.

### Conditional after boundaries are implemented

* Update `docs/wiki/architecture.md` with the actual component-to-role mapping,
  dependency decisions, and composition exceptions only after the refactor has
  established them.
* If the guidance change lands before the refactor, update only the conceptual
  principles and explicitly identify the current topology as transitional. Do
  not publish the old six-project matrix as a durable target.

### Out of scope

No update is needed for README, API reference, runtime behavior, operator guide,
local development, historical work-package plans, or `.github/AGENTS.md` unless
implementation changes their owned behavior. No new Domain project, production
project move, or Application/Infrastructure refactor belongs in the guidance
change itself.

## Unresolved decisions

* Choose the declarative policy format. A JSON file beside the architecture
  tests is the leading option because it is structured, testable, and separate
  from production project files. XML or strongly typed test fixture data are
  acceptable if repository conventions favor them.
* Choose unknown-project behavior during active refactoring. Fail closed gives
  the strongest ratchet; a temporary explicit `unclassified` state avoids
  blocking project extraction but must fail by a deadline or dedicated test.
* Decide whether the first implementation includes a complete repository
  boundary policy or only graph integrity checks until Application and
  Infrastructure refactoring settles.
* Confirm the architecture test project name and whether mixed integration and
  in-memory tests remain in one project.
* Verify multi-pattern instruction activation for `.cs` and `.csproj` files.
* Decide whether source-level package or namespace rules are valuable enough to
  justify compiled-assembly tooling after the project boundaries are selected.

## Validation recommendations

1. Validate the revised tracking artifacts for internal consistency: no success
   criterion or implementation step may require the old six-project matrix.
2. Run Markdown and frontmatter validation for changed tracking and instruction
   files using repository-supported tooling.
3. Run Chat Customizations Evaluations against representative `.cs` and
   `.csproj` files and confirm that the instruction offers conceptual guidance
   without assuming project names.
4. Unit test the generic graph and policy validator with in-memory fixtures for
   missing targets, cycles, duplicate identities, unresolved policy paths,
   unknown projects, forbidden direction, and composition exceptions.
5. Run the real-repository architecture assertion against the approved policy
   for the current refactor state.
6. Run the focused architecture test project, then build and test the solution.
7. Search the final instruction and validator source for the current six project
   names. Any occurrence must be an explicitly non-normative example or
   repository policy entry, never validator logic.
8. Manually review changed production work for boundary data leakage, misplaced
   policy, caller-owned ports, composition-only concrete adapter use, and policy
   tests requiring external machinery.

## Recommended next research

* [ ] Prototype JSON and MSBuild-metadata policy representations and compare
  refactor ergonomics, diagnostics, and discoverability.
* [ ] Determine the intended boundary map after the Application and
  Infrastructure refactor direction is known.
* [ ] Verify instruction activation for source and project files in the
  installed VS Code host.
* [ ] Evaluate one precise source-level dependency rule before selecting an
  architecture analysis package.

## Clarifying questions

* Must newly added production projects fail immediately while the refactor is in
  progress, or may they enter an explicit temporary `unclassified` state?
* Should the first architecture policy describe today's transitional graph, or
  should complete direction enforcement wait for the planned Application and
  Infrastructure boundary decisions?
