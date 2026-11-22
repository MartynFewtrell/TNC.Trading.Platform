<!-- markdownlint-disable-file -->
---
applyTo: '.copilot-tracking/changes/2026-08-04/web-project-folder-structure-changes.md'
---
# Implementation Plan: Web Project Folder Structure

## Overview

Reorganize the Web project incrementally around existing feature ownership boundaries while preserving startup, authentication, routes, static assets, and Web-to-API contracts.

## Objectives

### User Requirements

* Deliver the staged tidy-up recommendations from the Web project folder-structure research. Source: `.copilot-tracking/research/2026-08-04/web-project-folder-structure-research.md`.
* Reduce loose files under `src/TNC.Trading.Platform.Web` without changing runtime behavior. Source: User request and supplied research, 2026-08-04.
* Validate each migration stage and the final solution. Source: Supplied research validation plan.

### Derived Objectives

* Classify root view models by consumer before moving them. Derived from transport usage in `PlatformApiClient` and page-local Configuration form ownership.
* Separate physical folder moves from namespace changes. Derived from Razor imports, service registration, and bUnit dependencies.
* Preserve all route templates, router assembly discovery, root-relative navigation, and static asset paths. Derived from `Routes.razor`, `App.razor`, layout navigation, and `wwwroot` contracts.
* Use existing Unit, Aspire-backed Functional, and Playwright E2E suites rather than merging test boundaries. Derived from repository test structure and delegated research.

## Context Summary

The Web project already has stable `Authentication`, `Components/Layout`, `Components/Authorization`, `Components/App.razor`, `Components/Routes.razor`, and `wwwroot` boundaries. The cleanup should target mixed root C# files and page ownership, not relocate every root file. The selected path begins with namespace-preserving physical moves, starting with the clearly page-local Configuration slice, then moves remaining page slices one at a time. Shared transport contracts are retained or moved only after a consumer matrix establishes a deliberate owner.

### Affected Files Tree

```text
src/TNC.Trading.Platform.Web/
  Program.cs                         # preserve host startup
  TNC.Trading.Platform.Web.csproj   # preserve recursive SDK discovery
  Authentication/                    # preserve auth boundary
  Components/
    App.razor                        # preserve document and asset shell
    Routes.razor                     # preserve route discovery
    _Imports.razor                   # update only for intentional namespaces
    Layout/                          # preserve shell boundary
    Authorization/                   # preserve authorization boundary
    Pages/
      Configuration/                 # create first feature slice
      Home/                          # create incrementally
      Status/                        # create incrementally
      IgLogin/                       # create incrementally
      Administration/                # create incrementally
  wwwroot/                           # preserve static assets

test/TNC.Trading.Platform.Web/
  TNC.Trading.Platform.Web.UnitTests/       # update focused imports/setup
  TNC.Trading.Platform.Web.FunctionalTests/ # run distributed regression suite
  TNC.Trading.Platform.Web.E2ETests/        # run Playwright regression suite
```

Research sources:

* `.copilot-tracking/research/2026-08-04/web-project-folder-structure-research.md`
* `.copilot-tracking/research/subagents/2026-08-04/web-project-folder-structure-plan-research.md`
* `.github/copilot-instructions.md`
* `.github/instructions/folders.instructions.md`
* `.github/instructions/architecture.instructions.md`
* `.github/instructions/csharp.instructions.md`
* `.github/instructions/tests.instructions.md`

## Implementation Checklist

### [x] Implementation Phase 1: Baseline and ownership classification

<!-- parallelizable: false -->

* [x] Step 1.1: Capture working-tree state and establish Web build and focused unit-test baselines.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 12-32)
* [x] Step 1.2: Create a consumer matrix for every root view model and update request; reject a generic `Models/` destination.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 34-58)
* [x] Step 1.3: Confirm recursive SDK discovery, namespace/import impacts, registrations, available Web test layers, and the explicit Razor namespace strategy.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 60-78)

### [x] Implementation Phase 2: Namespace-preserving feature organization

<!-- parallelizable: false -->

* [x] Step 2.1: Move the Configuration page, presenter, form model, and mapper into `Components/Pages/Configuration/` using the approved explicit Razor namespace strategy and preserving `/configuration`.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 84-111)
* [x] Step 2.2: Move Home, Status, IG login history, and Administration slices one at a time, keeping route templates and shared contracts stable.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 113-133)
  * [x] Home portion: moved the Home page and presenter into `Components/Pages/Home/`; preserved the existing namespace, explicit Razor namespace strategy, and `/` route.
  * [x] Status portion: moved the Status page and presenter into `Components/Pages/Status/`; preserved the existing namespace, explicit Razor namespace strategy, and `/status` route.
  * [x] IG login history portion: moved the page into `Components/Pages/IgLogin/`; no directly owned presenter or form code exists, and the existing namespace, explicit Razor namespace strategy, and `/ig-login/history` route were preserved.
  * [x] Administration portion: moved the page into `Components/Pages/Administration/`; no directly owned presenter or form code exists, and the existing namespace, explicit Razor namespace strategy, and `/administration/authentication` route were preserved.
* [x] Step 2.3: Apply namespace/import/registration/test changes only for intentional feature-boundary changes.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 135-147)

### [x] Implementation Phase 3: Deliberate shared-boundary decision

<!-- parallelizable: false -->

* [x] Step 3.1: Finalize ownership for shared client/API contracts and move complete one-type-per-file units only when evidence supports it.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 153-176)

### [x] Implementation Phase 4: Route, asset, and distributed validation

<!-- parallelizable: false -->

* [x] Step 4.1: Verify route templates, `Routes.razor`, `App.razor`, root-relative navigation, and unchanged `wwwroot` assets.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 182-198)
* [x] Step 4.2: Run Web Unit, Functional, E2E, solution build, and solution test validation; require route/static-asset coverage evidence and fill any gap with focused smoke coverage.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 200-223)
* [x] Step 4.3: Record environment or baseline blockers without expanding scope.
  * Details: `.copilot-tracking/details/2026-08-04/web-project-folder-structure-details.md` (Lines 225-232)

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-04/web-project-folder-structure-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK selected by `global.json`
* Existing restore state or network access
* Docker and Aspire prerequisites for Functional and E2E tests
* Playwright browser installation for E2E tests
* Consumer classification before any shared-contract move

## Success Criteria

* Page ownership is visible under `Components/Pages/<Feature>/` without a generic `Models/` dumping ground. Traces to the supplied research and DR-01/DR-02.
* `Program.cs`, authentication behavior, route templates, `Routes.razor` assembly discovery, `App.razor` asset references, `wwwroot`, and root-relative navigation remain unchanged in behavior. Traces to the supplied research and Phase 4.
* Every moved type has coherent production and test references, with focused checkpoints after each slice. Traces to namespace/import/test findings.
* Web Unit, Functional, E2E, solution build, and solution test validation pass, or exact baseline/environment blockers are recorded. Traces to the delegated research and Phase 4 details.
