<!-- markdownlint-disable-file -->
# Task Research: Portable Clean Architecture Guidance Update

## Scope

Plan an update to the repository's architecture guidance so Clean Architecture dependency direction, policy ownership, adapter boundaries, composition exceptions, and testability are expressed as reusable principles without imposing project names, a project count, or the current repository topology.

## User Requirements

* Update `.github/instructions/architecture.instructions.md` based on the completed Clean Architecture research.
* Guide future development toward Clean Architecture.
* Consider supporting documentation updates.
* Do not enforce the current projects because the policy and adapter projects may change during refactoring.
* Make the instruction portable enough to reuse in future projects rather than specific to TNC.Trading.Platform.

## Key Findings

* `.github/instructions/architecture.instructions.md` is the existing owner of Vertical Slice Architecture and CQRS-style feature rules and should also own Clean Architecture dependency rules.
* Vertical Slice Architecture controls feature cohesion, CQRS controls operation contracts, and Clean Architecture controls responsibility ownership and source dependency direction. The guidance must present them as complementary.
* The Dependency Rule applies to conceptual responsibilities, not names such as Application, Infrastructure, Domain, API, or Web. Policy and use-case code must not depend on adapters, frameworks, delivery mechanisms, or external representations regardless of folder or assembly layout.
* Inbound adapters receive external input and invoke use cases. Outbound adapters implement inner-owned ports. Composition roots may reference concrete adapters only to assemble and host the system.
* Request and response models crossing inward must be simple, inward-owned data rather than framework, persistence, UI, transport, or provider types.
* The current project graph is point-in-time assessment evidence, not a target architecture. Likely retention of API, AppHost, ServiceDefaults, and Web does not make those names portable architecture roles.
* No stable post-refactor boundary map exists yet, so a project-level dependency policy would either freeze transitional projects or encode speculative future boundaries.
* A topology-neutral architecture integration test may check broken `ProjectReference` targets and cycles without prescribing roles, names, counts, or permitted layer edges. Direction enforcement must wait until a repository declares stable boundaries separately from the portable instruction.
* `docs/wiki/architecture.md` and `docs/wiki/testing-and-quality.md` are the supporting documentation sources of truth that require updates.
* `.github/lessons.md` should record that dependency and responsibility invariants, rather than canonical folder names, define Clean Architecture adoption.
* `.github/AGENTS.md`, historical delivery plans, README, runtime behavior, API reference, operator guidance, and local development documentation do not require changes for this contributor-guidance and validation slice.

## Selected Implementation Path

1. Verify the supported multi-pattern `applyTo` syntax for `.cs` and `.csproj` files.
2. Extend `.github/instructions/architecture.instructions.md` with portable conceptual roles, the Dependency Rule, boundary data and translation, inner-owned ports, composition-root constraints, testing expectations, anti-ceremony rules, examples, and primary references.
3. Add a topology-neutral architecture integration test that discovers production projects and validates only missing targets and cycles; do not encode project names, fixed counts, layer labels, or allowed edges.
4. Register the architecture test project in `TNC.Trading.Platform.slnx` and document the check's limited scope.
5. Update `docs/wiki/architecture.md` to describe portable principles and mark current component mapping as transitional; update `docs/wiki/testing-and-quality.md` and `.github/lessons.md`.
6. Validate instruction activation, generic graph integrity, solution build/tests, and source-boundary guidance through customization evaluation and manual review.

## Constraints

* Do not create a standalone Clean Architecture instruction or mandatory Domain project.
* Do not add ArchUnitNET, NetArchTest, MediatR, marker abstractions, or speculative ports.
* Do not refactor production responsibilities, remove existing framework references, or reduce `InternalsVisibleTo` in this initial ratchet.
* Parse project XML with a structured XML API, not regular expressions.
* Do not encode current project names, a fixed project count, named layer ownership, or an allowed-edge matrix in the instruction or validator.
* Do not infer architectural roles from project names or folder paths.
* Defer policy-driven direction enforcement until the refactor establishes stable boundaries and a repository-specific mapping can be changed atomically with future project moves.
* Update wiki documentation before implementation is considered complete.

## Validation

Run from the repository root:

```powershell
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj
dotnet build TNC.Trading.Platform.slnx
dotnet test TNC.Trading.Platform.slnx
```

The architecture suite must include in-memory fixtures proving diagnostics for missing targets and cycles. It must accept project additions, removals, and renames when the resulting graph is valid. Manual review and customization evaluation remain necessary for dependency direction, outer representations crossing inward, misplaced business decisions, inner-owned ports, concrete adapter use outside composition, and policy tests requiring external machinery.

## Sources

* `.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/clean-architecture-plan-support-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/portable-clean-architecture-guidance-research.md`
* `.github/instructions/architecture.instructions.md`
* `.github/copilot-instructions.md`
* `.github/instructions/token-discipline.instructions.md`
* `.github/instructions/refactoring.instructions.md`
* `.github/lessons.md`
* `docs/wiki/architecture.md`
* `docs/wiki/testing-and-quality.md`
* `TNC.Trading.Platform.slnx`

## Remaining Implementation Check

The exact `applyTo` syntax must be verified in the installed VS Code host before editing instruction frontmatter. If one instruction cannot activate reliably for both source and project files, retain source guidance in the existing instruction and add a narrowly scoped `.csproj` companion only as a documented fallback. A future repository-specific boundary policy is intentionally deferred until stable post-refactor roles are known.
