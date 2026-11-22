# Architecture Project Placement Research

- Date: 2026-08-03
- Scope: Determine the current contents, references/usages, solution/project layout, neighboring test conventions, related architecture integration tests, and safest implementation path for moving `TNC.Trading.Platform.Architecture` under `test/` and adding it to `TNC.Trading.Platform.slnx`.
- Constraint: Research only. No product or test files are to be edited.

## Research Questions

- What exact files and project metadata currently exist in `TNC.Trading.Platform.Architecture`?
- Where is the project referenced, imported, or otherwise used?
- How is the solution and test-project layout organized?
- What neighboring test conventions apply?
- Are there related architecture integration tests elsewhere?
- What is the safest move-and-solution-update path, and how should it be validated?

## Findings

### Current out-of-tree directory

- `TNC.Trading.Platform.Architecture/` exists at the repository root.
- Its complete current recursive inventory is one file:
	`TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs`.
- The root-level directory contains no `.csproj`, solution file, project manifest,
	test configuration, or other source file. The lone file declares
	`TNC.Trading.Platform.Architecture.IntegrationTests.ProjectGraph.ProjectGraphValidator`
	and implements missing-target and cycle validation (`.../ProjectGraphValidator.cs:1-70`).
- The same `ProjectGraphValidator.cs` exists under the canonical test project with
	the same namespace and implementation:
	`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs:1-70`.
	This is strong evidence that the root-level directory is a stale duplicate or
	partial leftover, not an independent project that can be moved as-is.

### Live architecture integration-test project

The complete project is already under `test/`:

`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/`

Its exact current files are:

- `TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
- `ArchitectureRules/ProductionProjectRolePolicy.cs`
- `ArchitectureRules/ProductionProjectRoleValidator.cs`
- `ArchitectureRules/ProductionProjectRoleValidatorTests.cs`
- `ArchitectureRules/RepositoryArchitectureRuleTests.cs`
- `ProjectGraph/ProjectGraph.cs`
- `ProjectGraph/ProjectGraphDiagnostic.cs`
- `ProjectGraph/ProjectGraphLoader.cs`
- `ProjectGraph/ProjectGraphValidator.cs`
- `ProjectGraph/ProjectGraphValidatorTests.cs`
- `ProjectGraph/ProjectNode.cs`
- `ProjectGraph/RepositoryProjectGraphTests.cs`

The project file targets `net10.0`, enables implicit usings and nullable reference
types, is not packable, and has no `ProjectReference` items. Its only package
references are `coverlet.collector` 6.0.4, `Microsoft.NET.Test.Sdk` 17.14.1,
`xunit` 2.9.3, and `xunit.runner.visualstudio` 2.8.2
(`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj:1-21`).
It is therefore an in-process xUnit architecture test project, not an Aspire
closed-box integration project: it does not reference `Aspire.Hosting.Testing`,
the AppHost, or any production project.

The graph model normalizes paths and stores project references
(`ProjectGraph.cs:3-21`, `ProjectNode.cs:1-3`, and
`ProjectGraphDiagnostic.cs:1-3`). `ProjectGraphLoader` enumerates all
`src/**/*.csproj`, loads XML, and resolves `ProjectReference` paths
(`ProjectGraphLoader.cs:5-32`). `ProjectGraphValidator` checks missing targets and
cycles (`ProjectGraphValidator.cs:3-70`). `RepositoryProjectGraphTests` locates
the repository by searching for `TNC.Trading.Platform.slnx`, loads the production
graph, and asserts zero diagnostics (`RepositoryProjectGraphTests.cs:8-24`).

The architecture-role suite adds repository-specific checks: application
outward project/framework/package references, infrastructure-to-application
direction, conditional Domain purity, and provider protocol terms in Application
source (`ArchitectureRules/ProductionProjectRolePolicy.cs:3-20`,
`ProductionProjectRoleValidator.cs:3-95`). Its tests use temporary repositories
and disposable cleanup (`ProductionProjectRoleValidatorTests.cs:3-151`).
`RepositoryArchitectureRuleTests` combines graph and role validation against the
current `src` tree (`ArchitectureRules/RepositoryArchitectureRuleTests.cs:5-30`).

### Solution and project layout

- `TNC.Trading.Platform.slnx` already contains the architecture project at
	`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
	(`TNC.Trading.Platform.slnx:10-12`).
