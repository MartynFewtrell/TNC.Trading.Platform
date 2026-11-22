<!-- markdownlint-disable-file -->
---
description: "Research findings for resolving the tracked root-level architecture test duplicate"
ms.date: 2026-08-03
---
# Architecture Project Placement Research

## Scope

Resolve the reported placement and solution-membership issue for `TNC.Trading.Platform.Architecture` while preserving the repository's established `test/` layout and avoiding duplicate projects.

## Verified Findings

* The tracked root-level path `TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs` is the only file in that root directory. It has no `.csproj` and cannot be built independently. The implementation is recorded in the subagent research at `.copilot-tracking/research/subagents/2026-08-03/architecture-project-placement-research.md`.
* The complete architecture xUnit project already exists at `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/`, including its `.csproj`, graph model, graph tests, and architecture-role tests.
* `TNC.Trading.Platform.slnx` already contains exactly one architecture project entry for the canonical `test/` project at the solution's `/test/` group.
* The root and canonical `ProjectGraphValidator.cs` files are byte-identical. The root path is tracked, clean, and was added independently in commit `d24754810f22471fe950d7ea3e2611340121b837` on `origin/008-refactor`.
* Existing documentation and quality-gate artifacts refer to the `test/` project as the active architecture suite.
* The architecture suite is an in-process xUnit project and does not launch Aspire; Aspire closed-box test instructions do not require changes for this cleanup.

## Constraints

* Repository folder rules require automated tests under `test/` and prohibit parallel top-level test roots.
* Do not create a second `.csproj` or solution entry.
* Because the root duplicate is tracked repository content, deleting it is an implementation change that must be authorized and reviewed, not treated as ignored generated output.
* Preserve the canonical `test/` project and its existing solution membership unchanged.

## Selected Resolution

Treat the `test/` project as the source of truth and remove only the stale root-level duplicate after verifying it remains byte-identical and has no unique references. Do not modify `TNC.Trading.Platform.slnx` unless validation reveals the canonical entry is actually absent in the implementation checkout.

## Fallback Resolution

If implementation occurs in a checkout where the canonical project is absent, move the complete project directory into `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/` and add exactly one solution entry. This fallback is not expected for the current checkout and must not be applied in addition to the existing canonical project.

## Validation

* Confirm tracked paths and clean or user-modified state with `git status --short --untracked-files=all`, `git ls-files`, `git diff`, and `git log --all -- <path>` before deletion.
* Confirm one architecture `.csproj` and one solution entry with `dotnet sln TNC.Trading.Platform.slnx list` and targeted file inventory checks.
* Run the focused architecture test project, solution build, and complete solution test suite after the cleanup.
* Review `git diff --name-status` and `git diff --stat` to confirm the only intended source change is deletion of the root duplicate.
