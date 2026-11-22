---
title: Humanizer NU1608 Investigation
description: Evidence-based investigation of Humanizer package version conflicts in Web FunctionalTests and E2ETests
ms.date: 2026-09-14
ms.topic: troubleshooting
---

# Humanizer NU1608 Investigation

## Questions

* Which direct or transitive packages introduce Humanizer.Core 3.0.10 and Humanizer.Core.<culture> 2.14.1?
* Is a safe verified direct package-root alignment available for both affected test graphs?
* What smallest project-file change eliminates the warnings without suppression?

## Evidence

Both generated graphs contain the same relevant package set for `net10.0`:

* `Humanizer.Core/3.0.10` is resolved from `Aspire.Hosting/13.5.3` and
	`JsonPointer.Net/7.0.1`. The cached nuspec files declare exact
	`Humanizer.Core` version `3.0.10` dependencies for the applicable target
	frameworks.
* `WireMock.Net.Shared/2.15.0` requires `Handlebars.Net.Helpers.Humanizer`
	`2.5.5`.
* `Handlebars.Net.Helpers.Humanizer/2.5.5` requires `Humanizer` in the range
	`[2.14.1, 4.0.0)`; the graph resolves `Humanizer/2.14.1`.
* `Humanizer/2.14.1` requires its culture packages, including
	`Humanizer.Core.af`, `Humanizer.Core.ar`, and the other
	`Humanizer.Core.<culture>` packages, at exactly `2.14.1`. Each resolved
	culture package in both graphs is `2.14.1` and requires `Humanizer.Core`
	`[2.14.1]`.
* The NU1608 entries in both assets files therefore report the exact conflict:
	culture package `2.14.1` requires `Humanizer.Core (= 2.14.1)`, while NuGet
	resolves `Humanizer.Core 3.0.10`.

Evidence paths:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/obj/project.assets.json`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/obj/project.assets.json`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj`
* `%USERPROFILE%/.nuget/packages/aspire.hosting/13.5.3/aspire.hosting.nuspec`
* `%USERPROFILE%/.nuget/packages/jsonpointer.net/7.0.1/jsonpointer.net.nuspec`
* `%USERPROFILE%/.nuget/packages/wiremock.net.shared/2.15.0/wiremock.net.shared.nuspec`
* `%USERPROFILE%/.nuget/packages/handlebars.net.helpers.humanizer/2.5.5/handlebars.net.helpers.humanizer.nuspec`
* `%USERPROFILE%/.nuget/packages/humanizer/2.14.1/humanizer.nuspec`

The two project files directly reference `WireMock.Net` `2.15.0`; they do not
directly reference any Humanizer package. The functional-test project uses
`Microsoft.Playwright` `1.58.0`, and the E2E project uses
`Microsoft.Playwright.Xunit` `1.58.0`; this difference does not affect the
Humanizer conflict. No C# source under the Web test tree references `WireMock`.

No safe direct package-root alignment was verified. Pinning
`Humanizer.Core` to `2.14.1` would satisfy the culture packages but violate the
exact `3.0.10` requirements from Aspire and JsonPointer. Pinning it to
`3.0.10` leaves the culture packages' exact `2.14.1` constraints broken. The
local NuGet cache contains no `Humanizer` 3.x package and no
`Humanizer.Core.<culture>` 3.x packages that could provide a coherent 3.x
alignment.

## Recommendation

The smallest project-file change that removes the conflicting dependency, if
the unused-package check remains valid, is to remove the direct
`<PackageReference Include="WireMock.Net" Version="2.15.0" />` from both test
projects. This removes `WireMock.Net.Shared`, `Handlebars.Net.Helpers.Humanizer`,
`Humanizer`, and the culture packages while retaining Aspire's required
`Humanizer.Core 3.0.10`.

This recommendation should be applied only if no test source or shared test
fixture begins consuming WireMock. The current source search found no `WireMock`
usage. If WireMock is required, there is no safe warning-free direct pin based
on the verified package metadata; the correct fix would require a WireMock
release whose dependency graph no longer brings the Humanizer 2.14.1 culture
packages, or an upstream package fix.

## Follow-up Questions

None identified.