- The solution has a `/src/` folder containing six production projects, a `/test/`
	folder, and service-oriented test folders for Architecture, Api, AppHost,
	Application, Infrastructure, and Web (`TNC.Trading.Platform.slnx:1-32`).
- Neighboring test projects follow the same shape:
	`test/<Service>/<Service>.<TestType>/<Service>.<TestType>.csproj`.
	Unit and non-Aspire integration projects generally have test SDK, xUnit, and
	coverlet references; AppHost-backed API/Web suites add
	`Aspire.Hosting.Testing` and reference the AppHost. Examples are
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:1-35`,
	`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:1-29`,
	and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:1-35`.
- `global.json` pins SDK `10.0.200` with `latestFeature` roll-forward
	(`global.json:1-6`).

### References and usages

- The only active solution reference is the canonical `test/...` project path
	(`TNC.Trading.Platform.slnx:12`).
- The only active documentation references are the focused test command and test
	inventory entry in `docs/wiki/testing-and-quality.md:51`, `:83`, and `:451`.
- Source namespaces and type usages are confined to the canonical test tree; the
	project has no project references and no production code references it.
- Historical repository research records also describe the canonical `test/...`
	project as the active architecture project and identify the root-level directory
	as having no project listed in the solution
	(`.copilot-tracking/research/subagents/2026-07-26/clean-architecture-migration-plan-verification.md:82-85`).
- Build artifacts and prior quality-gate logs contain output paths under
	`test/TNC.Trading.Platform.Architecture/...`, corroborating that the `test/`
	project is the one executed. `artifacts/` is ignored by `.gitignore` and is not
	source authority (`.gitignore:55-66`).

### Related architecture integration tests

There is one related architecture integration-test project, already in the
expected location. It contains six graph-focused tests covering missing targets,
direct cycles, transitive cycles, valid acyclic graphs, valid additions/renames,
and valid removal of an unreferenced project
(`ProjectGraph/ProjectGraphValidatorTests.cs:3-132`), plus repository graph and
role-boundary checks described above. No second architecture `.csproj` or related
architecture test project exists under the root-level out-of-tree directory.

## Relevant Instructions

- `.github/instructions/folders.instructions.md` applies globally and requires
	automated tests under `test/`, preserving the existing top-level layout and
	updating references when files move.
- `.github/instructions/tests.instructions.md` requires separate test projects by
	test type, xUnit for unit tests, descriptive test files, deterministic tests,
	and test method names in `MethodName_StateUnderTest_ExpectedResult` form.
- `.github/instructions/aspire-tests.instructions.md` requires
	`Aspire.Hosting.Testing`, `DistributedApplicationTestingBuilder`, external
	boundaries, and disposal for tests that launch an AppHost. It does not require
	changes here because this architecture project does not launch Aspire.
- `.github/instructions/architecture.instructions.md` requires testable inward
	boundaries and project-graph validation as a separate concern; the current
	architecture suite is consistent with that graph-integrity role.
- `.github/instructions/csharp.instructions.md` requires one top-level type per
	file, file/type naming alignment, nullable-safe code, and tests for behavior
	changes. The existing architecture files follow the one-type-per-file pattern.
- `.github/copilot-instructions.md` and the folder instructions both prohibit
	introducing a parallel top-level test root and prefer the established `test/`
	hierarchy.

## Safest Implementation Path

The premise that the architecture project is outside `test/` and absent from the
solution does not match the current working tree. The project is already in
`test/` and already listed in `TNC.Trading.Platform.slnx`. The safest plan is:

1. Treat `test/TNC.Trading.Platform.Architecture/...` as the source of truth.
2. Before deleting anything, compare the root duplicate with the canonical
	 `ProjectGraphValidator.cs` and inspect version-control status/history to
	 determine whether the root directory is an accidental leftover or an
	 intentional untracked work area.
3. If it is stale, remove only the root-level
	 `TNC.Trading.Platform.Architecture/` duplicate. Do not move or recreate the
	 canonical test project and do not add a second solution entry.
4. If a future checkout differs and the canonical `test/` project is genuinely
	 absent, move the complete project directory (all 12 files) to
	 `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/`,
	 preserve namespaces and relative paths, and add exactly the solution entry
	 already shown at `TNC.Trading.Platform.slnx:12`.
