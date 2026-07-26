---
title: Testability Gaps Research
description: Research report on current testing strategy, testability gaps, and refactoring opportunities in the TNC.Trading.Platform repository
author: GitHub Copilot
ms.date: 2026-06-26
ms.topic: overview
keywords:
  - testing
  - testability
  - refactoring
  - unit-tests
  - integration-tests
estimated_reading_time: 8
---

## Status

Complete

## Strongest Findings

1. Web page behavior is being tested through a heavy component harness because page logic and request-shaping live inside the Razor component.

Evidence:
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:15 drives the Configuration page through bUnit for default section state, save error preservation, and restart messaging.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:23 constructs a full test context with authentication state, HTTP context, navigation, audit client, access token provider, API client, shell context provider, and theme state.
* src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:190 keeps load orchestration in OnInitializedAsync, save orchestration in SaveAsync at line 218, and form mapping/parsing in the nested ConfigurationFormModel at line 246.

How this affects testability:
* Simple behavior checks require a broad fixture with auth, navigation, and HTTP plumbing.
* Parsing and mutation rules are not directly unit-testable without rendering the page.
* Test failures can be brittle because UI markup, auth wiring, and request flow are coupled in one test seam.

Refactoring opportunity:
* Extract a configuration page presenter or form service that owns load, save, and form mapping logic.
* Keep the Razor component thin so direct unit tests can target parsing, validation, restart messaging, and failure handling without bUnit.

2. Real authentication coverage depends on full AppHost and external-style readiness flows, which makes the most expensive path carry core auth behavior.

Evidence:
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:11 references Aspire.Hosting.Testing and line 27 references the AppHost project.
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:15 builds and starts the distributed AppHost, then waits for keycloak and sql health at lines 24 and 25, API readiness at line 28, and token endpoint readiness at line 30.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:11 through line 23 pulls in Aspire.Hosting.Testing, Playwright, and the AppHost project.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:11 through line 23 does the same for E2E coverage.

How this affects testability:
* Important authentication behavior is validated primarily through slow, environment-sensitive flows.
* Failures can come from container startup, identity-provider readiness, port discovery, or browser timing instead of product behavior.
* The repository has direct unit coverage for some auth helpers, but end-to-end fixtures still carry a large share of confidence for auth journeys.

Refactoring opportunity:
* Extract a narrower authentication flow boundary, for example a sign-in/session orchestration service or a route-guard policy service, so redirect and session-validity decisions can be tested without AppHost.
* Keep only a thin set of smoke tests on the distributed path.

3. AppHost process management is duplicated across multiple high-cost test suites, which increases maintenance cost and brittleness.

Evidence:
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAppHostProcessFactory.cs:5 defines one AppHost process factory.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs:5 defines another near-copy for Web functional tests.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:9 adds a third variant for E2E tests.
* grep results also show duplicate AppHostProcessHandle implementations in API integration, Web functional, and Web E2E suites.

How this affects testability:
* Process startup, port discovery, and readiness logic are repeated in several places.
* Fixes to startup handling or readiness detection must be applied to multiple copies.
* Duplicate harness code makes the expensive suites more fragile and obscures the actual product assertions.

Refactoring opportunity:
* Consolidate process and readiness helpers into a shared test infrastructure package or shared test project.
* Standardize one AppHost launcher abstraction so functional and E2E tests reuse the same diagnostics and startup rules.

4. Application coordinator tests exist, but many of them are broad persistence-backed scenario tests because orchestration logic is tightly coupled to EF stores and side effects.

Evidence:
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:29 constructs PlatformStateCoordinator with EF runtime state, retry cycle, event, and login snapshot stores plus credential service and notification dispatcher.
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:804 shows the reusable factory still wiring the coordinator against concrete EF-backed infrastructure.
* src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:21 declares a wide constructor surface and line 122 starts TickAsync, which coordinates schedule gating, blocked-live handling, session expiry, retries, IG login capture, notifications, and event persistence.
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/RetryPolicyTimingTests.cs:29 only isolates the static delay calculation, while most richer behaviors stay in large scenario tests.

