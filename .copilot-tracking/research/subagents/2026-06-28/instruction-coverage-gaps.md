---
title: Copilot Instruction Coverage Gaps
description: Research summary of existing repository guidance relevant to GitHub Copilot customization and the instruction coverage gaps implied by current evidence
ms.date: 2026-06-28
ms.topic: reference
---

## Research scope

* Identify existing GitHub Copilot customization files and adjacent developer-guidance sources in the repository.
* Separate explicitly documented development behaviors from behaviors that are only implicit in codebase structure, test layout, or prior planning artifacts.
* Infer which future Copilot instruction files would add value, using only verified repository evidence.

## Status

* Complete

## Repository-local Copilot customization surfaces found

### Standard `.github` customization locations

Search findings:

* No `.github/copilot-instructions.md` file was found under the repository root.
* No `.github/instructions/` files were found under the repository root.
* No `.github/prompts/` files were found under the repository root.
* No `.github/agents/` files were found under the repository root.
* The repository does contain `.github/workflows/`, but the directory is empty.

Implication:

* The repository has no repo-local Copilot customization entry point in the locations GitHub Copilot would normally consume first.

### Nonstandard instruction-like artifacts

The only repository files matching `*.instructions.md` are plan artifacts under `.copilot-tracking`, not reusable repository-wide Copilot instructions:

* `.copilot-tracking/plans/2026-06-26/refactoring-and-testability-opportunities-plan.instructions.md`
* `.copilot-tracking/plans/2026-06-26/root-artifact-cleanup-plan.instructions.md`

Evidence that these are task-plan artifacts rather than shared repo instructions:

* `.copilot-tracking/plans/2026-06-26/refactoring-and-testability-opportunities-plan.instructions.md:1-3` declares an implementation plan and applies only to `.copilot-tracking/changes/2026-06-26/refactoring-and-testability-opportunities-changes.md`.
* `.copilot-tracking/plans/2026-06-26/root-artifact-cleanup-plan.instructions.md:1-3` declares an implementation plan and applies only to `.copilot-tracking/changes/2026-06-26/root-artifact-cleanup-changes.md`.

Implication:

* Instruction-like content exists, but it is scoped to plan execution artifacts rather than repository-wide developer assistance.

### Agent and prompt artifacts

Search findings:

* No repository-local `*.agent.md` files were found.
* No repository-local `*.prompt.md` files were found.

Related evidence:

* `docs/006-refactor-app/hve-agent-recommendations-for-refactoring-workflow.md:63-79` proposes future agents such as `Work Package Refactoring Reviewer`, `Refactoring Mitigation Planner`, and `Refactoring Mitigation Implementor`.
* `docs/006-refactor-app/hve-agent-recommendations-for-refactoring-workflow.md:188-231` proposes supporting helpers such as `Scope-to-Implementation Mapper` and `Validation Command Resolver`.

Implication:

* The repository contains guidance about possible future agent structure, but not actual repo-local Copilot agent or prompt files.

## Explicitly documented development behaviors

### Local runtime and orchestration guidance

Explicit behaviors:

* The supported local runtime uses Docker-managed infrastructure: `docs/wiki/local-development.md:12`.
* `AppHost` is the local composition root and should remain thin, with focused support files handling registration and shared wiring: `docs/wiki/local-development.md:14`.
* There is no supported synthetic AppHost runtime path for local startup or AppHost-backed distributed validation: `docs/wiki/local-development.md:25`.
* The canonical run command is `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`: `docs/wiki/local-development.md:40`.
* Root-level output capture is explicitly forbidden for AppHost logs and dashboard captures: `docs/wiki/local-development.md:49`.

### Build and test guidance

Explicit behaviors:

* The canonical build command is `dotnet build`: `docs/wiki/local-development.md:32`.
* The canonical repository-wide test command is `dotnet test`: `docs/wiki/local-development.md:132`, `docs/wiki/testing-and-quality.md:197`.
* A serialized fallback `dotnet test -m:1` is documented for intermittent MSBuild child-node exits: `docs/wiki/local-development.md:138`, `docs/wiki/testing-and-quality.md:203`.
* The repository uses multiple test levels from unit to browser-driven flows: `docs/wiki/testing-and-quality.md:7`.
* The distributed validation model intentionally keeps AppHost-backed coverage narrow and evidence-driven: `docs/wiki/testing-and-quality.md:122`.

### Test architecture and quality expectations

Explicit behaviors:

