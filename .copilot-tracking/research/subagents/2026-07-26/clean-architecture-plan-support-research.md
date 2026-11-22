---
title: Clean architecture plan support research
description: Implementation planning evidence for Clean Architecture instruction, documentation, and validation updates
ms.date: 2026-07-26
ms.topic: concept
---

## Research questions

* Which exact repository assets and line ranges need updates to implement the
  selected Clean Architecture direction?
* Which documentation scope should accompany the architecture instruction
  change without duplicating ownership?
* What implementation dependencies, architecture checks, and local or CI
  validation commands should the delivery plan include?
* Which unresolved decisions must be made before implementation?

## Provisional direction

* Update `.github/instructions/architecture.instructions.md` as the single
  owner of Vertical Slice, CQRS-style contract, and Clean Architecture
  dependency guidance.
* Extend activation to project files only after verifying the supported
  `applyTo` syntax.
* Add a structured XML production-project reference allow-list check that
  rejects forbidden direct references, unknown production projects, and
  cycles.
* Align the implemented architecture and testing wiki pages with the new
  dependency rule and executable validation.
* Treat `.github/AGENTS.md` and `.github/lessons.md` as conditional updates:
  change them only if implementation confirms a new durable routing or
  authoring lesson not already recorded.

## Evidence inventory

### Primary architecture instruction

* `.github/instructions/architecture.instructions.md:1-3` owns the current
  description and activation scope. The description should include inward
  dependency rules, and `applyTo` must cover production and test C# project
  files after the syntax is verified.
* `.github/instructions/architecture.instructions.md:6-19` names and scopes the
  instruction only around Vertical Slice Architecture and CQRS-style feature
  work. Rename the title and broaden the overview and scope to distinguish
  feature cohesion from cross-boundary dependency direction.
* `.github/instructions/architecture.instructions.md:23-74` contains the
  normative `MUST`, `SHOULD`, and `MUST NOT` rules. Preserve the useful slice
  rules, but add the closed production dependency matrix, Application policy
  ownership, inner-owned ports, adapter translation, composition-root
  exceptions, and incremental framework-coupling ratchet.
* `.github/instructions/architecture.instructions.md:76-87` currently validates
  only with broad build and test commands. Add the focused architecture check,
  solution build, affected tests, and manual boundary-review checks.
* `.github/instructions/architecture.instructions.md:89-120` has empty
  references plus feature-only examples and notes. Add primary Clean
  Architecture references and concise allowed dependency-inversion, forbidden
  reversal, and valid composition examples. Avoid a canonical four-project
  folder diagram.

### Customization operating assets

* `.github/AGENTS.md:5-21` already selects instructions as the owner of stable
  repository rules and defines the relevant loading order. No update is
  needed.
* `.github/AGENTS.md:55-57` and `.github/copilot-instructions.md:43-46` require a
  durable lesson only when customization work confirms a stable routing or
  authoring rule.
* `.github/lessons.md:3-9` does not yet record the architectural adoption
  lesson. Add one concise line during implementation: dependency invariants
  and responsibility ownership define Clean Architecture adoption; canonical
  folder or project names do not.
* `.github/instructions/token-discipline.instructions.md:38-51` supports
  updating the existing architecture owner and avoiding a parallel Clean
  Architecture instruction.
* `.github/instructions/refactoring.instructions.md:22-46` already covers
  separation of concerns, dependency inversion, explicit dependencies, narrow
  abstractions, and testability. Do not duplicate its refactoring workflow in
  the architecture instruction; cross-reference it where helpful.

### Implemented architecture documentation

* `docs/wiki/architecture.md:5-42` describes the architectural style and
  dependency graph but does not state the Dependency Rule or show why the API
  to Infrastructure edge is a composition exception. Update this section with
  the six-project direct-reference matrix and an explanation of runtime flow
  versus source dependency direction.
* `docs/wiki/architecture.md:72-108` shows runtime request flow. Clarify that
  outward runtime calls use Application-owned ports even though source
  dependencies remain inward.
* `docs/wiki/architecture.md:110-127` describes API composition. State that
  concrete Infrastructure access is limited to registration, configuration,
  adapter selection, startup, and hosting, not endpoint business workflows.
* `docs/wiki/architecture.md:129-171` describes Application and Infrastructure
  responsibilities. Reframe these as policy/use-case ownership versus adapter
  ownership, document boundary translation, and record current Application
  framework coupling as an incremental migration baseline rather than the
  desired end state.
