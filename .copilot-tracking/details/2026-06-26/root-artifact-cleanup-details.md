---
title: Root Artifact Cleanup Details
description: Step-by-step implementation details for removing erroneous root-level log and text artifacts and preventing recurrence.
author: GitHub Copilot
ms.date: 2026-06-26
ms.topic: how-to
keywords:
  - root artifacts
  - cleanup
  - apphost logs
  - repository hygiene
estimated_reading_time: 6
---
<!-- markdownlint-disable-file -->

## Context Reference

Sources: .copilot-tracking/research/2026-06-26/root-log-files-origin-research.md, .copilot-tracking/research/subagents/2026-06-26/root-log-origin-research.md, docs/wiki/local-development.md, src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json, .gitignore

## Implementation Phase 1: Clean root artifacts

<!-- parallelizable: false -->

### Step 1.1: Confirm and classify root-level capture artifacts

Inspect the repository root and classify the current artifact files into deletion candidates versus legitimate tracked assets. The initial candidate set is the current root files that match the observed local capture pattern: `apphost-dashboard-err.log`, `apphost-dashboard-out.log`, `apphost.err.log`, `apphost.out.log`, `artifacts-api-synth-nameclaim.txt`, `artifacts-apphost-err.log`, `artifacts-apphost-err.txt`, `artifacts-apphost-out.log`, `artifacts-apphost-out.txt`, `artifacts-functional-out.txt`, `dashboard-cookies.txt`, and `dashboard.html`.

Files:
* . - verify current root artifact set before deletion
* .copilot-tracking/research/2026-06-26/root-log-files-origin-research.md - source evidence for classification rules

Discrepancy references:
* DR-01

Success criteria:
* The cleanup candidate list is explicit and limited to root-level local capture artifacts
* No source, documentation, or generated build directories are included in the deletion set

Context references:
* .copilot-tracking/research/2026-06-26/root-log-files-origin-research.md (Lines 30-48) - artifact origin and classification evidence
* .copilot-tracking/research/subagents/2026-06-26/root-log-origin-research.md (Lines 19-33) - supporting evidence that files are redirected console output and browser captures

Dependencies:
* Research findings accepted as the basis for classification

### Step 1.2: Remove the erroneous root files and verify the root is clean

Delete only the classified local capture files from the repository root, then verify that the root no longer contains those patterns. Preserve the ignored `artifacts/` directory itself because it is a legitimate workspace location distinct from the erroneous `artifacts-*.txt` and `artifacts-*.log` files.

Files:
* . - remove only the classified root artifact files

Success criteria:
* The root no longer contains the classified `apphost*.log`, `artifacts-*.txt`, `artifacts-*.log`, `dashboard-cookies.txt`, or `dashboard.html` files
* The `artifacts/` directory and all legitimate project folders remain untouched

Context references:
* .gitignore (Lines 49-51) - confirms `artifacts/` is a distinct ignored directory rather than a root filename pattern
* .copilot-tracking/research/subagents/2026-06-26/root-log-origin-research.md (Lines 36-44) - distinguishes root artifacts from intended repository structure

Dependencies:
* Step 1.1 completion

### Step 1.3: Validate root cleanup

Run a root directory listing and, if needed, a targeted file search to confirm that the cleanup patterns no longer exist at the repository root.

Validation commands:
* PowerShell root listing scoped to the repository root - confirm deletion results
* File search for `apphost*.log`, `artifacts-*.txt`, `artifacts-*.log`, `dashboard-cookies.txt`, and `dashboard.html` - confirm no remaining root matches

## Implementation Phase 2: Add repository guardrails

<!-- parallelizable: true -->

### Step 2.1: Tighten ignore rules for the known root artifact name patterns

Update `.gitignore` to add explicit root-level patterns for the observed artifact names. This does not stop creation, but it prevents accidental staging if a local workflow regresses before the underlying redirect source is corrected.

Files:
* .gitignore - add explicit root-level ignore entries for `apphost*.log`, `apphost-dashboard-*.log`, `artifacts-*.txt`, `artifacts-*.log`, `dashboard-cookies.txt`, and `dashboard.html`

Discrepancy references:
* DD-01

Success criteria:
* `.gitignore` explicitly protects against staging the observed root artifact patterns
* New ignore rules do not shadow legitimate tracked files under `docs/`, `src/`, `test/`, or `artifacts/`

Context references:
* .gitignore (Lines 49-51) - existing ignored `artifacts/` directory rule
* .gitignore (Lines 107-108) - current generic `*.log` protection
* .copilot-tracking/research/2026-06-26/root-log-files-origin-research.md (Lines 93-109) - rationale for defense-in-depth ignore rules

