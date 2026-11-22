---
applyTo: '.copilot-tracking/changes/2026-07-26/clean-architecture-guidance-update-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Portable Clean Architecture Guidance

## Overview

Extend the existing architecture instruction with reusable Clean Architecture principles based on responsibilities and inward source dependencies, add topology-neutral project graph integrity checks, and document the current repository structure as transitional rather than enforcing it as the target architecture.

## Objectives

### User Requirements

* Update `.github/instructions/architecture.instructions.md` to guide development toward Clean Architecture. — Source: User request, 2026-07-26.
* Do not enforce the current projects because the policy and adapter projects may change during refactoring. — Source: User clarification, 2026-07-26.
* Treat API, AppHost, ServiceDefaults, and Web only as likely survivors, not universal architecture layer names. — Source: User clarification, 2026-07-26.
* Make the instruction portable so the same guidance can be reused in future projects. — Source: User clarification, 2026-07-26.
* Update supporting documentation where it helps sustain this direction. — Source: Original user request, 2026-07-26.

### Derived Objectives

* Preserve compatible Vertical Slice and CQRS rules while defining Clean Architecture through conceptual responsibilities. — Derived from: Primary research and existing instruction ownership.
* Avoid fixed project counts, canonical layer projects, current project names, and allowed-edge matrices in normative guidance. — Derived from: Portability and planned refactoring.
* Enforce only topology-neutral graph integrity in this task: references resolve and the production project graph is acyclic. — Derived from: Stable invariants that do not require a boundary map.
* Defer project-level dependency direction enforcement until refactoring establishes stable repository-specific boundaries. — Derived from: Current Application and Infrastructure roles may change.
* Present current topology in the wiki as an implemented snapshot, not the Clean Architecture target. — Derived from: Documentation source-of-truth requirements without freezing transitional structure.

## Context Summary

### Project Files

* `.github/instructions/architecture.instructions.md` - Existing architecture owner to become portable across project topologies.
* `.github/lessons.md` - Durable learning location for invariant-over-project-name guidance.
* `docs/wiki/architecture.md` - Current implementation source of truth requiring transitional-topology clarification.
* `docs/wiki/testing-and-quality.md` - Test documentation requiring generic graph-check scope and limitations.
* `TNC.Trading.Platform.slnx` - Registration point for the architecture integration-test project.
* `src/**/*.csproj` - Discovered graph input; no specific production project is normative.

### References

* `.copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md` - Primary-source principles and point-in-time repository assessment.
* `.copilot-tracking/research/2026-07-26/clean-architecture-guidance-update-research.md` - Revised selected path and constraints.
* `.copilot-tracking/research/subagents/2026-07-26/portable-clean-architecture-guidance-research.md` - Impact analysis for portable guidance and generic enforcement.
* `https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html` - Dependency Rule source.
* `https://blog.cleancoder.com/uncle-bob/2016/01/04/ALittleArchitecture.html` - Policy/detail and inner-owned interface source.
* `https://www.informit.com/articles/article.aspx?p=2832399` - Publisher-authorized Dependency Rule excerpt.

### Standards References

* `.github/copilot-instructions.md` - Repository-wide test, wiki, and stable-learning requirements.
* `.github/instructions/architecture.instructions.md` - Existing Vertical Slice and CQRS rules to preserve.
* `.github/instructions/refactoring.instructions.md` - Existing incremental refactoring and narrow-abstraction rules.
* `.github/instructions/tests.instructions.md` - Integration-test taxonomy and test conventions.
* `.github/instructions/folders.instructions.md` - Test placement rules.
* `.github/instructions/token-discipline.instructions.md` - Existing-owner and concise customization guidance.

## Implementation Checklist

### [x] Implementation Phase 1: Confirm Instruction Activation

<!-- parallelizable: false -->

* [x] Step 1.1: Verify activation for production and test C# source and project files, using a narrow companion only if required.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 12-31)