* `docs/wiki/architecture.md:186-194` already identifies AppHost as a
  composition root. Align this wording with the instruction's composition
  exception.
* `docs/wiki/architecture.md:253-267` is the correct place to record the
  deliberate trade-offs: no mandatory Domain project, current six-project
  layout retained, and framework coupling reduced when owning features change.

### Testing and guideline documentation

* `docs/wiki/testing-and-quality.md:5-20` inventories test projects. Add the
  architecture unit-test project and its project-graph responsibility.
* `docs/wiki/testing-and-quality.md:37-66` describes Application and
  Infrastructure behavior tests but currently accepts Infrastructure-backed
  Application tests without distinguishing isolated policy tests. State the
  desired lower-cost policy-test boundary and treat current coupling as a
  baseline to reduce.
* `docs/wiki/testing-and-quality.md:192-211` is the validation command section.
  Add the focused architecture-test command before the root build and full test
  commands.
* `docs/wiki/testing-and-quality.md:248-257` is the future-change checklist.
  Add preservation of the production project-reference matrix, simple boundary
  data, and independently testable Application policy.
* `docs/wiki/codebase-guideline-alignment-delivery-plan.md:5-14` marks the prior
  guideline plan as done and gives it a different historical objective.
  `docs/wiki/codebase-guideline-alignment-delivery-plan.md:189-211` records its
  completed validation and acceptance criteria. Do not rewrite this historical
  plan for the new architecture direction.

### Build and executable enforcement entry points

* `TNC.Trading.Platform.slnx:2-27` includes all six production projects and the
  existing test projects. Add the selected architecture test project so root
  build and test commands execute it.
* `global.json:1-6` pins SDK `10.0.200`; the new test project should target the
  existing `net10.0` baseline.
* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13`
  has no production project reference but has the current ASP.NET Core framework
  reference at line 10. The project-edge test should pass this graph while the
  framework reference remains a documented migration candidate.
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:9-22`
  references Application and owns external packages.
* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:3-17`
  references Application, Infrastructure, and ServiceDefaults. The direct
  Infrastructure edge is allowed only as a composition-root exception.
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:3-11`
  references Application and ServiceDefaults, not API or Infrastructure.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:3-8`
  references API and Web for Aspire orchestration metadata.
* `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:3-21`
  has no production project references and must remain independent of product
  policy and adapters.
* No `.github/workflows/` files, repository build scripts, `Directory.Build.*`
  files, or architecture-named test/check files were found. Current validation
  enters through `TNC.Trading.Platform.slnx` and documented root `dotnet build`
  and `dotnet test` commands.

## Selected documentation scope

### Required updates

1. Update `.github/instructions/architecture.instructions.md` as the single
   normative owner. Do not create a standalone Clean Architecture instruction.
2. Add the architecture test project and project-graph test under
   `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.UnitTests/`,
   then register it in `TNC.Trading.Platform.slnx`.
3. Update `docs/wiki/architecture.md` to document the implemented dependency
   direction, closed direct-reference matrix, adapter boundary, composition
   exceptions, current framework-coupling baseline, and incremental adoption.
4. Update `docs/wiki/testing-and-quality.md` to list the architecture check,
   explain isolated Application policy testing, and publish the focused command.
5. Add one stable lesson to `.github/lessons.md` stating that dependency and
   responsibility invariants, not canonical folder names, define adoption.

### Explicit non-scope

* Do not update `.github/AGENTS.md`; its asset ownership and stable-learning
  rules already cover this work.
* Do not update
  `docs/wiki/codebase-guideline-alignment-delivery-plan.md`; it is a completed
  historical plan for type/file organization, not the source of truth for the
  new architecture direction.
* Do not update `README.md`, operator guidance, API reference, runtime behavior,
  or local-development guidance unless implementation changes runtime behavior
  or introduces a reusable script. The selected first step changes contributor
  rules and validation, not application runtime behavior.
* Do not create a Domain project, move production responsibilities, remove
  current framework references, add ArchUnitNET/NetArchTest, or reduce
  `InternalsVisibleTo` in the initial instruction-and-ratchet change.

## Dependencies and validation

### Selected executable check

Prefer a dedicated xUnit project at
`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.UnitTests/`
over an MSBuild target for the first implementation. This matches the repository
test layout, integrates with existing `dotnet test` entry points, gives focused
failure messages, and avoids coupling every build to custom target wiring.

The project should follow the package baseline in
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:1-26`,
but it should not reference production projects. The test should locate the
repository root, enumerate `src/*/*.csproj`, and parse each file with
`System.Xml.Linq`. It should maintain the closed six-project allow-list and
fail when:

