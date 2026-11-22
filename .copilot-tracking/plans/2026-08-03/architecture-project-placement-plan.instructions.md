<!-- markdownlint-disable-file -->
---
applyTo: '.copilot-tracking/changes/2026-08-03/architecture-project-placement-changes.md'
---
# Implementation Plan: Architecture Project Placement

## Overview

Remove the tracked, root-level duplicate architecture validator while retaining the complete architecture test project under `test/` and its existing single solution entry.

## Objectives

### User Requirements

* Place the architecture project under `test/` and ensure it is included in the solution. Source: User request, 2026-08-03.

### Derived Objectives

* Reconcile the reported state with the current checkout before changing files. Derived from verified research showing the canonical project and solution entry already exist.
* Remove duplicate tracked content only after confirming it has no unique implementation or references. Derived from repository status/history and folder rules.
* Preserve one buildable architecture test project and exactly one solution membership entry. Derived from project inventory and solution inspection.
* Validate the cleanup with focused tests, solution build, and full solution tests. Derived from repository test and architecture guidance.

## Context Summary

The current checkout already contains the complete project at `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/` and lists it in `TNC.Trading.Platform.slnx`. The root-level directory contains only a tracked, byte-identical copy of `ProjectGraphValidator.cs`, added independently in commit `d24754810f22471fe950d7ea3e2611340121b837`. Therefore the selected implementation is a guarded deletion of the root duplicate, not a second move or solution edit.

Research sources:

* `.copilot-tracking/research/2026-08-03/architecture-project-placement-research.md`
* `.copilot-tracking/research/subagents/2026-08-03/architecture-project-placement-research.md`
* `.github/instructions/folders.instructions.md`
* `.github/instructions/architecture.instructions.md`
* `.github/instructions/tests.instructions.md`

## Affected Files Tree

```text
TNC.Trading.Platform.Architecture/
  TNC.Trading.Platform.Architecture.IntegrationTests/
    ProjectGraph/ProjectGraphValidator.cs  # remove stale duplicate

test/
  TNC.Trading.Platform.Architecture/
    TNC.Trading.Platform.Architecture.IntegrationTests/  # preserve canonical project

TNC.Trading.Platform.slnx  # verify existing entry; change only if absent in target checkout
```

## Implementation Checklist

### [x] Implementation Phase 1: Verify and authorize cleanup

<!-- parallelizable: false -->

* [x] Step 1.1: Confirm the root path is tracked, clean or explicitly user-owned, and byte-identical to the canonical validator.
  * Details: `.copilot-tracking/details/2026-08-03/architecture-project-placement-details.md` (Lines 12-40)
* [x] Step 1.2: Confirm the canonical test project and exactly one solution entry already exist; do not add a duplicate.
  * Details: `.copilot-tracking/details/2026-08-03/architecture-project-placement-details.md` (Lines 42-60)
* [x] Step 1.3: Obtain explicit authorization to delete the tracked root duplicate if implementation is performed.
  * Details: `.copilot-tracking/details/2026-08-03/architecture-project-placement-details.md` (Lines 62-68)

### [x] Implementation Phase 2: Remove stale duplicate

<!-- parallelizable: false -->

* [x] Step 2.1: Delete only `TNC.Trading.Platform.Architecture/` and leave the canonical `test/` project and solution unchanged.
  * Details: `.copilot-tracking/details/2026-08-03/architecture-project-placement-details.md` (Lines 74-92)
* [x] Step 2.2: If the target checkout lacks the canonical project, stop and require a new research pass identifying an authoritative complete project before any move or solution edit.
  * Details: `.copilot-tracking/details/2026-08-03/architecture-project-placement-details.md` (Lines 94-106)

### [x] Implementation Phase 3: Validate placement and solution integrity

<!-- parallelizable: false -->

* [x] Step 3.1: Verify one architecture project and one solution entry remain, and inspect the diff for scope.
  * Details: `.copilot-tracking/details/2026-08-03/architecture-project-placement-details.md` (Lines 112-129)
* [x] Step 3.2: Run the focused architecture tests, solution build, and complete solution tests.
  * Details: `.copilot-tracking/details/2026-08-03/architecture-project-placement-details.md` (Lines 131-147)
* [x] Step 3.3: Record any environment or unrelated failures without broadening the change.
  * Details: `.copilot-tracking/details/2026-08-03/architecture-project-placement-details.md` (Lines 149-155)

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-03/architecture-project-placement-log.md` for discrepancy tracking, implementation paths, and follow-on work.

## Dependencies

* .NET SDK selected by `global.json` (`10.0.200` with `latestFeature` roll-forward)
* A clean or understood working tree before deleting tracked content
* Explicit authorization for the tracked duplicate deletion
* No additional package or framework dependency is required

## Success Criteria

* The only buildable architecture test project is under `test/TNC.Trading.Platform.Architecture/`. Traces to the user requirement and research findings.
* `TNC.Trading.Platform.slnx` contains exactly one architecture project entry pointing under `test/`. Traces to the user requirement and solution inventory.
* The root-level duplicate is absent, with no unique canonical source removed. Traces to DR-01 and the selected implementation path.
* Focused architecture tests, solution build, and complete solution tests pass, or failures are documented as environment blockers. Traces to repository validation guidance.