Dependencies:
* Phase 1 cleanup complete

### Step 2.2: Update the local development guide with a no-root-capture rule

Amend the local development documentation to state that AppHost should be run without redirecting stdout or stderr to repository-root files. Document the approved alternatives for captured output: the terminal, an ignored subdirectory such as `artifacts/local/`, or a temp folder outside the repository root.

Files:
* docs/wiki/local-development.md - add guidance near the AppHost run command and troubleshooting sections

Discrepancy references:
* DD-02

Success criteria:
* The local development guide states that root-level output capture files are not part of the supported workflow
* The guide gives at least one approved alternate location for optional captured logs

Context references:
* docs/wiki/local-development.md (Lines 35-42) - current documented AppHost run command
* .copilot-tracking/research/subagents/2026-06-26/root-log-origin-research.md (Lines 57-66) - prevention options grounded in research

Dependencies:
* Phase 1 cleanup complete

### Step 2.3: Validate repository guardrails

Validate the `.gitignore` and documentation changes with markdown diagnostics and a targeted repository search for the new artifact patterns.

Validation commands:
* Markdown diagnostics for docs/wiki/local-development.md
* Search for explicit root artifact patterns to confirm they are covered by documentation and ignore rules

## Implementation Phase 3: Correct the external workflow that creates the files

<!-- parallelizable: true -->

### Step 3.1: Inspect local non-repository run surfaces for output redirection

Check the likely user-local sources identified by research: PowerShell history, the PowerShell profile, user-level VS Code tasks or launch configs, shell aliases/functions, and any one-off scripts used to capture AppHost or dashboard output.

Files:
* User-local PowerShell history or profile - inspect for `>`, `2>`, `*>`, `Out-File`, `Tee-Object`, or `Start-Transcript`
* User-local VS Code task and launch settings - inspect for AppHost run commands with output capture

Discrepancy references:
* DR-01

Success criteria:
* The concrete creator of the root files is identified or narrowed to a single local surface
* The investigation is limited to the specific redirection mechanisms identified in research

Context references:
* .copilot-tracking/research/2026-06-26/root-log-files-origin-research.md (Lines 73-91) - likely source classes and search targets
* .copilot-tracking/research/subagents/2026-06-26/root-log-origin-research.md (Lines 68-84) - unresolved ambiguity and next-research guidance

Dependencies:
* Phase 1 cleanup complete

### Step 3.2: Remove or relocate the redirection target away from the repository root

Once the local creator is found, either remove output redirection entirely or change the target location to an ignored folder such as `artifacts/local/` or a temp path outside the repository. If the source is a user-level task or shell wrapper, keep the run command behavior aligned with the repository documentation.

Files:
* User-local task, profile, alias, or helper script - update to remove root-targeted capture

Success criteria:
* Re-running the local AppHost workflow no longer creates root-level capture files
* Optional captured logs, if still required, land only in the approved ignored location

Context references:
* docs/wiki/local-development.md (Lines 35-42) - canonical direct run command
* .copilot-tracking/research/2026-06-26/root-log-files-origin-research.md (Lines 73-109) - preferred prevention hierarchy

Dependencies:
* Step 3.1 completion

### Step 3.3: Validate external workflow correction

Re-run the user’s normal AppHost startup workflow and confirm that no new root-level `.log` or `.txt` capture artifacts appear.

Validation commands:
* Re-run the actual local AppHost startup workflow that previously created the files
* Root directory listing before and after startup - confirm no recurrence

## Implementation Phase 4: Final validation

<!-- parallelizable: false -->

### Step 4.1: Run full validation for the cleanup slice

Execute all validation needed for the affected scope:
* Root directory listing
* Markdown diagnostics for docs/wiki/local-development.md
* Git status review for removed root artifacts and updated guardrail files

### Step 4.2: Fix minor validation issues

Resolve minor markdown, ignore-rule, or cleanup oversights that surface during validation. Keep fixes constrained to the cleanup task.

### Step 4.3: Report blocking issues

If the files still reappear after repository guardrail updates, report the remaining blocker as a user-local workflow source that must be changed outside the repository. Do not expand the repository changes beyond documentation and ignore rules without new evidence.

## Dependencies

* Repository write access for `.gitignore` and `docs/wiki/local-development.md`
* Access to user-local shell or editor configuration if the redirection source is outside the repository
* PowerShell or equivalent shell access for cleanup verification

## Success Criteria

* The current erroneous root-level capture artifacts are removed
* The repository documents the supported no-root-capture AppHost workflow
* Ignore rules explicitly guard against accidental staging of the observed root artifact names
* The actual local creator of the files is changed so the files do not reappear after AppHost startup
