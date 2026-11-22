<!-- markdownlint-disable-file -->
# Release Changes: Architecture Project Placement

**Related Plan**: `.copilot-tracking/plans/2026-08-03/architecture-project-placement-plan.instructions.md`
**Implementation Date**: 2026-08-03

## Summary

Implementation removed the tracked root-level duplicate architecture validator after confirming it was byte-identical to the canonical copy, had no root-path consumers, and that the complete `test/` project and its single solution entry were already present. Focused architecture validation passed with 13 tests.

## Changes

### Added

* Changes log for the architecture project placement implementation.

### Modified

* `.copilot-tracking/plans/2026-08-03/architecture-project-placement-plan.instructions.md` - Marked verification, duplicate-removal, and validation steps complete.

### Removed

* `TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs` - Removed the tracked root-level byte-identical duplicate.

## Additional or Deviating Changes

* None yet.

## Release Summary

Focused architecture tests passed with 13/13 tests. The solution build passed with 109 existing warnings. The complete solution test command reached 361 tests (350 succeeded, 11 failed, 0 skipped) before the API integration test host was canceled and emitted MSB5021; this is recorded as an unrelated distributed-test/environment blocker requiring follow-up.
