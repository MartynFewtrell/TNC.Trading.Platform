<!-- markdownlint-disable-file -->
# Compatibility Research: bUnit and AngleSharp

Research the Phase 4 package blocker in the Web unit tests: `bunit.web 1.40.0` with a direct `AngleSharp 1.5.0` reference throws `MissingMethodException` for `AngleSharp.Dom.IHtmlCollection<T>.get_Item(Int32)`. Scope is evidence and recommendation only; no repository source or project files were changed.

## Question

Determine the smallest supported package change that removes the AngleSharp vulnerability while keeping the Web unit tests passing.

## Repository Evidence

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` currently references:
  * `bunit.web` `1.40.0`
  * direct `AngleSharp` `1.5.0`
  * target framework `net10.0`
* The resolved graph from `dotnet list ... package --include-transitive` is:
  * `bunit.web` `1.40.0`
  * `bunit.core` `1.40.0`
  * `AngleSharp` `1.5.0`
  * `AngleSharp.Css` `1.0.0-beta.154`
  * `AngleSharp.Diffing` `1.0.0`
* The direct `AngleSharp 1.5.0` reference is therefore winning NuGet's version selection over bUnit's declared `AngleSharp 1.2.0` dependency.

## Reproduced Failure

Command:

```powershell
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj" --no-restore --filter "FullyQualifiedName~TNC.Trading.Platform.Web.UnitTests" -v:minimal
```

Result on 2026-09-20: **Failed: 5, Passed: 133, Skipped: 0, Total: 138**.

All five failures are rooted in:

```text
System.MissingMethodException: Method not found: '!0 AngleSharp.Dom.IHtmlCollection`1.get_Item(Int32)'.
```

The failures include `AccountPreferencesTests.Save_ShouldSendBooleanJsonValue_WhenSelectionIsChanged` (both data rows), `AccountPreferencesTests.Render_ShouldShowSettingsInformationArchitecture_WhenInitialLoadSucceeds`, `AccountPreferencesTests.Select_ShouldChangePendingValue_WhenOperatorChoosesEnabledRadio`, and a `ConfigurationTests` wait failure wrapping the same exception. This disproves the current direct `AngleSharp 1.5.0` override as a passing solution.

## Package Metadata and Availability

Configured NuGet sources are:

```text
nuget.org https://api.nuget.org/v3/index.json
Microsoft Visual Studio Offline Packages C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\
```

`dotnet package search` reports:

* `bunit.web` latest: `1.40.0`
* `bunit` latest: `2.11.3`
* `AngleSharp` latest: `1.8.2`
* `AngleSharp.Css` latest: `1.1.2`
* `AngleSharp.Diffing` latest: `1.1.1`

The bUnit 1.40.0 nuspec declares `AngleSharp` version `1.2.0` as a minimum dependency in the `net9.0` and compatible framework groups; it does not declare an upper bound. This is a NuGet resolution allowance, not proof of binary compatibility. The runtime test above demonstrates that the allowed override is not compatible with this bUnit build.

The public bUnit release line has moved to package ID `bunit` for 2.x. The first stable 2.x version listed by nuget.org is `2.0.66` (earlier 2.0 entries are previews). Its published nuspec has a `net10.0` group with:

* `AngleSharp >= 1.3.0`
* `AngleSharp.Css >= 1.0.0-beta.157`
* `AngleSharp.Diffing >= 1.1.0`

The bUnit package page documents the compatibility matrix as 1.x for .NET Core 3.1 through .NET 9 and 2.x for .NET 8 and later. The repository targets `net10.0`, so bUnit 2.x is in its supported framework range.

References:

* [bUnit 2.11.3 package page](https://www.nuget.org/packages/bunit/2.11.3)
* [bUnit release list](https://github.com/bUnit-dev/bUnit/releases)
* [AngleSharp 1.5.0 package page](https://www.nuget.org/packages/AngleSharp/1.5.0)
* [AngleSharp 1.4.0 package page](https://www.nuget.org/packages/AngleSharp/1.4.0)
* Existing advisory evidence in `.copilot-tracking/research/2026-09-20/build-tests-aspire-account-preferences-research.md`: `AngleSharp 1.2.0` is affected by `GHSA-pgww-w46g-26qg`; the repository's target patched floor was previously identified as `1.5.0`.

## Recommendation

The smallest supported package-level change is a bUnit major-package migration to the first stable 2.x package, `bunit 2.0.66`, rather than forcing `AngleSharp 1.5.0` under `bunit.web 1.40.0`:

```xml
<!-- remove -->
<PackageReference Include="AngleSharp" Version="1.5.0" />
<PackageReference Include="bunit.web" Version="1.40.0" />

<!-- add -->
<PackageReference Include="bunit" Version="2.0.66" />
```

The direct AngleSharp reference should be removed so bUnit 2.0.66 owns the compatible parser family. Its declared `AngleSharp >= 1.3.0` floor is below the patched target, and NuGet should be allowed to resolve the patched AngleSharp version explicitly during implementation validation. If the resolved version is below the repository's vulnerability-fixed floor, retain a direct `AngleSharp` reference at `1.5.0` only after confirming the bUnit 2.0.66 test suite passes; do not retain that override with bUnit 1.40.0.

A newer alternative is `bunit 2.11.3`, which declares `AngleSharp 1.8.1`, `AngleSharp.Css 1.1.2`, and `AngleSharp.Diffing 1.1.1` for `net10.0`. It is likely the lower-risk long-term choice for current package maintenance, but it is not the smallest change because it upgrades farther than necessary.

## Risks and Unresolved Evidence

* This research did not modify the project or execute tests against bUnit 2.0.66. Passing Web tests after the package migration remains an implementation validation requirement.
* bUnit 2.x is a major package transition. Existing namespaces and test APIs may be source-compatible, but the migration guide must be checked if compilation or runtime failures appear.
* `AngleSharp 1.4.0` is not a safe workaround: NuGet currently reports a moderate vulnerability for that version, so downgrading from 1.5.0 would trade the ABI failure for an unresolved advisory.
* Keeping `bunit.web 1.40.0` and pinning any AngleSharp 1.5+ version is rejected by the reproduced runtime failure, regardless of the nuspec's missing upper bound.
* The package graph should be checked after migration for duplicate or incompatible `AngleSharp.*` versions and for any new vulnerability diagnostics.

## Focused Validation Commands

Run these after changing only the Web unit project package references:

```powershell
dotnet restore "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj"
dotnet list "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj" package --include-transitive
dotnet build "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj" --no-restore -warnaserror
dotnet test "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj" --no-restore --filter "FullyQualifiedName~TNC.Trading.Platform.Web.UnitTests"
dotnet list "test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj" package --vulnerable --include-transitive
```

The acceptance result is zero `MissingMethodException` failures, all 138 Web unit tests passing, and no AngleSharp vulnerability in the resolved graph. If bUnit 2.0.66 needs test-source migration, test that migration separately; do not mask the failure by weakening assertions or by retaining the known-incompatible bUnit 1.40.0 plus AngleSharp 1.5.0 combination.
