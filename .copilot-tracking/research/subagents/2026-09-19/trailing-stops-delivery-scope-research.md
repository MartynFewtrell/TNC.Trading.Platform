---
title: Trailing Stops Delivery Scope Research
description: Verified delivery surfaces for trailing-stops alignment and account-mismatch behavior
author: GitHub Copilot
ms.date: 2026-09-19
ms.topic: reference
---

## Status

Complete. This document verifies the current implementation and test surfaces
without changing product behavior.

## Research Questions

1. Verify the CSS alignment correction and associated Playwright E2E assertions.
2. Verify account-mismatch behavior across the Application, API,
	 Infrastructure, and Web layers, including current operational recovery.
3. Verify repository-standard build and test commands for this delivery scope.

## Verified Findings

### CSS Alignment and Browser Coverage

The configuration controls are native Blazor radio inputs and an HTML button.
The layout defect is caused by CSS rather than Radzen component behavior.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:40-56`
	renders the control row, radio fieldset, and Save button.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:68-85`
	makes the fieldset grow with `flex: 1 1 auto` and bottom-aligns the row with
	`align-items: end`. The growing fieldset pushes Save to the right; its
	different height produces the vertical misalignment.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:89-121`
	supplies the radio-label sizing, keyboard focus, and wrapping behavior that
	must remain unchanged.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:82-108`
	is the focused desktop geometry test. Its current assertions only prove that
	Save is to the right of the fieldset and remains inside the row; they do not
	assert adjacent placement or vertical-center alignment.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:110-126`
	separately protects narrow-viewport visibility and absence of horizontal
	overflow. `:130-147` protects keyboard radio selection.

The narrow implementation surface is the two declarations at
`AccountPreferences.razor.css:68-85`: remove or change the fieldset flex-grow
and center the row cross-axis alignment. The desktop E2E assertion should use
bounding boxes to prove Save starts near the fieldset's natural right edge and
is vertically centered to a defined tolerance, while retaining the existing
no-overflow and keyboard coverage.

### Account-Mismatch Guard and Recovery

The guard is deliberately fail-closed. A stored account binding must equal the
latest authenticated IG snapshot before either Check status or Save performs
account-preference I/O. The infrastructure gateway repeats this comparison
against the account returned by its newly created IG session.

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesAccountMismatch.cs:3-8`
	defines the stable problem type, non-identifying title, and operator recovery
	text: reauthenticate the intended account, then check status; contact an
	administrator to change the configured account.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:15-31`
	loads the latest broker-environment snapshot and rejects absent or differing
	durable account IDs before gateway remediation. `:37-54` binds a new save to
	the snapshot ID and performs the IG action only after the guard succeeds.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:14-21`
	rejects absent or differing snapshot IDs before acquiring the lease or
	observing provider preferences. `:26-42` performs observation/remediation
	only after the account guard succeeds.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:13-27`
	repeats an ordinal account comparison before observation. `:32-50` repeats
	it before preference reads or writes in remediation. `:109-118` obtains the
	provider's `currentAccountId` from a new IG session.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-60`
	maps the protected GET, PUT, and Check status routes. `:145-158` returns a
	`200 OK` projection with `VerificationFailed` and the safe failure summary
	when Check status has an existing state; it returns a problem only when no
	state exists.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:32-42`
	maps a failed save to problem details. `:51-67` maps account mismatch to the
	stable problem type and HTTP `409 Conflict`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:43-76`
	renders an account-mismatch save problem as `SaveError`. `:91-98` applies
	the Check status projection instead of treating its verification failure as
	a blocked UI command.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:14-20`
	presents current and verification failure messages as alerts. `:33-56`
	disables commands only during loading or active save/check operations, not
	because the saved state records an account mismatch.

The operational recovery supported today is: authenticate the intended IG Demo
account, verify that authentication succeeds, then select Check status. Do not
use Save to rebind and do not edit SQL directly. A persistent warning after
reauthentication needs browser network/console evidence to distinguish a true
ID mismatch from authorization, stale revision, lease, or provider failure.

The existing tests prove this boundary:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:18-27`
	asserts Check status returns the mismatch warning without observation I/O.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs:30-47`
	asserts Save returns the typed conflict without remediation or persistence.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:18-29`
	asserts the gateway rejects a session mismatch before the preference endpoint.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:107-147`
	asserts both routes preserve the durable binding and make no provider GET or
	PUT preference call. `:163-178` verifies the precise API response contract.

### Repository Validation Commands

`global.json:1-6` pins SDK `10.0.302`. The standard commands are documented in
`docs/wiki/local-development.md:100-106` and `:230-240`:

```powershell
dotnet build
dotnet test
```

For focused delivery validation, run the affected browser suite after Docker is
available, because the E2E project references Aspire hosting and Playwright:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferencesE2ETests"
```

For a guard-only change, the smallest associated checks are:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~IgAccountPreferencesGatewayTests"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~AccountPreferencesFunctionalTests"
```

`docs/wiki/local-development.md:7-26` establishes Docker as a local-runtime
prerequisite. `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:1-27`
confirms the E2E suite uses `Aspire.Hosting.Testing` and
`Microsoft.Playwright.Xunit` plus the AppHost project. The standard fallback
for intermittent repository-wide MSBuild child-node exits is `dotnet test -m:1`
at `docs/wiki/local-development.md:234-240`.

## Recommended Implementation Sequence

1. Change only the two control-row sizing/alignment declarations and update the
	 desktop bounding-box assertion to express adjacent and centered alignment.
2. Run the focused Account Preferences E2E class, preserving narrow-layout and
	 keyboard assertions, then run `dotnet build`.
3. Do not change account-mismatch behavior to resolve the reported incident.
	 Reauthenticate the intended IG account and run Check status operationally.
4. If intentional account changes are a product requirement, define a separate
	 administrator-only, audited rebind workflow with fresh target confirmation,
	 expected revision, lease handling, and dedicated tests.
5. Run the focused Application, Infrastructure, and Functional suites only if
	 a later change touches identity comparison or recovery semantics; finish
	 with repository-wide `dotnet test` for a merged delivery.

## Unresolved Gaps and Decisions

* IG documentation has not been verified for `currentAccountId` case and
	whitespace semantics. Do not add trimming or case-insensitive comparison
	without that provider contract, and apply any approved canonicalization at
	every capture, persistence, comparison, lease, and migration boundary.
* The current code supports recovery to the already-bound account, not an
	intentional account switch. Product ownership must decide whether an
	administrator-only rebind workflow is required.
* The exact desktop geometry tolerance should be selected when implementing the
	E2E assertion. It should accommodate browser sub-pixel layout while still
	failing when the fieldset grows to consume the row.