* a known production project has a forbidden direct `ProjectReference`
* an unexpected production project appears without an explicit matrix decision
* a referenced production project is missing from the known set
* the production project-reference graph contains a cycle

The test should normalize paths before comparing project identities and report
the source project, target project, and violated rule. Do not parse XML with
regular expressions. Keep framework/package category checks manual in this
first slice because the primary research selects direct project edges as the
initial executable invariant.

### Implementation dependencies

1. Verify that the installed VS Code host activates an instruction for both
   `.cs` and `.csproj` files using the proposed comma-separated `applyTo` value.
   If it does not, use the host-supported pattern form or create a narrow
   project-file companion as a fallback.
2. Confirm the dedicated xUnit project decision before implementation. An
   MSBuild target remains a fallback only if architecture violations must fail
   `dotnet build` before tests run.
3. Add the test project to `TNC.Trading.Platform.slnx`; otherwise root
   `dotnet test` will not execute it.
4. Preserve the current graph as the passing baseline. The initial work is a
   ratchet, not a production refactor.
5. Update the two selected wiki pages before considering the plan complete, as
   required by `.github/copilot-instructions.md:43-43`.

### Validation commands

Run from the repository root in this order:

```powershell
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.UnitTests/TNC.Trading.Platform.Architecture.UnitTests.csproj
dotnet build TNC.Trading.Platform.slnx
dotnet test TNC.Trading.Platform.slnx
```

For a cheap negative proof, temporarily add one forbidden production
`ProjectReference` in a disposable local edit, run only the focused architecture
test, verify that its diagnostic names the forbidden edge, and remove the probe
before final validation. Also verify the unknown-project and cycle branches with
isolated test fixture XML rather than mutating production projects.

Manual review remains necessary for rules the first check does not encode:

* new Application framework or package categories
* HTTP, UI, EF, persistence, or provider SDK types crossing inward
* endpoint or AppHost code owning business decisions
* concrete Infrastructure use outside composition modules
* policy tests that require ASP.NET Core, Aspire, containers, databases, or UI

## Decisions and remaining gaps

### Selected decisions

* Keep the existing six production projects initially.
* Treat Vertical Slice Architecture, CQRS-style contracts, and Clean
  Architecture as complementary concerns in one instruction.
* Make Application the owner of use cases, policy, inward boundary models, and
  outbound ports while no Domain project exists.
* Use a closed direct-reference matrix and fail closed on unknown production
  projects.
* Use a dependency-light xUnit/XML architecture check as the first executable
  enforcement mechanism.
* Adopt incrementally: prevent new reversed edges immediately, then reduce
  current framework coupling or misplaced policy when the owning feature
  changes.

### Decisions requiring confirmation

* Confirm the supported multi-pattern `applyTo` syntax in the installed VS Code
  host. This blocks the exact frontmatter edit, not the instruction content.
* Confirm whether architecture violations should fail only the test stage
  (selected xUnit approach) or the build stage (custom MSBuild target). No
  repository CI workflow currently settles this operational preference.
* Decide whether the architecture test should derive the known project set from
  `TNC.Trading.Platform.slnx` or keep an explicit six-project set beside the
  matrix. Recommendation: keep the production set explicit so a new project
  fails closed until reviewed; use the solution only as an additional
  completeness check.

### Remaining implementation research

* [ ] Verify instruction activation experimentally for `.cs` and `.csproj`.
* [ ] Prototype the focused xUnit/XML check and compare its diagnostics with a
  minimal MSBuild-target prototype only if build-stage enforcement remains a
  requirement.
* [ ] Inventory Application ASP.NET Core and `Microsoft.Extensions` usages as a
  separate migration backlog; this is not required for the initial ratchet.
* [ ] Determine a future source-level rule before evaluating ArchUnitNET. Do not
  add an architecture package without a concrete rule that project-edge checks
  cannot express.

No remaining research gap blocks writing the normative instruction content,
the selected wiki updates, or the focused architecture test plan.

## References

* `.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md`