* AppHost-focused unit tests should cover settings, wiring, registration, and topology smoke before higher-cost distributed suites run: `docs/wiki/testing-and-quality.md:28`.
* Proof-data unit tests are deterministic and do not require IG credentials or network access for normal `dotnet test` runs: `docs/wiki/testing-and-quality.md:126-139`.
* The quality checklist for future changes explicitly says to preserve secret safety, Test-versus-Live safety, degraded-state UI, health endpoint stability, environment tagging, and deterministic retry behavior: `docs/wiki/testing-and-quality.md:248-255`.

### Code-organization guidance

Explicit behaviors:

* Non-generated C# code should keep one top-level type per file with a matching file name: `README.md:45`.

### Workflow and planning guidance for refactoring work

Explicit behaviors, but only inside docs and `.copilot-tracking` artifacts rather than repo-level Copilot instructions:

* Test execution should be treated as a phase gate rather than an end-of-project activity: `.copilot-tracking/plans/2026-06-26/refactoring-and-testability-opportunities-plan.instructions.md:24`.
* Expensive distributed authentication and browser-backed suites should remain milestone regression coverage while cheaper seams grow below them: `.copilot-tracking/plans/2026-06-26/refactoring-and-testability-opportunities-plan.instructions.md:25`.
* Shared harness consolidation should happen only after production seams become clear: `.copilot-tracking/plans/2026-06-26/refactoring-and-testability-opportunities-plan.instructions.md:26`.
* The supported local run workflow should forbid root-level capture files and provide approved alternatives: `.copilot-tracking/plans/2026-06-26/root-artifact-cleanup-plan.instructions.md:104`.

## Behaviors that are documented only indirectly or remain implicit for Copilot

These behaviors are present in docs, plans, or repository structure, but are not codified in a repo-local Copilot customization surface.

### Command selection and validation cadence remain scattered

Evidence:

* Canonical build and test commands are documented in `docs/wiki/local-development.md:32-40` and `docs/wiki/local-development.md:132-143`.
* Validation-as-phase-gate behavior is documented only in `.copilot-tracking/plans/2026-06-26/refactoring-and-testability-opportunities-plan.instructions.md:24-26`.
* `docs/006-refactor-app/hve-agent-recommendations-for-refactoring-workflow.md:221-231` explicitly recommends a future `Validation Command Resolver`.

Gap:

* No repo-local Copilot instruction currently tells an agent which commands to prefer by default, when to use serialized test execution, or how to preserve the repository's immediate-versus-milestone validation pattern.

### Documentation-sync expectations remain advisory

Evidence:

* `README.md:49-51` routes developers to the local-development and documentation index files.
* `docs/006-refactor-app/hve-agent-recommendations-for-refactoring-workflow.md:276` says mitigations should determine whether architecture, local development, testing guidance, or runtime behavior docs need updates.

Gap:

* No repo-local Copilot instruction states when code changes must be accompanied by updates to `README.md`, `docs/wiki/local-development.md`, `docs/wiki/testing-and-quality.md`, or other wiki pages.

### Refactoring workflow structure is proposed, not enforced

Evidence:

* `docs/006-refactor-app/hve-agent-recommendations-for-refactoring-workflow.md:83-151` defines reviewer, planner, and implementor responsibilities.
* `docs/006-refactor-app/hve-agent-recommendations-for-refactoring-workflow.md:188-231` defines bounded helper roles.
* `docs/006-refactor-app/hve-process-understanding.md:64-87` describes the Research, Plan, Implement, Review flow and the distinct goals of each phase.

Gap:

* None of this is implemented as repo-local `.github/agents/`, `.github/prompts/`, or `.github/instructions/` files, so Copilot has no repository-native workflow guardrails to apply automatically.

### Sensitive operational and runtime rules are not packaged as agent-safe guardrails

Evidence:

* Local runtime must stay Keycloak- and Docker-based rather than synthetic: `docs/wiki/local-development.md:12-25`.
* Root-level output capture is unsupported: `docs/wiki/local-development.md:49`.
* Distributed auth coverage should stay intentionally narrow: `docs/wiki/testing-and-quality.md:122`.
* Future quality changes must preserve secret safety and environment-safety expectations: `docs/wiki/testing-and-quality.md:248-255`.

Gap:

* These are important operational constraints, but they are only present in human-facing docs and task artifacts, not in always-applied repo-local Copilot instructions.

## Inferred instruction coverage gaps

### Gap 1: Repository bootstrap guidance for Copilot is missing

