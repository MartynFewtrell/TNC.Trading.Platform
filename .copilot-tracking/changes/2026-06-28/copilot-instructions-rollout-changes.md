<!-- markdownlint-disable-file -->
# Release Changes: Copilot Instructions Rollout

**Related Plan**: copilot-instructions-rollout-plan.instructions.md
**Implementation Date**: 2026-06-28

## Summary

Implement the first repository-local Copilot instruction set under `.github` as a compact seven-file rollout aligned to repository guidance and Microsoft Learn recommendations.

Structural rollout work is complete, but behavior-level Copilot applicability validation remains open because the recorded implementation did not capture the request-context evidence required by the plan.

The validation guidance has now been tightened so source and test changes must always finish with repository-root `dotnet build` and `dotnet test` execution, with `dotnet test -m:1` reserved for the known intermittent MSBuild child-node fallback.

## Changes

### Added

* `.github/copilot-instructions.md`
	* Added the repository-wide Copilot bootstrap for the .NET 10 Aspire solution.
	* Pointed Copilot to `docs/wiki` as the source of truth, named AppHost as the supported local composition root, preserved the one-top-level-type-per-file rule, and listed the canonical validation commands including the `dotnet test -m:1` fallback.
* `.github/instructions/dotnet-validation.instructions.md`
	* Added focused validation-order guidance for `src/**/*.cs` and `test/**/*.cs`.
	* Encoded narrow-first validation preferences, escalation rules, and the repository fallback for intermittent MSBuild child-node instability.
* `.github/instructions/apphost-runtime.instructions.md`
	* Added AppHost and ServiceDefaults runtime constraints for supported local orchestration, AppHost-backed validation, and unsupported root-level runtime artifacts.
* `.github/instructions/architecture-boundaries.instructions.md`
	* Added repository-specific layer responsibility and dependency-direction guidance for Application, Infrastructure, Api, Web, AppHost, and ServiceDefaults.
	* Encoded stable ownership boundaries and cross-project drift patterns to avoid without expanding into generic C# style guidance.
* `.github/instructions/testing-strategy.instructions.md`
	* Added repository-specific test-pyramid and cost-aware coverage guidance for `src/**/*.cs` and `test/**/*.cs`.
	* Encoded cheap-first test selection, narrow high-cost AppHost-backed coverage expectations, and explicit complementarity with the validation instructions.
* `.github/instructions/docs-sync.instructions.md`
	* Added repository-specific documentation-sync guidance for `src/**`, `test/**`, `README.md`, and `docs/**/*.md`.
	* Encoded source-of-truth routing back to `README.md`, `docs/README.md`, and the wiki pages that must stay aligned when code, runtime, API, operator, or test behavior changes.
	* Tightened the documentation triggers so application and test changes now require an explicit `docs/wiki/` relevance review, with updates to the appropriate wiki page whenever the change affects current behavior, runtime behavior, API shape, operator flow, architecture responsibilities, or testing guidance.
* `.github/instructions/refactoring-workflow.instructions.md`
	* Added repository-specific thresholds for when larger or riskier work should use `.copilot-tracking` research, planning, review, and bounded implementation slices.
	* Encoded a direct-edit threshold so trivial fixes are not forced into the heavier workflow, while cross-project or high-risk changes still reuse the repository's existing planning artifacts.

### Modified

* `.github/copilot-instructions.md`
	* Replaced the previous narrow-first completion message with an explicit repository rule that any code or test change must finish with repository-root `dotnet build` and `dotnet test` execution.
	* Kept narrower project-scoped validation as iteration-time feedback only and preserved the `dotnet test -m:1` fallback for intermittent MSBuild child-node instability.
* `.github/instructions/dotnet-validation.instructions.md`
	* Reworked the validation guidance so `src/**/*.cs` and `test/**/*.cs` changes now require final repository-root `dotnet build` and `dotnet test` passes before work is considered complete.
	* Repositioned focused project or filtered validation as supplemental iteration guidance instead of the completion gate.

* `.copilot-tracking/plans/2026-06-28/copilot-instructions-rollout-plan.instructions.md`
	* Marked Implementation Phase 1 complete.
	* Added the explicit seven-file single-responsibility mapping.
	* Added the baseline `applyTo` scope decisions, accepted overlap rules, and the four-part validation model.
	* Reclassified Phases 2 through 5 as validation-incomplete while preserving the completed authoring and structural-review steps.
	* Reopened Steps 2.4, 3.3, 4.3, and 5.1 because the required request-context applicability evidence was not recorded.
* `.copilot-tracking/plans/logs/2026-06-28/copilot-instructions-rollout-log.md`
	* Recorded that Phase 1 found no discrepancies against the research-backed seven-file baseline.
	* Logged one low-priority follow-on check for possible future `docs-sync` scope narrowing if applicability evidence shows noise.
	* Recorded that Phase 3 introduced no scope discrepancies and used structural representative-file validation because direct Copilot reference inspection was not available in this execution path.
	* Recorded that Phases 4 and 5 introduced no new scope conflicts, kept the rollout at the compact seven-file baseline, and retained the same validation limitation around unavailable direct Copilot request-reference inspection.
	* Added explicit representative-file evidence for the completed structural scope reviews and separated that evidence from the still-missing behavior-level applicability checks.
	* Added the later validation attempt against Copilot debug surfaces, including the confirmed command IDs, the unmet command preconditions, and the fact that no prompt-export artifact was emitted to the session debug-log store.

### Removed

* None.

## Additional or Deviating Changes

* The repository validation rule was tightened after the initial rollout authoring so final validation is now mandatory at repository scope for every source or test change.
	* Reason: The user explicitly required Copilot to always build the project and run tests after code or test changes to protect application integrity.
* The latest repository-root validation run did not complete cleanly even though the instruction update itself is in place.
	* Reason: `dotnet build` succeeded, but `dotnet test` failed in existing AppHost-backed authentication suites that timed out while waiting for runtime listener discovery. This was not the known MSBuild child-node issue that would justify the `dotnet test -m:1` fallback.

* No repository-facing governance documentation was added in Phase 5 because the rollout did not introduce extra maintainership guidance beyond the instruction files and tracking artifacts themselves.
* The rollout status is now classified as structurally complete but validation-incomplete.
	* Reason: The plan requires representative VS Code Copilot request-context evidence for Steps 2.4, 3.3, 4.3, and 5.1, and that evidence is still not present in the tracked implementation artifacts.
* An automated capture attempt was completed from the current VS Code session but did not unblock the missing validation evidence.
	* Reason: The relevant Copilot debug commands exist, but the raw-request and prompt-export commands reported unmet preconditions and did not produce durable request-context artifacts that could be attached to the rollout record.

## Release Summary

Phase 1 is complete. Phases 2 through 5 are structurally implemented but remain open for behavior-level validation. The rollout has the full seven-file repository-local instruction baseline, scoped `applyTo` decisions, overlap review, and recorded follow-on items without expanding the initial file set speculatively. The `docs-sync` instruction set now also explicitly requires a `docs/wiki/` review whenever application or test changes are made, closing the previous ambiguity around when wiki maintenance must be considered. The validation instructions and root bootstrap now also require repository-root `dotnet build` and `dotnet test` after any source or test change, while keeping `dotnet test -m:1` as a targeted fallback for the known intermittent MSBuild child-node issue. Remaining work is limited to capturing the representative Copilot applicability evidence required by Steps 2.4, 3.3, 4.3, and 5.1, and addressing the currently failing AppHost-backed authentication test timeout if a clean repository-root test pass is required during this rollout.