How this affects testability:
* The coordinator can be tested, but each test tends to exercise several persistence and side-effect paths at once.
* It is harder to isolate a single transition rule from storage behavior.
* The behavioral surface is large enough that adding new cases will likely keep expanding a monolithic scenario suite.

Refactoring opportunity:
* Split state-transition decisions from effect execution.
* Move retry scheduling, degraded/active transition rules, and notification decisions into smaller pure policy objects that can be unit-tested without EF stores.

5. Infrastructure tests rely heavily on EF InMemory and local data-protection setup, which is cheap but can hide store-boundary issues and keeps logic in storage tests.

Evidence:
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/InfrastructureReflection.cs:11 creates PlatformDbContext with EF InMemory at line 14 and a temporary DataProtectionProvider at lines 20 to 21.
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfPlatformIgLoginSnapshotStoreTests.cs:17 uses the real store and DB context to verify snapshot-retention rules.
* grep results show the same CreateDbContext helper used across NotificationProviderTests, OperationalRecordRetentionProcessorTests, ProtectedCredentialServiceTests, and SqlPlatformConfigurationStoreTests.

How this affects testability:
* Persistence-facing rules are exercised cheaply, but the in-memory provider does not model all relational behavior.
* Business rules such as retention or snapshot classification stay embedded in store tests rather than in isolated domain services.
* Changes to data shape or persistence details can break many behavior tests together.

Refactoring opportunity:
* Extract retention and snapshot-selection rules into pure domain helpers, leaving EF tests to verify mapping and persistence only.
* Keep a smaller number of persistence tests and move rule-heavy assertions into storage-agnostic unit tests.

6. API endpoint behavior has thin direct coverage relative to the amount of inline HTTP/result mapping and exception translation in the endpoint layer.

Evidence:
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:26 through line 43 maps multiple protected endpoints in one static class.
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:56 handles request validation and response mapping for UpdatePlatformConfigurationAsync.
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:74 translates InvalidOperationException into HTTP 409 for TriggerManualAuthRetryAsync.
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:99 through line 138 resolves and records auth audit events with direct dependency access and inline response generation.
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs:10 covers auth registration, but grep found no direct tests targeting PlatformEndpoints or its exception/result mapping.

How this affects testability:
* Endpoint semantics are likely being trusted indirectly through broader integration paths.
* Validation-problem, conflict, and audit-recording responses are harder to verify quickly in isolation.
* Regressions in HTTP contract shaping can slip into heavier suites or go untested.

Refactoring opportunity:
* Extract endpoint handlers or use injectable endpoint services for audit recording and request-to-result translation.
* Add narrow endpoint tests around result mapping once logic is moved out of static methods.

7. Background supervision appears to have no direct tests even though it controls a core execution path.

Evidence:
* src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs:7 runs an infinite loop that creates a scope, resolves PlatformStateCoordinator, logs errors, and delays every second.
* grep found no test references for PlatformAuthSupervisor under test/.

How this affects testability:
* The service’s cancellation, retry-after-exception, and scope-per-tick behavior are not directly protected.
* Failures in host-level supervision may only surface through higher-level runtime behavior.

Refactoring opportunity:
* Extract a single-tick runner or clock-driven loop abstraction so supervision semantics can be tested without a live BackgroundService loop.

## Prioritized Recommendations

1. Extract pure decision objects from PlatformStateCoordinator first. This looks like the highest leverage seam because it would reduce the size and cost of future application tests while preserving the existing scenario suite as regression coverage.
2. Pull configuration page load, save, and form-mapping logic out of Configuration.razor into a presenter or form service. This should convert brittle component tests into cheaper direct unit tests.
3. Consolidate duplicated AppHost process helpers into shared test infrastructure. This will not change product design, but it should reduce maintenance cost and noise across the most expensive suites.
4. Extract API endpoint translation logic, especially audit recording and exception-to-result mapping, into injectable handlers. That would unlock fast contract-level tests without full API host startup.
5. Isolate retention and snapshot-selection rules from EF-backed stores so infrastructure tests focus on persistence boundaries instead of carrying domain behavior.
6. Add a test seam around PlatformAuthSupervisor by extracting one-iteration execution logic and delay control.

## Open Questions

* None. The main gaps and refactoring opportunities were identifiable from repository evidence.