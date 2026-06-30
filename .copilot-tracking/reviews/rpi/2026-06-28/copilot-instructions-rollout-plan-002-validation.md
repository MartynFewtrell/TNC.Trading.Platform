---
title: Copilot Instructions Rollout Phase 2 Validation
description: Validation of Implementation Phase 2 against the recorded rollout changes and research requirements.
author: GitHub Copilot
ms.date: 2026-06-28
ms.topic: reference
keywords:
  - github copilot
  - custom instructions
  - validation
  - phase 2
estimated_reading_time: 5
---

## Validation Summary

* Phase: 2
* Status: Partial
* Outcome: Needs rework
* Coverage assessment: Phase 2 implementation content is largely present, but the required applicability-validation evidence for Step 2.4 is missing from the recorded implementation.

## Phase Requirements Checked

| Plan item | Requirement | Recorded status | Validation result | Evidence |
|-----------|-------------|-----------------|-------------------|----------|
| Step 2.1 | Create a short repository bootstrap file that identifies the repo as a .NET 10 Aspire solution, points to `docs/wiki`, names AppHost as the local composition root, preserves one-top-level-type-per-file guidance, and lists canonical validation commands | Marked complete | Verified | Plan requirement: `.copilot-tracking/plans/2026-06-28/copilot-instructions-rollout-plan.instructions.md` and `.copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md`; file evidence: `.github/copilot-instructions.md` |
| Step 2.2 | Create a scoped `.NET` validation instruction file for `src/**/*.cs` and `test/**/*.cs` focused on narrow-first validation order and repository command preferences | Marked complete | Verified | Plan requirement: `.copilot-tracking/plans/2026-06-28/copilot-instructions-rollout-plan.instructions.md` and `.copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md`; file evidence: `.github/instructions/dotnet-validation.instructions.md` |
| Step 2.3 | Create an AppHost and ServiceDefaults runtime instruction file with supported runtime and unsupported-pattern guidance | Marked complete | Verified | Plan requirement: `.copilot-tracking/plans/2026-06-28/copilot-instructions-rollout-plan.instructions.md` and `.copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md`; file evidence: `.github/instructions/apphost-runtime.instructions.md` |
| Step 2.4 | Validate the three files together, including representative Copilot applicability checks with expected instruction references in request context | Marked complete | Partially verified | Plan requirement: `.copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md`; recorded implementation lacks Phase 2 applicability-check evidence |

## Findings

### Major

* Step 2.4 is marked complete, but the recorded implementation does not provide the required behavior-level applicability evidence showing that the expected instruction references appeared in representative VS Code Copilot request context. The Phase 2 plan requires representative Copilot applicability checks for one C# file and one AppHost file, with acceptance based on expected instruction references in context. The changes log records Phase 2 as complete after authoring and validation, but the planning log only documents unavailable direct Copilot request-reference inspection for later phases and the release log does not capture an equivalent Phase 2 exception or substitute evidence. This leaves the required validation step unproven rather than completed.
  * Requirement evidence: `Implementation Phase 2`, `Step 2.4`, and success criteria in `.copilot-tracking/plans/2026-06-28/copilot-instructions-rollout-plan.instructions.md`.
  * Validation detail evidence: representative applicability checks and expected instruction references in `.copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md`.
  * Recorded completion claim: `.copilot-tracking/changes/2026-06-28/copilot-instructions-rollout-changes.md`.
  * Limitation evidence for later phases only: `.copilot-tracking/plans/logs/2026-06-28/copilot-instructions-rollout-log.md`.

### Minor

* The changes log summarizes the authored files accurately, but it does not distinguish structural validation from behavior validation for Phase 2. Given the Microsoft-guided validation model in the research, that omission reduces auditability of the rollout evidence.
  * Research evidence: behavior validation requirement in `.copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md`.
  * Recorded summary: `.copilot-tracking/changes/2026-06-28/copilot-instructions-rollout-changes.md`.

## Verified Evidence

### Step 2.1 bootstrap file

* The bootstrap file exists and stays compact.
* It identifies the repository as a .NET 10 Aspire solution.
* It points to `docs/wiki/` as the source of truth.
* It names `src/TNC.Trading.Platform.AppHost` as the supported local composition root.
* It preserves the one-top-level-type-per-file rule.
* It lists `dotnet build`, `dotnet test`, and `dotnet test -m:1`.

Evidence:
* `.github/copilot-instructions.md`
* `.copilot-tracking/changes/2026-06-28/copilot-instructions-rollout-changes.md`

### Step 2.2 validation instructions

* The validation instruction file exists with the expected `applyTo` scope of `src/**/*.cs, test/**/*.cs`.
* It expresses narrow-first validation order.
* It captures project-scoped command preference before broader solution validation.
* It retains the repository fallback to `dotnet test -m:1`.
* It keeps AppHost-backed suites as higher-cost validation.

Evidence:
* `.github/instructions/dotnet-validation.instructions.md`
* `.copilot-tracking/changes/2026-06-28/copilot-instructions-rollout-changes.md`

### Step 2.3 AppHost runtime instructions

* The runtime instruction file exists with the planned AppHost, ServiceDefaults, shared test-helper, and wiki `applyTo` scope.
* It keeps AppHost orchestration-focused.
* It keeps ServiceDefaults as the shared home for resilience and observability defaults.
* It encodes supported local workflow constraints.
* It forbids unsupported synthetic runtime paths and root-level runtime capture artifacts.

Evidence:
* `.github/instructions/apphost-runtime.instructions.md`
* `.copilot-tracking/changes/2026-06-28/copilot-instructions-rollout-changes.md`

## Deviations And Missing Work

* Missing: Recorded evidence that Step 2.4 ran representative Copilot applicability checks for at least one matching C# path and one matching AppHost path.
* Missing: Recorded evidence that the expected instruction references appeared in Copilot request context for those representative files.
* Deviation: Phase 2 is marked complete in the changes log without preserving the required behavior-validation evidence or documenting a Phase 2-specific limitation and fallback validation decision.

## Research Alignment Assessment

* The authored files align with the research recommendation to use a layered instruction model with a short root bootstrap plus scoped instruction files.
* The runtime instruction aligns with research that AppHost should remain orchestration-focused and ServiceDefaults should own shared resilience and observability defaults.
* The remaining gap is against the research-backed validation model, which requires validating that Copilot actually applied the scoped instructions rather than stopping at structural markdown review.

## Clarifying Questions

* Was Phase 2 applicability validation performed outside the tracked artifacts, such as through a transient VS Code request-context inspection that was never recorded?
* If direct Copilot request-reference inspection was unavailable during Phase 2 as well, should the changes log and planning log be corrected to record that limitation explicitly for this phase?

## Recommended Next Actions

1. Re-run or document Step 2.4 with representative applicability evidence for one C# file and one AppHost file.
2. If direct request-context inspection is still unavailable, record the limitation explicitly for Phase 2 and downgrade the phase completion claim to structural-only validation until behavior evidence exists.
3. Update the changes log or planning log so the Phase 2 completion record distinguishes authored-file verification from missing behavior-validation evidence.

## Final Verdict

Phase 2 is not fully complete as recorded. The authored bootstrap, validation, and runtime instruction files are present and materially aligned with the plan and research, but the required applicability-validation evidence for Step 2.4 is missing from the tracked implementation. The phase therefore validates as Partial and needs rework rather than Passed.