Why this is a gap:

* There is no `.github/copilot-instructions.md`.
* There are no repo-local `.github/instructions/`, `.github/prompts/`, or `.github/agents/` files.

What a future file should cover:

* Canonical repo overview.
* Preferred entry points for build, run, and test.
* The rule that AppHost is the supported local composition root.
* The rule that agents should prefer docs/wiki as the current-state source of truth.

### Gap 2: Validation-command and test-cadence guidance is missing

Why this is a gap:

* Command guidance exists, but it is spread across docs and planning artifacts.
* The repository has an explicit validation philosophy, but only in `.copilot-tracking` plan instructions.

What a future file should cover:

* Default validation commands.
* When to use `dotnet test` versus `dotnet test -m:1`.
* Which changes require only focused unit tests versus milestone AppHost-backed suites.
* The expectation that validation happens during a change, not only at the end.

### Gap 3: Documentation-update heuristics are missing

Why this is a gap:

* Runtime, testing, and operator behavior are heavily documented, but no repo-local instruction tells Copilot when those docs must be updated.

What a future file should cover:

* Triggers for updating `README.md`.
* Triggers for updating `docs/wiki/local-development.md` when run surfaces, credentials, or runtime topology change.
* Triggers for updating `docs/wiki/testing-and-quality.md` when test architecture, suite ownership, or quality evidence changes.

### Gap 4: Refactoring-workflow orchestration guidance is missing

Why this is a gap:

* The repository already contains a strong proposed HVE workflow, but only as prose in `docs/006-refactor-app/`.

What a future file should cover:

* When to use research-first versus direct implementation.
* When to produce durable plan artifacts.
* How to split review, planning, implementation, and validation responsibilities.
* How to keep delegation bounded for repository scans, evidence gathering, and validation resolution.

### Gap 5: Operational safety guardrails are missing from Copilot-consumable instructions

Why this is a gap:

* The repository has explicit safety constraints around secret handling, Test-versus-Live behavior, distributed auth topology, and root-artifact avoidance, but those constraints are not packaged as repo-level Copilot rules.

What a future file should cover:

* Do not reintroduce synthetic AppHost runtime paths for local startup or AppHost-backed validation.
* Do not add root-level log or dashboard capture outputs.
* Preserve secret-safe behavior and redaction expectations.
* Preserve narrow high-cost distributed coverage unless lower-cost seams cannot prove the behavior.

## Candidate instruction-file topics

Recommended repository-local customization topics, ordered by likely value:

1. `.github/copilot-instructions.md`
   * Repo overview, source-of-truth docs, canonical commands, and high-level safety rules.
2. `.github/instructions/dotnet-validation.instructions.md`
   * Build and test command defaults, serialized fallback usage, and immediate-versus-milestone validation cadence.
3. `.github/instructions/apphost-runtime.instructions.md`
   * Supported local runtime topology, Keycloak and Docker expectations, no synthetic AppHost path, and no root-capture artifacts.
4. `.github/instructions/docs-sync.instructions.md`
   * When code changes require updates to `README.md`, `docs/wiki/local-development.md`, `docs/wiki/testing-and-quality.md`, and related wiki pages.
5. `.github/instructions/refactoring-workflow.instructions.md`
   * Research, planning, implementation, and review expectations for larger refactoring work.
6. `.github/instructions/testing-strategy.instructions.md`
   * Pyramid shape, narrow distributed-suite principle, preferred low-cost seams, and real-runtime versus deterministic test boundaries.

## Summary

The repository already documents substantial development guidance, but it is distributed across `README.md`, `docs/wiki/`, `docs/006-refactor-app/`, and `.copilot-tracking/` plan artifacts rather than in repo-local Copilot customization files. The largest verified gaps are the absence of a repo-level Copilot entry point, the lack of command and validation-cadence instructions, missing documentation-sync heuristics, and the absence of Copilot-consumable operational guardrails for AppHost, test topology, and safety-sensitive behaviors.

## Recommended next research

- [ ] Inspect current code-review or pull-request templates, if any are added later, to align future Copilot guidance with human review expectations.
- [ ] Sample a few representative `src/` and `test/` projects for additional implicit conventions that are not yet surfaced in docs.
- [ ] Compare the proposed candidate instruction topics against any organization-level Copilot setup outside this repository before implementation.

## Clarifying questions

* None. The requested repository-evidence scan was sufficient to identify the current guidance surfaces and the missing repo-local Copilot customization areas.