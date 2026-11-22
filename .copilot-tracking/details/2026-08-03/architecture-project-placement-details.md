<!-- markdownlint-disable-file -->
# Implementation Details: Architecture Project Placement

## Context Reference

Sources: `.copilot-tracking/research/2026-08-03/architecture-project-placement-research.md`, `.copilot-tracking/research/subagents/2026-08-03/architecture-project-placement-research.md`, `.github/instructions/folders.instructions.md`, `.github/instructions/architecture.instructions.md`, `.github/instructions/tests.instructions.md`, and the user request dated 2026-08-03.

## Implementation Phase 1: Verify and authorize cleanup

<!-- parallelizable: false -->

### Step 1.1: Confirm duplicate provenance and content

Before editing, inspect the working tree and tracked paths. Confirm the root directory contains only `TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs`, that the path is tracked, and that it is byte-identical to `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs`. Check history and diffs so user changes are not overwritten.

Files:

* `TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs` - Candidate stale duplicate
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs` - Canonical implementation to preserve

Discrepancy references:

* DR-01, DR-02, DD-01, and DD-02 in `.copilot-tracking/plans/logs/2026-08-03/architecture-project-placement-log.md`

Success criteria:

* No unreviewed user modification exists in either validator.
* The root file has no unique content or project metadata.

Validation commands:

```powershell
git status --short --untracked-files=all
git ls-files --stage -- TNC.Trading.Platform.Architecture test/TNC.Trading.Platform.Architecture
git diff -- TNC.Trading.Platform.Architecture test/TNC.Trading.Platform.Architecture
git log --all -- TNC.Trading.Platform.Architecture
rg -n --hidden --glob '!.git/**' 'TNC\.Trading\.Platform\.Architecture|TNC\.Trading\.Platform\.Architecture\.IntegrationTests' .
```

Review every reference-scan match in source, project files, solution files, documentation, scripts, and tracking artifacts. Any match that depends on the root path must be redirected to the canonical `test/` path before deletion or explicitly documented as historical evidence.

### Step 1.2: Confirm canonical project and solution membership

Inspect the canonical project inventory and solution listing. Record that the canonical `.csproj` is present and that the solution includes it exactly once. Do not add a second entry.

Files:

* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj` - Buildable architecture test project
* `TNC.Trading.Platform.slnx` - Existing solution membership

Success criteria:

* Exactly one architecture `.csproj` exists.
* Exactly one solution entry points to the project under `test/`.

Validation command:

```powershell
dotnet sln TNC.Trading.Platform.slnx list
```

### Step 1.3: Obtain deletion authorization

Because the root duplicate is tracked repository content, treat its removal as a substantive source deletion. Obtain explicit authorization before implementation. If authorization is not available, stop with the plan ready for handoff and make no source change.

Success criteria:

* Deletion authorization is recorded in the implementation context.

## Implementation Phase 2: Remove stale duplicate

<!-- parallelizable: false -->

### Step 2.1: Delete the root-level duplicate

Delete only `TNC.Trading.Platform.Architecture/`. Preserve the complete canonical project, namespaces, tests, documentation, and existing solution entry. Do not introduce a replacement file in the root.

Files:

* `TNC.Trading.Platform.Architecture/` - Delete stale duplicate directory
* `test/TNC.Trading.Platform.Architecture/` - Preserve unchanged
* `TNC.Trading.Platform.slnx` - Preserve unchanged in the current checkout

Success criteria:

* No root-level architecture directory remains.
* The canonical project remains intact and buildable.
* No unrelated files are changed.

Dependencies:

* Step 1.3 authorization

### Step 2.2: Stop on a divergent checkout

If verification finds that the canonical project is absent, stop implementation. Do not reconstruct or move a project from the root directory unless a follow-up research pass identifies an authoritative complete `.csproj` and source inventory. The current root inventory contains only one `.cs` file, so an unplanned move would create an incomplete, non-buildable test project.

Success criteria:

* No incomplete project is created from the partial root inventory.
* A divergent checkout is reported for follow-up research with its exact inventory and solution state.

Dependencies:

* Step 1.2 must show the canonical project is genuinely absent.
* A new research pass must identify the authoritative complete project before any move or solution edit is planned.

## Implementation Phase 3: Validate placement and solution integrity

<!-- parallelizable: false -->

### Step 3.1: Inspect final inventory and diff

Confirm the final project inventory and solution membership, then review the change summary. The expected current-checkout diff is a deletion of the root duplicate only.

Validation commands:

```powershell
Get-ChildItem -Recurse -File TNC.Trading.Platform.Architecture -ErrorAction SilentlyContinue
Get-ChildItem -Recurse -Filter *.csproj test/TNC.Trading.Platform.Architecture
dotnet sln TNC.Trading.Platform.slnx list
git diff --name-status
git diff --stat
```

Success criteria:

* One architecture `.csproj` and one solution entry remain.
* The diff contains only the authorized root duplicate deletion for the current checkout.

### Step 3.2: Run focused and full validation

Run the architecture project tests first, then the solution build, then the complete solution test suite. Use the SDK selected by `global.json` and do not suppress unrelated failures.

Validation commands:

```powershell
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj
dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet test TNC.Trading.Platform.slnx --no-restore
```

Success criteria:

* Focused architecture tests pass.
* The solution build passes.
* The complete solution test suite passes with zero failures.

### Step 3.3: Record blockers without scope expansion

If validation fails because of Docker, SDK, browser, service, or unrelated baseline issues, record the exact command and failure. Fix only narrow cleanup-related issues; defer unrelated product or test repairs to a separate work package.

Success criteria:

* Every failed command has an owner, cause, and next action recorded in the handoff.

## Dependencies

* Explicit authorization for tracked deletion
* .NET SDK from `global.json`
* Repository test prerequisites for the complete solution suite

## Success Criteria

* Placement and solution membership satisfy the repository folder rules.
* The canonical architecture suite remains the sole source of truth.
* Validation proves no duplicate project or solution entry was introduced.
