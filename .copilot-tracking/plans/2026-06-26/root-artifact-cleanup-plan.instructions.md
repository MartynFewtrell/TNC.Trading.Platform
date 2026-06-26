---
description: "Implementation plan for removing erroneous root-level log and text artifacts and preventing recurrence"
applyTo: '.copilot-tracking/changes/2026-06-26/root-artifact-cleanup-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Root Artifact Cleanup

## Overview

Remove erroneous root-level capture artifacts, add repository guardrails against accidental restaging, and correct the external local workflow that currently recreates those files.

## Objectives

### User Requirements

* Create a plan to clean up erroneous root-level `.log` and `.txt` files of the observed type. Source: user request on 2026-06-26.
* Update whatever is necessary to prevent those files from reappearing. Source: user request on 2026-06-26.

### Derived Objectives

* Separate repository cleanup from the actual external redirection source so the implementation fixes both symptoms and cause. Derived from: .copilot-tracking/research/2026-06-26/root-log-files-origin-research.md (Lines 112-126).
* Add defense-in-depth repository guardrails even though ignore rules alone do not stop creation. Derived from: .copilot-tracking/research/subagents/2026-06-26/root-log-origin-research.md (Lines 59-67).
* Keep the cleanup narrowly scoped to erroneous root artifacts and avoid touching the legitimate `artifacts/` directory or project content. Derived from: repository root inventory and research classification rules.

## Context Summary

### Project Files

* .gitignore - already ignores generic `*.log` files and the `artifacts/` directory, but does not explicitly protect the current root artifact name patterns.
* docs/wiki/local-development.md - documents the supported AppHost run command without any output redirection.
* src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json - confirms AppHost launch settings do not define file logging or root output capture.

### References

* .copilot-tracking/research/2026-06-26/root-log-files-origin-research.md - primary repository research for artifact origin and prevention strategy.
* .copilot-tracking/research/subagents/2026-06-26/root-log-origin-research.md - supporting evidence for local redirection origin and defense-in-depth options.
* .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md - implementation step details with validation scope.

### Standards References

* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\markdown.instructions.md - markdown authoring requirements.
* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\writing-style.instructions.md - writing style requirements.
* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\prompt-builder.instructions.md - requirements for `.instructions.md` plan artifacts.

## Implementation Checklist

### [x] Implementation Phase 1: Clean root artifacts

<!-- parallelizable: false -->

* [x] Step 1.1: Confirm and classify the current root artifact files
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 24-44)
* [x] Step 1.2: Remove the erroneous root files without touching legitimate directories
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 46-62)
* [x] Step 1.3: Validate the root cleanup result
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 64-70)

### [x] Implementation Phase 2: Add repository guardrails

<!-- parallelizable: true -->

* [x] Step 2.1: Tighten root-specific ignore rules for observed artifact names
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 76-96)
* [x] Step 2.2: Document the no-root-capture rule in the local development guide
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 98-117)
* [x] Step 2.3: Validate ignore-rule and documentation guardrails
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 119-125)

### [ ] Implementation Phase 3: Correct the external workflow source

<!-- parallelizable: true -->

* [ ] Step 3.1: Inspect local run surfaces for redirection or transcript capture
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 131-151)
* [ ] Step 3.2: Remove or relocate the root-targeted capture path
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 153-169)
* [ ] Step 3.3: Re-run the actual local workflow and confirm no recurrence
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 171-177)

### [x] Implementation Phase 4: Validation

<!-- parallelizable: false -->

* [x] Step 4.1: Run full validation for the cleanup slice
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 183-188)
* [x] Step 4.2: Fix minor validation issues discovered during the cleanup slice
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 190-192)
* [x] Step 4.3: Report blocking issues that remain outside repository control
  * Details: .copilot-tracking/details/2026-06-26/root-artifact-cleanup-details.md (Lines 194-196)

## Planning Log

See .copilot-tracking/plans/logs/2026-06-26/root-artifact-cleanup-log.md for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* Repository write access for .gitignore and docs/wiki/local-development.md
* Access to user-local shell history, profile, or editor configuration if the redirection source lives outside the repository
* PowerShell or equivalent shell access for cleanup verification

## Success Criteria

* The current erroneous root-level capture artifacts are removed. Traces to: user cleanup requirement.
* The supported local run workflow explicitly forbids root-level capture files and offers approved alternatives. Traces to: prevention requirement and docs/wiki/local-development.md:35-40.
* Root artifact names are explicitly ignored to reduce restaging risk if a local workflow regresses. Traces to: defense-in-depth recommendation in .copilot-tracking/research/subagents/2026-06-26/root-log-origin-research.md:67.
* The local creator of the files is corrected so an AppHost start no longer recreates root artifacts. Traces to: .copilot-tracking/research/2026-06-26/root-log-files-origin-research.md:114-126.
