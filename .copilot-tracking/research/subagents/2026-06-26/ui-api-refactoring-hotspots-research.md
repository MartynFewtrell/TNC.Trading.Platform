---
title: UI and API Refactoring Hotspots Research
description: Research notes on Web and API refactoring hotspots that reduce testability
author: GitHub Copilot
ms.date: 2026-06-26
ms.topic: overview
keywords:
  - refactoring
  - testability
  - web
  - api
estimated_reading_time: 6
---

## Status

Complete

## Research Scope

* Inspect src/TNC.Trading.Platform.Web/ and src/TNC.Trading.Platform.Api/ for code quality and design shapes that make testing harder than necessary
* Inspect corresponding tests under test/TNC.Trading.Platform.Web/ and test/TNC.Trading.Platform.Api/
* Record evidence-backed hotspots with file paths, symbols, and line numbers

## Strongest Findings

* Fat Razor pages own data loading, auth gating, error handling, UI state, and request-to-view-model translation in one place. Evidence: src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:31,190,203,218,246,290,325 and src/TNC.Trading.Platform.Web/Components/Pages/Status.razor:239,253,268,289. Testability impact: component tests can only verify behavior by rendering the full page and driving BUnit against framework lifecycle methods instead of calling a focused presenter or page model. That makes edge-case coverage expensive and leaves parsing and branching logic coupled to Blazor rendering.

* The home page repeats the same orchestration shape as status and configuration, including auth checks, audit side effects, redirect timing, API calls, and alert composition. Evidence: src/TNC.Trading.Platform.Web/Components/Pages/Home.razor:115,133,146,163. Testability impact: logic such as access-denied auditing, redirect timing, and alert creation is buried inside component lifecycle hooks, so tests must render the component and inspect navigation or handler side effects instead of exercising a narrow seam with direct inputs and outputs.

* The Web HTTP client is chatty and repetitive, with each method rebuilding the same authorized-request, send, status-check, and JSON-deserialize flow. Evidence: src/TNC.Trading.Platform.Web/PlatformApiClient.cs:13,27,41,71,117. Testability impact: individual behaviors are testable only through message-handler assertions, as shown by test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs:15,95,200. This shape encourages many near-duplicate tests and makes cross-cutting concerns such as error mapping, cancellation, and scope selection harder to cover once in one place.

* Mapping and validation logic is duplicated across layers instead of being concentrated behind one contract seam. Evidence: src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:290,325; src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationMapping.cs:5; src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusMapping.cs:5; src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:5; src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:78. Testability impact: date parsing, enum parsing, CSV conversion, and response-shape translation must be proven indirectly through component or endpoint flows, which increases setup cost and raises the risk of drift between client-side and API-side translators.

* API endpoint composition is centralized in one large static module that mixes routing, authorization, validation handling, mapping, and operational event persistence. Evidence: src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:26,33,39,43,47,78,130. Testability impact: there is no narrow test file for endpoint composition itself, while current API tests focus on auth registration and selected helpers such as test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs:20 and test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthAuditEventResolverTests.cs:18. Route-level policies, result shapes, and binding rules therefore tend to get covered only through broader integration behavior.

* Startup and auth behavior is heavily embedded in Program and authentication registration glue, which drives tests toward full-host or real-process fixtures. Evidence: src/TNC.Trading.Platform.Api/Program.cs:15,37,43,49,63,90; src/TNC.Trading.Platform.Web/Program.cs:10,35,37,59,72; src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:11,63,110,162. Testability impact: there are seam-level tests for parts of auth registration in test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebAuthenticationServiceCollectionExtensionsTests.cs:18 and test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthenticationRegistrationTests.cs:17, but end-to-end auth confidence still depends on expensive fixtures that boot the app host or distributed app, such as test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:9,11 and test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:13,19,24,25.

* The Web unit-test harness is itself a signal that core page behavior is over-coupled to framework plumbing. Evidence: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:21,23,56,83,97 plus page tests in test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:16,40,77 and test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StatusTests.cs:16,80. Testability impact: each page test must assemble authentication state, HTTP context, audit client, access token provider, navigation coordinator, and API client before any page-specific assertion can run. That shared setup cost is a direct consequence of behavior living inside components instead of behind smaller interfaces.

## Prioritized Recommendations

1. Extract page-level orchestration from Home, Status, and Configuration into focused services or page-model style classes that return plain result objects for load, save, redirect, and alert decisions.
2. Collapse PlatformApiClient onto a reusable send-and-deserialize helper, or split it by feature area, so authorization scope selection and error translation can be tested once instead of per method.
3. Move configuration parsing and mapping into dedicated mappers/value objects shared by page and API boundaries, then add direct unit tests for CSV, enum, and time parsing without rendering components or hitting endpoints.
4. Break PlatformEndpoints into feature-local endpoint modules or explicit endpoint builder methods, then add narrow tests for route registration, required policies, and typed result behavior.
5. Isolate startup workflows in Program into injectable startup tasks so database initialization, retention processing, configuration application, and state ticks can be unit-tested without full host boot.
6. Reduce PlatformComponentTestContext responsibilities by introducing thinner abstractions for scope checks, operator context, and API reads so page tests can stub one seam instead of reconstructing most of the auth stack.

## Open Questions

* None.
