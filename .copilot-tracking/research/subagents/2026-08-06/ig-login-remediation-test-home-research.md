---
title: IG Login Remediation Test Home Research
description: Identifies the narrowest existing test surface for IG configuration remediation behavior coverage.
author: GitHub Copilot
ms.date: 2026-08-06
ms.topic: reference
keywords:
  - IG
  - configuration
  - testing
---

## Research Scope

* Identify the best existing test project and exact test file or surface for IG configuration remediation-state coverage.
* Inspect API and Web configuration endpoint or UI tests and the DR-02 plan-validator finding.
* Determine whether one test project can cover both API response mapping and the rendered operator message.

## Findings

### DR-02 Scope

DR-02 identifies two untested Phase 3 behaviors: the safe configuration remediation
projection and the conditional operator message. The validator explicitly says the
current plan only builds the API and Web projects.

* Evidence: `.copilot-tracking/plans/logs/2026-08-06/ig-login-403-degraded-health-log.md:8`
* The Phase 3 design requires API response mapping and Web view-model projection of
  per-field usability plus `RequiresCredentialReentry`, then a configuration-page
  message requiring all three Demo secrets in one write-only save.
* Evidence: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md:157-196`

### API Response Mapping Home

Use the existing API unit-test project:

`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`

Add `GetPlatformConfigurationMappingTests.cs` in that project. This is a new file in
the existing test project, focused on
`GetPlatformConfigurationMapping.ToResponse`, rather than extending an endpoint,
integration, or Web test.

* The mapping directly builds `GetPlatformConfigurationResponse` in
  `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationMapping.cs:7`.
* The response contract is declared in
  `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationResponse.cs:3`.
* The closest local API-unit pattern is
  `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/GetPlatformStatusMappingTests.cs:5`.
* `src/TNC.Trading.Platform.Api/Properties/InternalsVisibleTo.cs:3` exposes API
  internals to this project, making it the narrowest project able to directly assert
  the server mapper and secret-free boolean projection.

Recommended behavior surface: construct the application configuration response with
one or more unusable credentials, call `ToResponse`, and assert every safe usability
or aggregate re-entry boolean. Assert that the API response contains no secret,
ciphertext, exception, or key identifier fields. Add a complementary all-usable case
that clears the aggregate re-entry signal.

Focused command after the file exists:

```powershell
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~GetPlatformConfigurationMappingTests"
```

### Web Operator Message Home

Use the existing Web unit-test project and extend its current configuration component
class:

`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs`

The test should supply a configuration payload with
`RequiresCredentialReentry` set to `true`, render `Configuration`, then use the
established bUnit `WaitForAssertion` pattern to assert the new conditional message.
It should also assert that the three secret-edit inputs remain blank and that no
persisted secret value appears in the markup.

* `ConfigurationTests` already renders the configuration page at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:51`
  and waits on asynchronous rendering with `WaitForAssertion`.
* Its shared fixture is `PlatformComponentTestContext` at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:22`.
* Extend `PlatformWebTestData.CreateConfiguration` at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebTestData.cs:48`
  with an optional remediation-state argument, following its existing
  `restartRequired` parameter pattern.
* Render the message in the existing IG credentials section, adjacent to the current
  secret-handling hint at
  `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:147`.
* Existing stable selectors for the stored-state content are
  `configuration-stored-api-key`, `configuration-stored-identifier`, and
  `configuration-stored-password` at
  `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:149-151`.
* Add a dedicated `data-testid` to the new remediation message. This is more stable
  than selecting the IG accordion by CSS structure and matches the current component
  convention.

The Web project also has a lower-level configuration deserialization test at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs:49`.
Extend that test only if a separate assertion is desired for Web HTTP JSON parsing of
the new booleans. It does not prove the API server mapping.

Focused command after the component test exists:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ConfigurationTests"
```

Optional focused Web-client projection command:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~PlatformApiClientTests"
```

### One-Project Decision

No. One existing test project cannot adequately cover both required layers.

`TNC.Trading.Platform.Web.UnitTests` can cover Web deserialization and the visible
message, but it references only the Web project and cannot directly exercise the
internal API mapper. `TNC.Trading.Platform.Api.UnitTests` can directly assert the
server response mapping, but it has no bUnit dependency or Web component reference.
Using a higher-cost API-to-browser functional or E2E test would leave the redaction
contract less precise and would not be the narrowest existing test surface.

## References

* `.copilot-tracking/plans/logs/2026-08-06/ig-login-403-degraded-health-log.md`
* `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/GetPlatformStatusMappingTests.cs`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs`
