---
title: bUnit Removal Alternatives Research
description: Evidence-based assessment of replacing bUnit and direct AngleSharp usage with xUnit, Aspire testing, WebApplicationFactory, and Playwright.
ms.date: 2026-09-15
ms.topic: research
---

## Status

Complete. No product or test code was modified. This assessment is based on the
current source tree and official guidance retrieved on 2026-09-15.

## Research scope

* Whether the repository can remove bUnit and direct AngleSharp usage
* Viable roles for xUnit, Aspire.Hosting.Testing, WebApplicationFactory, and
	Playwright
* Test-layer migration, coverage and feedback gaps, package implications, and
	an incremental removal plan
* A decisive recommendation supported by workspace evidence and official
	documentation

## Executive recommendation

Do not remove bUnit as part of centralizing the test strategy. Remove the direct
`AngleSharp` override immediately and restore bUnit's compatible dependency graph;
then retain bUnit for fast Blazor component rendering and lifecycle tests. Use xUnit
for pure presenter/view-model tests, `Aspire.Hosting.Testing` plus Playwright for a
small number of cross-process browser journeys, and `WebApplicationFactory` only for
new single-Web-host HTTP pipeline checks where a browser is unnecessary.

Full bUnit removal is technically feasible but is not an equivalent substitution:
it moves 49 current render/interactivity checks to a slower and more
environment-sensitive browser suite, or deletes their component-level assurance.
Microsoft's Blazor guidance explicitly identifies component unit tests and browser
E2E tests as complementary approaches. It says bUnit is a third-party component
unit-test library, while E2E tests include CSS, JavaScript, DOM, and browser APIs;
unit tests execute in milliseconds, expose the component instance, and are more
reliable. See [Test Razor components in ASP.NET Core Blazor](https://learn.microsoft.com/aspnet/core/blazor/test?view=aspnetcore-10.0).

## Current dependency and test inventory

### bUnit and AngleSharp ownership

* bUnit appears in exactly one active project: `bunit.web` 1.40.0 is a direct
	dependency at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:16`.
* Direct `AngleSharp` 1.8.1 appears only beside bUnit at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:15`.
	No `using AngleSharp` or `AngleSharp.` source usage was found beneath `src/` or
	`test/`.
* The resolved graph puts `AngleSharp.Css` 1.0.0-beta.154 and `AngleSharp.Diffing`
	1.0.0 under bUnit. The direct 1.8.1 reference overrides bUnit's declared
	AngleSharp 1.2.0 dependency. The existing failure investigation records four
	`MissingMethodException` failures before behavioral assertions at
	`.copilot-tracking/research/subagents/2026-09-15/failing-tests-investigation.md:29`
	and `:83-89`.
* Therefore, direct AngleSharp is not needed for the repository's own source code.
	Its removal is a low-risk package correction independent of any decision about
	bUnit. It must not be replaced by a different HTML parser unless a future
	`WebApplicationFactory` test genuinely needs to parse non-browser HTML.

Microsoft also states that AngleSharp in its integration-test sample is a third-party
demonstration dependency, not a supported or required ASP.NET Core integration-test
dependency. See [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0#anglesharp-vs-application-parts-for-antiforgery-checks).

### Existing UI test layers

| Layer | Current evidence | Appropriate responsibility |
| --- | --- | --- |
| xUnit unit tests | The Web unit project already tests presenters/view models without rendering, for example `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:10-44`, `HomeTests.cs:10-37`, `StatusTests.cs:10-52`, and `AccountPreferencesTests.cs:17-87` | Pure state, mapping, validation, transport decisions, cancellation, and error policy |
| bUnit component tests | Seven component test classes plus `PlatformComponentTestContext`, starting at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:22` | Razor rendering, component event handlers, mock JS interop, lifecycle, markup/accessibility contracts, and component-instance state |
| Aspire closed-box tests | API integration starts the AppHost and creates a named-resource client at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:33-40` and `:89` | Cross-process API, Web, SQL, Keycloak, and service-discovery behavior |
| Playwright browser E2E | `Microsoft.Playwright.Xunit` is installed at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:12`; `AccountPreferencesE2ETests` inherits `PageTest` at `AccountPreferences/AccountPreferencesE2ETests.cs:6` | Authenticated user journeys, browser DOM/JS/CSS interactions, accessible controls, and route navigation |
| Playwright-assisted functional tests | `Microsoft.Playwright` is installed at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:12`, but its account-preferences tests make HTTP assertions at `AccountPreferences/AccountPreferencesFunctionalTests.cs:29-93` | Requirement-level API and authenticated-session checks, not component rendering |

The existing Web E2E fixture still launches the AppHost through a custom process
factory at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:18-23`.
It should be migrated to the established Aspire builder model before adding browser
coverage. The current fixture has a known shared-provider port race, documented at
`.copilot-tracking/research/subagents/2026-09-15/failing-tests-investigation.md:120-130`.

## Viable alternatives and boundaries

### xUnit without a renderer

Keep and expand direct xUnit tests for presenters, view models, service registration,
and API-client behavior. This is already viable because the shared bUnit context has
a non-rendering path, `CreateServiceContext`, at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:126-130`.
Before bUnit removal, extract that service-registration path into a plain xUnit test
fixture so it no longer inherits `Bunit.TestContext` at line 22.

This absorbs the direct cases in Configuration, Home, Status, and Account
Preferences, including preference state/cancellation/save-policy tests at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:17-87`
and `:377-456`. It does not render Razor components and cannot replace tests that
call `RenderComponent`, `Find`, `Change`, `Click`, or `WaitForAssertion`.

### WebApplicationFactory

`WebApplicationFactory<TEntryPoint>` is viable for a new, separate Web integration
test project that needs fast in-process checks of redirects, authentication
challenges, route availability, headers, HTTP APIs, antiforgery, or server-rendered
HTML responses. It is not presently used anywhere in active test code, and no
`Microsoft.AspNetCore.Mvc.Testing` package is referenced.

It is not a bUnit replacement for this interactive-server UI. The production host
registers interactive server components at
`src/TNC.Trading.Platform.Web/Program.cs:13-14` and maps the interactive render mode
at `src/TNC.Trading.Platform.Web/Program.cs:71-72`. A TestServer `HttpClient` can
exercise the HTTP pipeline and initial HTML, but cannot perform a real browser's
interactive event, focus, CSS, and JavaScript behavior. The host also lacks the
`public partial class Program` test entry-point declaration commonly used by
`WebApplicationFactory`; adding one would be a small product-code change.

Microsoft recommends `Microsoft.AspNetCore.Mvc.Testing` and
`WebApplicationFactory` for a test web host, and recommends a browser tool such as
Playwright for SPA testing. See [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0).

### Aspire.Hosting.Testing

Do not use Aspire as a replacement for component tests or individual route tests. It
is the correct boundary for small numbers of distributed user workflows because it
launches the AppHost and resources as separate processes, does not permit
in-process service replacement, and supplies named-resource `HttpClient` instances.
This matches the repository's test instruction and the passing synthetic API
fixture.

The official Aspire guidance says to use `WebApplicationFactory<T>` for one project
in memory or mocked dependencies, and Aspire testing for closed-box, distributed
behavior. See [Aspire testing overview](https://aspire.dev/testing/overview/) and
[Write your first Aspire test](https://aspire.dev/testing/write-your-first-test/).

### Playwright

Playwright is the viable bUnit replacement only for tests whose value is the real
browser behavior. It is already installed with xUnit support and `PageTest`. Its
web-first assertions and per-test browser-context isolation make it suitable for
the important journeys. See [Playwright for .NET installation](https://playwright.dev/dotnet/docs/intro)
and [Writing tests with Playwright for .NET](https://playwright.dev/dotnet/docs/writing-tests).

## Migration map if bUnit removal is mandated

| Current component suite | Destination if bUnit is removed | Reason |
| --- | --- | --- |
| `AccountDetailsTests.cs:9-143` | Playwright E2E: empty state, type-tab selection, refresh failure, older/newer navigation, and selection retention | These tests render data, activate tabs/buttons, and inspect browser-visible panels |
| `AccountPreferencesTests.cs:95-368, 471-476` | Split: retain state/presenter tests as xUnit; migrate radio/save/dismiss/history paging/role UI flows to Playwright E2E | Rendering tests drive radio controls, inspect disabled states and status text, and rely on JS focus; pure state and presenter logic are already direct xUnit candidates |
| `ConfigurationTests.cs:49-169` | Playwright E2E for accordion defaults, validation-failure value retention, restart feedback, and credential-reentry rendering; retain `LoadAsync` and `SaveAsync` at `:10-44` as xUnit | The former checks Razor markup and event responses; the latter calls the presenter directly |
| `HomeTests.cs:43-81` | Playwright E2E for anonymous landing and no-role navigation; retain direct presenter tests at `:10-37` as xUnit | Route/auth redirects must be browser validated; alert calculation remains pure |
| `IgLoginHistoryTests.cs:11-104` | Playwright E2E | All four tests assert rendered empty/data/error/detail surfaces |
| `MainLayoutTests.cs:12-77` | Playwright E2E | Signed-in shell, protected navigation, and sidebar collapse require rendered layout interaction |
| `StatusTests.cs:58-215` | Playwright E2E for warning, role visibility, disabled retry button, retry message, and details; retain direct tests at `:10-52` as xUnit | Component interactions are browser behavior; presenter mapping and state classification are direct logic |

`WebApplicationFactory` can supplement this map with HTTP-only coverage for
unauthenticated redirect/challenge contracts and a small HTML smoke test. It should
not inherit the component cases simply because it is faster than Aspire: that would
either require reintroducing an HTML parser or reduce assertions to response text.

## Coverage and feedback holes without bUnit

Removing bUnit before adding the mapped browser coverage leaves the following gaps:

* No millisecond-scale Razor rendering checks for markup structure, empty/loading/error
	alternatives, conditional controls, or semantic attributes. Examples include
	`AccountDetailsTests.cs:19-49`, `IgLoginHistoryTests.cs:11-104`, and
	`MainLayoutTests.cs:12-77`.
* No direct component-instance/lifecycle inspection. The preference suite currently
	verifies cancellation by disposing rendered components at
	`AccountPreferencesTests.cs:276-300`; browser E2E can observe effects, not the
	component instance or cancellation token.
* Slower feedback and more failure modes for each UI variation. Microsoft describes
	component unit tests as millisecond-scale and more reliable, versus E2E tests as
	seconds-scale, environment-sensitive, and unable to inspect the component instance.
	The repository currently has 10 higher-level test failures caused by fixture port
	races before product assertions, recorded at
	`.copilot-tracking/research/subagents/2026-09-15/failing-tests-investigation.md:29-31`.
* Fewer cheap assertions over combinations of identity, response order, and failure
	modes unless those become direct presenter tests. The bUnit context controls
	authentication and sequenced HTTP responses at
	`PlatformComponentTestContext.cs:55-121`; reproducing each permutation through
	Keycloak/AppHost/browser adds substantial fixture cost.
* CSS, JavaScript, focus, and true accessibility behavior are not holes created by
	bUnit removal, because bUnit never establishes them fully. Those behaviors need
	targeted Playwright coverage regardless, especially the confirmation-focus behavior
	asserted through mock JS interop at `AccountPreferencesTests.cs:181-195`.

## Incremental plan

1. Remove the direct `AngleSharp` package reference from the Web unit-test project,
	 restore, and run the Web unit-test project. This restores bUnit's compatible
	 transitive version and resolves the current deterministic dependency regression.
2. Split `PlatformComponentTestContext` into a bUnit rendering fixture and a plain
	 service/provider fixture. Move all direct presenter/view-model cases to use the
	 latter, preserving fast xUnit coverage independently of bUnit.
3. Replace the custom process-launcher implementation in the Web E2E fixture with
	 `DistributedApplicationTestingBuilder`, named resource endpoints, readiness
	 checks, and deterministic disposal. Fix the provider port race before expanding
	 Playwright because current E2E and functional tests fail during setup.
4. Add a narrow `WebApplicationFactory` integration project only for HTTP-pipeline
	 contracts not already covered by API or Aspire tests. Reference
	 `Microsoft.AspNetCore.Mvc.Testing`, use the Web SDK as required by Microsoft
	 guidance, and expose an appropriate test entry point.
5. Add Playwright coverage for one representative journey from each rendered feature:
	 shell/navigation, status/manual retry, configuration save failure/success,
	 account details tabs/history, account preferences save/paging, and IG history.
	 Use accessible locators and web-first assertions.
6. For every bUnit class, retain its xUnit-only cases and migrate browser-essential
	 cases before deleting the corresponding bUnit tests. Run the focused browser
	 class repeatedly in CI conditions to establish timing and fixture reliability.
7. Remove `bunit.web`, the remaining bUnit fixture/helpers, and only then remove
	 direct AngleSharp if it was deliberately retained during the transition. Confirm
	 no source or package references remain and update the test strategy documentation.

## Package and project implications

* Immediate correction: remove direct `AngleSharp` 1.8.1 from the Web unit-test
	project. This is not a bUnit removal and should preserve `bunit.web` 1.40.0.
* Full removal: remove `bunit.web` and its transitive `bunit.core`,
	`AngleSharp.Diffing`, and `AngleSharp.Css`. The test project retains xUnit,
	`Microsoft.NET.Test.Sdk`, coverlet, the ASP.NET Core framework reference, and the
	Web project reference for presenter/view-model tests.
* Add `Microsoft.AspNetCore.Mvc.Testing` only in a separate Web integration-test
	project when specific HTTP-pipeline scenarios justify it. Do not add it to the
	unit-test project, which would violate the repository's unit/integration boundary.
* Retain `Aspire.Hosting.Testing` 13.5.3 and `Microsoft.Playwright.Xunit` 1.58.0 in
	the existing E2E project. The functional project's direct `Microsoft.Playwright`
	1.58.0 is required only by its browser-authentication session factory at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationSessionFactory.cs:16-26`.
* Playwright browser binaries must be installed for the target framework as part of
	local/CI setup. The existing packages alone do not make browser E2E tests
	executable.

## Follow-on questions

* Is the objective a mandatory dependency reduction, or a faster and more reliable
	UI feedback loop? The former supports full bUnit removal after migration; the
	latter supports retaining bUnit and removing only direct AngleSharp.
* Should browser journeys run on every pull request, or should the full
	Aspire/Keycloak/Playwright lane be scheduled separately from the focused xUnit
	suite? The answer determines the acceptable migration volume and CI duration.