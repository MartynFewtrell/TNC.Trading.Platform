---
applyTo: '.copilot-tracking/changes/2026-08-04/option-b-shared-contract-boundary-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Option B Shared Contract Boundary

## Overview

Replace the temporary Web-to-Application authentication dependency with a deliberate, framework-neutral shared contract assembly while preserving host-owned authentication behavior and tests.

## Objectives

### User Requirements

* Create a plan for Option B, adding a deliberate shared contract boundary. Source: user request.

### Derived Objectives

* Remove the project-level Web-to-Application exception rather than only reorganizing Web model folders. Derived from .copilot-tracking/research/2026-08-04/shared-contract-boundary-research.md.
* Keep authentication registration, endpoint routing, token handling, audit clients, and provider-specific policy in host or adapter projects. Derived from .github/instructions/architecture.instructions.md and the research findings.
* Make resolver ownership and nested provider option scope explicit before extraction. Derived from the mixed responsibilities found in PlatformAuthenticationConfigurationResolver and PlatformAuthenticationOptions.
* Protect the new dependency direction with executable architecture tests and updated wiki guidance. Derived from docs/wiki/architecture.md and the current architecture test exception.

## Context Summary

### Project Files

* src/TNC.Trading.Platform.Application/Authentication/ - current owner of shared authentication defaults, options, and resolver.
* src/TNC.Trading.Platform.Api/Authentication/ and src/TNC.Trading.Platform.Web/Authentication/ - host-specific authentication composition that consumes the shared surface.
* src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj - current temporary Application project reference.
* test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ - current project-role policy, validator, and transitional exception tests.
* docs/wiki/architecture.md and docs/wiki/application-overview.md - documentation of the current exception and its retirement intent.

### References

* .copilot-tracking/research/2026-08-04/shared-contract-boundary-research.md - verified codebase findings, alternatives, recommended extraction, and validation commands.
* docs/wiki/testing-and-quality.md - repository validation conventions.
* global.json - pinned .NET SDK selection.

### Standards References

* .github/copilot-instructions.md - Clean Architecture, C# file ownership, authentication, documentation, and validation requirements.
* .github/instructions/architecture.instructions.md - dependency direction and contract ownership.
* .github/instructions/configuration.instructions.md - protected configuration and secret-handling boundaries.
* .github/instructions/folders.instructions.md - placement of new production and test projects.

## Implementation Checklist

### [ ] Implementation Phase 1: Contract Inventory and Project Design

<!-- parallelizable: false -->

* [ ] Step 1.1: Classify every member in the current defaults, options, and resolver types.
  * Details: .copilot-tracking/details/2026-08-04/option-b-shared-contract-boundary-details.md (Lines 12-34)
* [ ] Step 1.2: Create and register the narrowly named authentication contract project.
  * Details: .copilot-tracking/details/2026-08-04/option-b-shared-contract-boundary-details.md (Lines 36-56)

### [ ] Implementation Phase 2: Extract Stable Contracts and Rewire Consumers

<!-- parallelizable: false -->

* [ ] Step 2.1: Extract stable authentication vocabulary and configuration data, with staged resolver ownership.
  * Details: .copilot-tracking/details/2026-08-04/option-b-shared-contract-boundary-details.md (Lines 62-85)
* [ ] Step 2.2: Rewire API, Web, and focused tests without moving Web ViewModels.
  * Details: .copilot-tracking/details/2026-08-04/option-b-shared-contract-boundary-details.md (Lines 87-110)
* [ ] Step 2.3: Run focused contract, API, Web, and authentication tests.
  * Details: .copilot-tracking/details/2026-08-04/option-b-shared-contract-boundary-details.md (Lines 112-128)

### [ ] Implementation Phase 3: Retire the Temporary Architecture Exception

<!-- parallelizable: false -->

* [ ] Step 3.1: Replace the Web-to-Application exception with an explicit contract-project rule and update wiki documentation.
  * Details: .copilot-tracking/details/2026-08-04/option-b-shared-contract-boundary-details.md (Lines 134-159)

### [ ] Implementation Phase 4: Final Validation and Handoff

<!-- parallelizable: false -->

* [ ] Step 4.1: Run full solution build, tests, and architecture validation; record blockers requiring additional planning.
  * Details: .copilot-tracking/details/2026-08-04/option-b-shared-contract-boundary-details.md (Lines 165-180)

## Planning Log

See .copilot-tracking/plans/logs/2026-08-04/option-b-shared-contract-boundary-log.md for discrepancy tracking, implementation paths, and suggested follow-on work.

## Dependencies

* .NET SDK selected by global.json
* Existing restore assets or a permitted restore operation
* API, Web, Application, and architecture test projects
* An explicit ownership decision for nested provider options and resolver behavior

## Success Criteria

* A named, framework-neutral authentication contract assembly is consumed by both API and Web.
* The temporary Web-to-Application project dependency is removed and rejected by architecture tests.
* Local Keycloak and Azure Entra ID authentication vocabulary, configuration semantics, and host behavior remain compatible.
* Wiki documentation, focused tests, and full validation commands reflect the deliberate boundary.
* The unrelated Web ViewModel folder move remains out of scope and is recorded as follow-on work.

## Planning Log Reference

See .copilot-tracking/plans/logs/2026-08-04/option-b-shared-contract-boundary-log.md.
