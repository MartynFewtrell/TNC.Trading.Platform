<!-- markdownlint-disable-file -->
# Release Changes: Root Artifact Cleanup

**Related Plan**: root-artifact-cleanup-plan.instructions.md
**Implementation Date**: 2026-06-26

## Summary

Implements cleanup of erroneous root-level AppHost and dashboard capture artifacts, adds repository guardrails, and narrows the remaining recurrence source to an external local workflow outside committed repository control.

## Changes

### Added

* None yet.

### Modified

* .gitignore - Added explicit root-level ignore rules for the observed AppHost, dashboard, and artifacts capture file patterns.
* docs/wiki/local-development.md - Documented the supported no-root-capture AppHost workflow and approved alternate capture locations.

### Removed

* apphost-dashboard-err.log - Removed erroneous root-level AppHost dashboard stderr capture.
* apphost-dashboard-out.log - Removed erroneous root-level AppHost dashboard stdout capture.
* apphost.err.log - Removed erroneous root-level AppHost stderr capture.
* apphost.out.log - Removed erroneous root-level AppHost stdout capture.
* artifacts-api-synth-nameclaim.txt - Removed erroneous root-level API synthesis capture artifact.
* artifacts-apphost-err.log - Removed erroneous root-level AppHost capture artifact.
* artifacts-apphost-err.txt - Removed erroneous root-level AppHost text capture artifact.
* artifacts-apphost-out.log - Removed erroneous root-level AppHost capture artifact.
* artifacts-apphost-out.txt - Removed erroneous root-level AppHost text capture artifact.
* artifacts-functional-out.txt - Removed erroneous root-level functional capture artifact.
* dashboard-cookies.txt - Removed erroneous root-level dashboard cookie capture artifact.
* dashboard.html - Removed erroneous root-level dashboard HTML capture artifact.

## Additional or Deviating Changes

* The exact external command or helper that created the root artifacts was not found in committed workspace files, user-level VS Code tasks, standard PowerShell profiles, current VS Code session logs, or accessible PowerShell history matches.
	* Reason: The remaining source appears to be outside committed repository control and was not reproducible from the accessible local configuration surfaces during this implementation pass.

## Release Summary

Removed 12 erroneous root-level capture artifacts and preserved the legitimate `artifacts/` directory and project folders. Added explicit root-level ignore rules in `.gitignore` for the observed AppHost, dashboard, and `artifacts-*` capture patterns, and updated the local development guide to forbid repository-root output capture while pointing developers to approved alternatives such as `artifacts/local/` or a temporary folder.

The accessible local investigation ruled out committed workspace files, user-level VS Code tasks, standard PowerShell profile locations, current VS Code session logs, and accessible PowerShell history pattern matches as the source of recurrence. The remaining blocker is an external or one-off local workflow that still needs to be identified directly if the artifacts reappear.