### [x] Implementation Phase 2: Author Portable Architecture Guidance

<!-- parallelizable: false -->

* [x] Step 2.1: Broaden instruction ownership without naming or requiring a project topology.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 37-54)
* [x] Step 2.2: Add conceptual roles, inward dependency constraints, inner-owned ports, boundary translation, and composition rules.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 56-79)
* [x] Step 2.3: Add portable examples, incremental decision guidance, references, and validation limits.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 81-100)
* [x] Step 2.4: Validate activation and confirm the instruction contains no TNC-specific normative rules.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 102-112)

### [x] Implementation Phase 3: Add Topology-Neutral Graph Integrity Checks

<!-- parallelizable: false -->

* [x] Step 3.1: Create the dependency-light architecture integration-test project.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 118-135)
* [x] Step 3.2: Implement generic project discovery plus missing-target and cycle validation without role classification or allowed edges.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 137-159)
* [x] Step 3.3: Add repository and in-memory tests proving graph integrity and tolerance of valid topology changes.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 161-180)
* [x] Step 3.4: Register the suite and run focused validation without adding a repository boundary policy.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 182-198)

### [x] Implementation Phase 4: Align Supporting Documentation

<!-- parallelizable: false -->

* [x] Step 4.1: Update the architecture wiki with portable principles and mark the current topology as transitional.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 204-221)
* [x] Step 4.2: Document generic graph checks, focused execution, and enforcement limitations.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 223-239)
* [x] Step 4.3: Record the portable invariant-over-project-layout lesson.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 241-253)
* [x] Step 4.4: Validate consistency between instruction, wiki pages, and lesson.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 255-260)

### [ ] Implementation Phase 5: Full Validation and Handoff

<!-- parallelizable: false -->

* [x] Step 5.1: Run focused architecture tests, solution build/tests, and customization/Markdown checks.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 266-272)
* [x] Step 5.2: Verify portability and source-boundary guidance without fixed project assumptions.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 274-276)
* [x] Step 5.3: Fix isolated validation issues and rerun the narrow failing check.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 278-280)
* [x] Step 5.4: Record project-level direction enforcement as deferred until stable boundaries exist.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 282-284)
* [x] Step 5.5: Report unresolved validation failures with diagnostics and recommended next actions.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-guidance-update-details.md` (Lines 286-288)

## Planning Log

See `.copilot-tracking/plans/logs/2026-07-26/clean-architecture-guidance-update-log.md` for the superseded fixed-topology approach, selected portable path, and follow-on work.

## Dependencies

* .NET SDK `10.0.200`.
* Installed VS Code customization evaluation tooling.
* Existing xUnit and Microsoft.NET.Test.Sdk package baseline.
* Docker-backed infrastructure when full solution tests require Aspire services.
* Repository Markdown and frontmatter validation tooling when available.

## Success Criteria

* The architecture instruction guides an unrelated future project without TNC project knowledge. — Traces to: User portability requirement.
* Normative rules use responsibilities and dependency direction, not current project names, fixed project count, or canonical layer assemblies. — Traces to: User refactoring requirement.
* The instruction preserves compatible Vertical Slice and CQRS guidance while adding policy, use-case, adapter, boundary data, composition, and testability constraints. — Traces to: Primary research.
* The final instruction activates for production and test C# source and project files, with a validated narrow fallback if necessary. — Traces to: Activation research.
* Generic graph tests fail on missing targets and cycles but accept valid project additions, removals, and renames. — Traces to: Topology-neutral enforcement decision.
* No repository-specific boundary policy, allowed-edge matrix, role rank, unknown-project failure, or fixed production set is introduced. — Traces to: Deferred stable-boundary decision.
* Architecture and testing wiki pages distinguish portable direction from transitional implementation structure. — Traces to: Wiki source-of-truth requirement.
* No standalone Clean Architecture instruction, mandatory Domain project, architecture package, or broad production refactor is introduced. — Traces to: Selected incremental implementation path.
