<!-- markdownlint-disable-file -->
---
title: Testing Improvement Plan Research
description: Planning synthesis for the test reliability remediation work.
author: GitHub Copilot
ms.date: 2026-09-15
ms.topic: troubleshooting
keywords:
  - testing
  - aspire
  - bunit
  - sql-server
---

## Scope

Create a delivery plan that resolves the current 87 test failures, preserves useful test-layer coverage, and updates durable guidance so future tests follow fixture-owned resource and endpoint contracts.

## Evidence Summary

* `.copilot-tracking/research/2026-09-15/failing-tests-research.md` attributes all current failures to test-only package incompatibility or fixture infrastructure, not a product assertion failure.
* `.copilot-tracking/research/subagents/2026-09-15/testing-improvement-plan-scope.md` verifies the exact current implementation surfaces, shared-helper consumers, and affected documentation.
* The immediate independent repair removes the direct `AngleSharp` 1.8.1 override from the Web unit project while retaining `bunit.web` 1.40.0.
* API real-auth, Web functional, and Web E2E use shared process launching, listener scanning, fixed port leasing, and process-wide environment overrides. Migrate them as one resource-oriented Aspire fixture slice, then retire those helpers only after every consumer moves.
* `ControllableIgProvider` releases a reserved TCP port before binding its HTTP listener. Replace it with an endpoint-owning test server that publishes its URI after binding, and inject that URI through builder-scoped test configuration.
* Infrastructure SQL integration must remain service-local. Replace arbitrary Docker image discovery with a fixture-owned, readiness-checked SQL Server lifecycle and isolated database/reset boundary. The selected plan uses a dedicated test container rather than launching the production AppHost.
* Durable updates must revise `.github/instructions/aspire-tests.instructions.md`, `.github/instructions/tests.instructions.md`, `.github/AGENTS.md`, `.github/lessons.md`, `docs/wiki/testing-and-quality.md`, and `docs/wiki/local-development.md`.
* `src/TNC.Trading.Platform.AppHost/AppHostCompositionConstants.cs`, `AppHostEnvironmentWiring.cs`, and `AppHostInfrastructureRegistration.cs` currently preserve a fixed Keycloak `https://localhost:8080` authority. The migration must instead propagate the Keycloak named HTTP endpoint reference to API and Web and update the associated AppHost unit tests.
* No committed CI workflow exists. Add `.github/workflows/ci.yml` with distinct fast, Docker-backed, browser/real-Keycloak, and final-summary jobs. Publish per-project TRX unconditionally and redacted resource, SQL, and Playwright diagnostics only on failure.

## Decision Record

Selected path: retain bUnit, remove only its unsafe direct AngleSharp override; use named Aspire resources and randomized ports for AppHost-backed tests; use an owned SQL fixture for service-local persistence tests.

The shared AppHost fixture remains linked source under `test/Shared` to match the existing helper distribution and avoid a new project. Its public contract exposes named HTTP clients, resource endpoint URIs, bounded health readiness, builder-scoped configuration, and asynchronous disposal. It exposes no process handle, TCP listener inventory, dashboard URI, fixed port, or environment mutation.

Before retiring the fixed Keycloak workflow, prove a real authorization-code flow works with the randomized Web callback/origin. This is an explicit migration gate, not an open-ended investigation.

The Keycloak issuer/proxy expression needs a focused composition assertion before implementation locks its serialized form. The semantic contract is that API/Web authority derives from the named Keycloak endpoint and contains no fixed `localhost:8080` port.

## Planned Validation

Run each changed project twice after its migration. Then run API integration, Web functional, and Web E2E concurrently under ordinary test parallelism, followed by the complete solution build and test gate. Preserve failure diagnostics outside the worktree.