5. Run the focused project test, solution project-list check, and solution build;
	 then run the full solution test gate if the change is accepted.

This avoids duplicate namespaces, duplicate project identities, and accidental
loss of the five architecture-role files that are not present in the root-level
directory.

## Unresolved Questions

- Is deletion of the tracked root-level duplicate explicitly authorized? The
	current checkout does not show an unfinished move: the canonical project and
	solution entry already exist under `test/`, while the root file is a clean,
	tracked parallel addition.

### Version-control provenance and working-tree status

- `git status --short --untracked-files=all` returned no entries. The working
	tree is clean overall, including both architecture locations; there are no
	unstaged, staged, or untracked changes for the duplicate.
- `git log --all -- TNC.Trading.Platform.Architecture` returned exactly one
	commit: `d24754810f22471fe950d7ea3e2611340121b837` (`origin/008-refactor`,
	`008-refactor`), dated 2026-07-26, titled `Clarify Clean Architecture and add
	project graph tests`.
- That commit added the root-level file as an `A` path. The same commit also
	contains the complete canonical test project under `test/`; this is a
	parallel addition, not a later move from the root path.
- `git ls-files --stage` reports the root duplicate and canonical copy as
	separately tracked paths. Both `ProjectGraphValidator.cs` entries have the
	same blob ID `422f297ba874ae0cf5076e02f8ec8deaa936150a`, proving their tracked
	contents are identical at `HEAD`. The canonical `.csproj` and its other
	files are tracked only below `test/`; no root-level project file exists.
- `git diff -- TNC.Trading.Platform.Architecture` and
	`git diff --cached -- TNC.Trading.Platform.Architecture` are empty. The root
	path is not ignored (`git check-ignore -v` produced no match), so it is a
	tracked repository duplicate rather than a generated or ignored local file.

### Related tracking artifacts reviewed

- `.copilot-tracking/research/2026-07-26/clean-architecture-migration-plan-verification.md`
	states that the root directory has no solution-listed project and identifies
	the `test/` project as active.
- `.copilot-tracking/research/2026-07-26/clean-architecture-migration-research.md`
	uses the canonical `test/` architecture project for graph-validator evidence
	and focused validation.
- `.copilot-tracking/plans/2026-07-26/clean-architecture-migration-plan.instructions.md`
	places `TNC.Trading.Platform.Architecture/` under the `test/` tree in its
	proposed layout; it does not justify retaining a second root-level copy.
- `.copilot-tracking/changes/2026-07-26/clean-architecture-guidance-update-changes.md`
	records that the architecture suite was added under `test/`, registered in the
	solution, and that no architecture files were removed. This supports treating
	root cleanup as a separate explicit change rather than as an unfinished move.

### User-change risk and plan guardrails

- Because the duplicate is tracked and clean, removing it changes repository
	content and must not be presented as removal of an untracked build artifact.
	Do not modify the canonical `test/` project or add another solution entry.
- Before any cleanup, preserve the canonical copy and verify the root file is
	byte-identical; then remove only the root-level duplicate if that is explicitly
	approved. Review `git diff --name-status` and `git diff --stat` immediately
	afterward to ensure the deletion is the sole intended change.
- Validate that exactly one architecture `.csproj` remains, the solution has one
	architecture project entry, and the focused architecture test still passes.
- Existing tracking records describe placement, but none records a user request
	to delete this tracked root path. The open decision is therefore scope/intent,
	not provenance: the evidence supports stale duplicate classification, while
	deletion still needs explicit implementation authority.

## Recommended Validation

- Confirm both locations and the duplicate before editing:
	`Get-ChildItem -Recurse TNC.Trading.Platform.Architecture` and
	`Get-ChildItem -Recurse test/TNC.Trading.Platform.Architecture`.
- Confirm the solution sees the canonical project:
	`dotnet sln TNC.Trading.Platform.slnx list`.
- Run the focused architecture suite:
	`dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`.
- Build the solution graph:
	`dotnet build TNC.Trading.Platform.slnx --no-restore`.
- After any move/removal, repeat the focused test and solution build, then run:
	`dotnet test TNC.Trading.Platform.slnx --no-restore`.
- For a move-only change, inspect `git diff --name-status` and verify there is
	exactly one architecture `.csproj`, one canonical solution entry, and no stale
	root-level duplicate.
