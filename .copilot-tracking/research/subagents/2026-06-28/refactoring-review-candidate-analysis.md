---
title: Refactoring Review Candidate Analysis
description: Repository research to identify the most plausible completed refactoring task whose final review indicates additional work is required
ms.date: 2026-06-28
ms.topic: reference
---

## Research scope

* Identify the most plausible completed refactoring-related task under `docs/` whose final review indicates more work is still required.
* Capture evidence from review artifacts, adjacent specifications, and targeted code or tests where useful.

## Candidate ranking

### Most plausible primary candidate

`docs/005-refactor-app-host/002-project-test-review-report.md`

This is the strongest match for a completed refactoring task whose later review still says more work is required.

Why it is the best match:

* It sits inside an explicitly refactoring-focused workstream, `005-refactor-app-host`, whose requirements and technical specification define a completed structural refactor of AppHost plus follow-on real-runtime test migration expectations.
* The review is a later-stage artifact that explicitly identifies unresolved gaps after the AppHost refactor rather than before it. The top concerns are outstanding test-evidence and maintainability gaps, not missing initial implementation.
* A later evidence note confirms that some of those findings were addressed, while also preserving a smaller set of still-open follow-up items. That pattern closely matches a statement that a final review found more work was still needed.

Primary evidence:

* The review names unresolved findings `F1` to `F6`, including missing direct AppHost coverage, repeated high-cost startup, brittle reflection-heavy API unit tests, stale Web coverage documentation, and narrow provider-branch coverage: docs/005-refactor-app-host/002-project-test-review-report.md:42, docs/005-refactor-app-host/002-project-test-review-report.md:43, docs/005-refactor-app-host/002-project-test-review-report.md:44, docs/005-refactor-app-host/002-project-test-review-report.md:137, docs/005-refactor-app-host/002-project-test-review-report.md:142, docs/005-refactor-app-host/002-project-test-review-report.md:143, docs/005-refactor-app-host/002-project-test-review-report.md:144
* The same review recommends specific next work, including adding AppHost tests, reducing reflection, reusing distributed fixtures, and reconciling docs: docs/005-refactor-app-host/002-project-test-review-report.md:182, docs/005-refactor-app-host/002-project-test-review-report.md:183, docs/005-refactor-app-host/002-project-test-review-report.md:184, docs/005-refactor-app-host/002-project-test-review-report.md:185, docs/005-refactor-app-host/002-project-test-review-report.md:186, docs/005-refactor-app-host/002-project-test-review-report.md:219, docs/005-refactor-app-host/002-project-test-review-report.md:220, docs/005-refactor-app-host/002-project-test-review-report.md:221
* The workstream itself expected synthetic AppHost runtime removal and real-runtime test migration, so the review findings are directly comparable to the intended outcome: docs/005-refactor-app-host/requirements.md:41, docs/005-refactor-app-host/requirements.md:58, docs/005-refactor-app-host/requirements.md:77, docs/005-refactor-app-host/technical-specification.md:111, docs/005-refactor-app-host/technical-specification.md:112, docs/005-refactor-app-host/technical-specification.md:130
* A completed mitigation plan and a later quality-evidence note show the review was acted on, but not everything was fully closed. The evidence note says API unit coverage remains the weakest area, Web mutation evidence is still blocked, and future hardening value remains highest in Application orchestration and low-signal API unit areas: docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:9, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:25, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:45, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:56, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:58

### Secondary candidate

`docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md`

This is the most plausible runner-up, but it appears to be an earlier refactoring review whose major findings were subsequently incorporated into a completed mitigation plan.

Why it is less likely than `005`:

* It is clearly a refactoring review and it does say further work is needed.
* However, the adjacent mitigation artifact is marked completed and explicitly closes the three grouped work items that addressed the review findings.
* Later `005` artifacts are better evidence of a completed task that still had unresolved follow-up after review and mitigation.

Evidence:

* The refactoring review flags high-urgency issues around dual local runtime paths, in-memory persistence fallback, test-only auth in product code, duplicated auth registration logic, AppHost responsibility overload, outdated docs, and a test safety net centered on synthetic runtime paths: docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:35, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:63, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:64, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:65, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:69, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:70, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:74, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:75, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:79, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:80
* Those findings directly conflicted with the workstream’s intended architecture, which already specified Keycloak in AppHost as the local development baseline: docs/003-authentication-and-authorisation/technical-specification.md:21, docs/003-authentication-and-authorisation/technical-specification.md:26, docs/003-authentication-and-authorisation/technical-specification.md:38
* The follow-on mitigation plan is explicitly completed and marks all three mitigation work items as done: docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:78, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:79, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:80, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:84, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:112, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:138

## Exact unresolved issues by plausible candidate

### Candidate 1: `005-refactor-app-host/002-project-test-review-report.md`

Live unresolved issues that still matter after later mitigation evidence:

* API unit coverage remains weak even after improvement. The later evidence note states it is still the weakest lower-level coverage area and still needs focused growth: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:25
* Web mutation evidence remains blocked. Stryker still fails when mutating `src/TNC.Trading.Platform.Web/Program.cs`, so there is no final mutation-confidence signal for the Web unit suite: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:45
* The highest-value remaining hardening work is no longer AppHost coverage or Web component coverage, but Application orchestration and low-signal API unit areas: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:56
* Deferred follow-up is explicit rather than implied: keep the Web Stryker failure visible, use Application and Infrastructure survived-mutant hotspots when related work is already in scope, and continue expanding cheap API unit coverage before adding more high-cost distributed tests: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:58

Findings from the baseline review that appear outdated because later repository evidence contradicts them:

* `F1` missing focused AppHost tests is outdated. AppHost unit tests now exist, including `AppHostSettingsTests`, and the evidence note reports `94.69%` line coverage and `100.00%` branch coverage for the new AppHost unit suite: docs/005-refactor-app-host/002-project-test-review-report.md:42, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:13, test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostSettingsTests.cs:16
* `F4` missing evidence for Web component coverage is outdated. The repository now contains `MainLayoutTests`, `HomeTests`, `StatusTests`, and `ConfigurationTests`, and the mitigation plan explicitly reviewed those files as part of the reconciliation work: docs/005-refactor-app-host/002-project-test-review-report.md:143, docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:54, docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:179, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs:8, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/HomeTests.cs:8, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StatusTests.cs:8, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:8
* `F6` narrow provider-parity coverage is partly outdated. The mitigation plan lists direct AppHost provider-branch tests as completed, and `AppHostSettingsTests` exists as evidence of the new low-cost AppHost coverage layer: docs/005-refactor-app-host/002-project-test-review-report.md:144, docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:73, test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostSettingsTests.cs:16

Remaining ambiguity:

* The mitigation plan file still shows unchecked top-level bullets for Work Items 1 to 4 even though all nested tasks are checked and the plan status is `completed`: docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:9, docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:81, docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:111, docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:142, docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:176, docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md:207
* That inconsistency looks editorial rather than substantive because the evidence note clearly records delivered follow-up coverage and metrics. It is still worth treating as documentation drift if this artifact is used for follow-up planning.

### Candidate 2: `003-authentication-and-authorisation/001-work-package-refactoring-review-report.md`

Unresolved issues recorded in the review itself:

* Dual runtime topology in AppHost, with a non-container `Test` provider path conflicting with the Keycloak-based local-dev baseline: docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:63
* Hidden development-only in-memory persistence fallback in infrastructure startup: docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:64
* Test-only sign-in behavior embedded in product Web endpoint registration: docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:65
* Duplicated auth policy and provider-resolution logic between Web and API: docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:69, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:70
* AppHost responsibility overload and outdated local-development documentation: docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:74, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:75
* Test safety net biased toward synthetic runtime paths: docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:79, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:80

Why these findings are probably not the best current primary source:

* The follow-on mitigation plan is completed and maps all major finding groups to finished work items: docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:78, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:79, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:80, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:84, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:112, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:138
* Later `005` artifacts provide fresher, narrower evidence of what still remains after major refactoring and test-hardening work.

## Intended outcome versus review mismatch

### `005-refactor-app-host`

Intended outcome in the workstream definition:

* Remove synthetic or in-memory substitute runtime behavior from AppHost-backed distributed tests: docs/005-refactor-app-host/requirements.md:41, docs/005-refactor-app-host/requirements.md:58
* Ensure distributed validation exercises real runtime protections through the real Aspire-managed topology: docs/005-refactor-app-host/requirements.md:77
* Remove the synthetic AppHost runtime branch and migrate AppHost-backed integration, functional, and E2E tests to real runtime validation: docs/005-refactor-app-host/technical-specification.md:111, docs/005-refactor-app-host/technical-specification.md:112, docs/005-refactor-app-host/technical-specification.md:130

Mismatch found by the baseline review:

* The refactor completed structurally, but proof lagged behind the intended outcome. The review found missing direct AppHost coverage, expensive repeated startup, brittle API unit seams, and stale or mismatched Web coverage documentation: docs/005-refactor-app-host/002-project-test-review-report.md:42, docs/005-refactor-app-host/002-project-test-review-report.md:43, docs/005-refactor-app-host/002-project-test-review-report.md:44, docs/005-refactor-app-host/002-project-test-review-report.md:137, docs/005-refactor-app-host/002-project-test-review-report.md:142, docs/005-refactor-app-host/002-project-test-review-report.md:143, docs/005-refactor-app-host/002-project-test-review-report.md:144

Current state after later evidence:

* The mismatch narrowed materially. AppHost direct coverage and Web component coverage were added or reconciled, but the workstream still lacks a clean final state for Web mutation testing and still has weak API unit-signal relative to the surrounding suites: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:13, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:25, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:45

### `003-authentication-and-authorisation`

Intended outcome in the specification:

* Local development authentication should already have been Keycloak-based and AppHost-orchestrated: docs/003-authentication-and-authorisation/technical-specification.md:21
* The local environment should use a repeatable Keycloak realm import and seeded local identities: docs/003-authentication-and-authorisation/technical-specification.md:26, docs/003-authentication-and-authorisation/technical-specification.md:38

Mismatch found by the review:

* Product startup and docs still reflected an alternate lightweight mode plus synthetic branches, meaning the delivered implementation had drifted from its own stated architecture: docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:63, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:64, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:65, docs/003-authentication-and-authorisation/001-work-package-refactoring-review-report.md:75

Current state:

* The adjacent mitigation plan claims those mismatches were handled through completed work items, so this workstream is less useful as the primary current planning source unless later code-level validation reopens those concerns: docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:84, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:112, docs/003-authentication-and-authorisation/plans/004-work-package-refactoring-mitigation-plan.md:138

## Targeted code and test reads that affect artifact confidence

The targeted repository reads support treating `005` as the fresher primary follow-up source:

* AppHost-focused unit tests now exist, which directly weakens the baseline review’s `F1` claim: test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostSettingsTests.cs:16
* Web component test classes now exist for the exact pages the baseline review said were not evidenced: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs:8, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/HomeTests.cs:8, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StatusTests.cs:8, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:8
* Those tests are not stubs. They contain real assertions around signed-out shell state, role-aware rendering, degraded auth warnings, retry-button behavior, accordion defaults, and preservation of failed-save edits: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs:15, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/HomeTests.cs:37, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StatusTests.cs:45, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:37

This means the baseline `005` review is not sufficient on its own. It must be read together with the later quality-evidence note to avoid planning against stale findings.

## Recommended primary source for follow-up planning

Treat `docs/005-refactor-app-host/002-project-test-review-report.md` as the primary review artifact, but only in combination with `docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md` as the freshness check.

Why this pair is the best planning source:

* It is the strongest match for a completed refactoring task followed by review-driven additional work.
* It captures both the original unresolved findings and the later evidence showing which findings are already closed versus still live.
* The remaining live issues are narrower and better prioritized than the older `003` review: low-signal API unit coverage, blocked Web mutation testing, and future hardening centered on Application orchestration and specific survived-mutant hotspots.

If a single artifact must be chosen, use `docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md` as the operational starting point and treat `002-project-test-review-report.md` as its baseline context.

## Remaining research gaps

* No later code/test review artifact was found after `docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md`, so there is no newer repository document confirming whether the Web Stryker blocker or low API unit signal has since been addressed.
* I did not run tests or coverage tools in this research-only pass, so all status claims are based on repository artifacts rather than current execution.
* I did not read every changed test file in the `005` mitigation work. The targeted reads were enough to confirm that key baseline findings `F1` and `F4` are outdated, but not enough to independently verify every mitigation claim in the plan.
