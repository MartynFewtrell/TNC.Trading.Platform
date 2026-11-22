<!-- markdownlint-disable-file -->
# Release Changes: Portable Clean Architecture Guidance

**Related Plan**: clean-architecture-guidance-update-plan.instructions.md
**Implementation Date**: 2026-07-26

## Summary

The implementation extends the existing architecture guidance with portable Clean Architecture principles and adds topology-neutral project graph integrity checks without freezing the transitional repository topology.

## Changes

### Added

* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/` - Added dependency-light graph integrity integration tests, generic graph model/loader/validator types, and repository/in-memory scenarios.
* None beyond the architecture test suite.

### Modified

* `.github/instructions/architecture.instructions.md` - Expanded activation to production and test C# project files and added portable Clean Architecture responsibility, dependency direction, boundary translation, composition, testability, examples, references, and validation-limit guidance.
* `TNC.Trading.Platform.slnx` - Registered the architecture integration-test project and removed a duplicate solution-folder declaration found during validation.
* `docs/wiki/architecture.md` - Documented portable principles and labeled the current project topology as transitional and non-normative.
* `docs/wiki/testing-and-quality.md` - Documented the topology-neutral graph suite, focused command, accepted topology changes, and enforcement limits.
* `.github/lessons.md` - Added the invariant-over-project-layout Clean Architecture lesson.

### Removed

* None

## Additional or Deviating Changes

* The installed customization evaluation extension did not expose a callable activation evaluator, so static frontmatter, representative path matching, portability scans, Markdown structure checks, and editor diagnostics were used.
* The graph suite required two local corrections during focused validation: a duplicate diagnostic declaration was removed and synthetic graph references were normalized consistently.
* Full solution tests remain partially blocked by an unrelated distributed AppHost/Keycloak runtime failure: 239 tests passed and 1 failed after listener/authentication startup timeouts. The failure is recorded for follow-up rather than changing this architecture slice.

## Release Summary

The implementation added portable Clean Architecture guidance to the existing Vertical Slice and CQRS instruction, expanded activation to production and test C# project files, added a dependency-light topology-neutral project graph integrity suite, registered it in the solution, and aligned the architecture/testing wiki pages and repository lessons. The graph suite validates missing project-reference targets and cycles without encoding project names, project counts, layer roles, or allowed edges. Project-level dependency-direction enforcement remains deferred until refactoring establishes stable repository boundaries.

Focused architecture validation passed with 7/7 tests, and the solution build passed with existing warnings. The full solution test run produced 239 passed tests and 1 failed distributed authentication/runtime test due to AppHost listener discovery and Keycloak-backed Web startup timeouts. Follow-up is tracked as WI-06 in the planning log.
