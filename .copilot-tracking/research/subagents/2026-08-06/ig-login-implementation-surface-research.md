---
title: IG Login Implementation Surface Research
description: Current repository surfaces for credential usability remediation and the IG session protocol follow-up
author: GitHub Copilot
ms.date: 2026-08-06
ms.topic: reference
keywords:
  - IG
  - credentials
  - data protection
  - authentication
estimated_reading_time: 5
---

## Research Scope

Verify the current implementation surfaces for the permanent remediation selected
in `.copilot-tracking/research/2026-08-06/ig-login-403-degraded-health-research.md`:

* Distinguish stored credential presence from active-process decryptability
* Block IG authentication when any required credential is unusable
* Return and persist a redacted remediation state
* Re-save all supplied credential secrets safely
* Keep IG session v2 and `Accept` compatibility as a separately staged follow-up

## Status

Complete. The checked-out source, contracts, test seams, solution membership,
and focused validation commands are verified.

## Initial Hypothesis

`ProtectedCredentialService` currently treats ciphertext row presence as
configuration completeness while translating decryption failure to an empty
runtime value. Extending its inward-facing contract with redacted usability
metadata, then having `PlatformAuthenticationReconciler` short-circuit before
the gateway call, should be the smallest permanent repair. Focused unit tests
should disprove this hypothesis by showing that an unreadable stored credential
already blocks gateway authentication or is already represented in platform state.

## Verified Current Surfaces

### Credential Contract And Persistence

* `src/TNC.Trading.Platform.Application/Configuration/IProtectedCredentialService.cs`
  owns the inward credential port. It currently exposes separate
  `GetPresenceAsync`, `GetCredentialsAsync`, and `UpdateAsync` operations.
* `src/TNC.Trading.Platform.Application/Configuration/CredentialPresence.cs`
  defines `HasApiKey`, `HasIdentifier`, `HasPassword`, and `IsComplete` as a
  simple conjunction of the three presence flags.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs`
  carries that contract to all auth decision points.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs`
  obtains presence by selecting credential types only. `GetCredentialsAsync`
  catches `CryptographicException` and substitutes an empty string. Therefore,
  a ciphertext row that cannot be decrypted is currently present and complete
  for policy purposes but empty at the IG boundary.
* The same service updates each nonblank supplied secret independently. It
  protects the supplied value with the active Data Protection protector and
  reuses an existing row when present. It neither reads nor attempts to migrate
  an omitted or unreadable value.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs`
  calls `UpdateAsync` before `SaveChangesAsync`. Its configuration-audit
  metadata already records only the redacted booleans
  `SecretsUpdated.ApiKey`, `SecretsUpdated.Identifier`, and
  `SecretsUpdated.Password`.

### Authentication And Remediation State

* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs`
  decides whether to authenticate based on `PlatformConfigurationSnapshot.Credentials.IsComplete`.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs`
  checks the same property before its immediate-recovery request, records an
  `AuthAttempted` event only when it calls the gateway, and persists a degraded
  state with a redacted `BlockedReason` when credentials are incomplete.
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs`
  is a second auth entry point. It calls `IBrokerAuthenticationGateway`
  directly after committing the retry intent, without checking credentials.
  The permanent remediation must guard this path as well.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs`
  already persists `SessionStatus`, `IsDegraded`, `BlockedReason`, and
  `LatestFailureSummary`; redacted remediation can use these existing fields.
* `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/AuthStateResponse.cs`
  exposes the redacted `BlockedReason` to clients. No persistence migration is
  required for a new safe fixed message.

### Configuration Response And UI Projection

* `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/CredentialPresenceResponse.cs`
  and `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatedCredentialPresenceResponse.cs`
  currently return only three presence booleans.
* Their mapping types, `GetPlatformConfigurationMapping.cs` and
  `UpdatePlatformConfigurationMapping.cs`, are the HTTP projection points for
  redacted usability/remediation booleans.
* `src/TNC.Trading.Platform.Web/CredentialPresenceViewModel.cs`,
  `PlatformConfigurationViewModel.cs`, and
  `Components/Pages/Configuration/ConfigurationFormModel.cs` retain the same
  three-field shape. `ConfigurationFormModelMapper.cs` deliberately creates a
  blank update model, so rendering new usability state need not expose or
  preserve secret values.

### IG Protocol Follow-Up Surface

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
  sends the Demo `POST session` request, currently with `Version: 3`, JSON
  content, and no explicit `Accept` header. The same adapter then requires CST
  and `X-SECURITY-TOKEN` and uses them for `/accounts` v1 and `/positions` v2.
* Keep this adapter-only correction separate from credential usability:
  request `Accept: application/json; charset=UTF-8` and use session `Version: 2`
  if the existing CST/header-token model is retained. The Application port and
  reconciliation behavior should not change in this phase.

## Smallest Staged Implementation Path

1. Extend the application-owned credential projection with explicit, secret-free
   usability information for API key, identifier, and password. Preserve row
   presence separately, and redefine the policy predicate used for login as
   all three values both present and decryptable.
2. In `ProtectedCredentialService`, load the three rows once and return the
   per-field presence/usability result while preserving the existing
   per-nonblank `UpdateAsync` behavior. Do not try to decrypt, log, or migrate
   unreadable plaintext beyond the boolean result.
3. Propagate the redacted projection through the SQL configuration store, both
   configuration API responses, and Web view models. Render an operator action
   such as re-entering all three Demo credentials, but never secret values,
   ciphertext, protector/key identifiers, or decryptability exceptions.
4. Make the state transition engine and reconciler use the usability predicate.
   When unusable, transition to existing degraded state before writing
   `AuthAttempted` or calling IG, with a fixed redacted remediation reason such
   as `IG demo credentials must be re-entered.`
5. Apply the same predicate before
   `TriggerManualAuthRetryHandler` calls the gateway. Persist the retry intent
   and a redacted failure outcome consistently, or reject the manual action
   using an existing/new non-secret retry outcome after product decision.
6. Re-save all three credentials in one configuration update. Existing
   `UpdateAsync` already reprotects each supplied secret using the active
   provider, leaving no need for an automatic ciphertext migration.
7. In a separate IG protocol phase, update only the gateway session request to
   `Version: 2` and an explicit JSON `Accept` header, then assert the existing
   header-token proof flow remains intact.

## Decisive Implications

* A presence-only test cannot prove credentials are usable. The current code
  allows unreadable rows to reach the IG HTTP request.
* `PlatformAuthenticationReconciler` is not the only place to fix the guard.
  `TriggerManualAuthRetryHandler` otherwise remains an outbound-auth bypass.
* The persistence schema can retain its current runtime-state fields because a
  fixed redacted remediation reason is already stored and projected.
* A full three-field re-save is supported by the current safe write behavior;
  a partial save intentionally retains omitted ciphertext and cannot repair an
  unreadable omitted secret.

## Evidence With Line References

### Permanent Remediation

* `src/TNC.Trading.Platform.Application/Configuration/IProtectedCredentialService.cs:3-15`
  defines the inward port. Its current `GetPresenceAsync` at line 5 and
  `GetCredentialsAsync` at line 7 split row presence from runtime decryption.
* `src/TNC.Trading.Platform.Application/Configuration/CredentialPresence.cs:5-11`
  carries only three row-presence flags and defines `IsComplete` solely from
  them. Extend this type with explicit redacted usability flags and make the
  login predicate require both presence and usability.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs:17-28`
  derives presence from credential-type rows without attempting decryption.
  Its `GetCredentialsAsync` at lines 49-75 catches `CryptographicException` at
  line 68 and maps it to an empty credential value. This is the direct
  false-complete mechanism.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs:31-47,80-105`
  updates only nonblank supplied values, and reprotects each with the active
  provider. A complete three-field update is already the safe re-save path.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:80-121`
  invokes that update before saving and records the redacted three-field
  `SecretsUpdated` audit metadata at lines 109-114. `MapAsync` obtains the
  existing presence projection at line 181.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:52-54`
  uses `Credentials.IsComplete` to decide whether authentication may start.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:224-233`
  guards immediate recovery, logs the auth attempt at line 228, and calls the
  broker at line 230 only when that predicate is true. The same unguarded
  active transition is at lines 385-395, so the shared predicate fixes both.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:475-534`
  already persists a degraded state and fixed, secret-free missing-credential
  reason. Use this branch or an adjacent helper with wording that distinguishes
  unusable ciphertext from never-entered credentials.
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:54-67`
  commits a degraded retry intent then calls the gateway at line 65 without a
  readiness guard. It must use the same predicate before that call.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs:11-25`
  and `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/AuthStateResponse.cs:3-6`
  already persist and publish a redacted blocked reason.

### Configuration Projection

* `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/CredentialPresenceResponse.cs:3-6`
  and `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationMapping.cs:30-34`
  are the GET response shape and mapping.
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatedCredentialPresenceResponse.cs:3-6`
  and `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:54-58`
  are the save-response equivalents.
* `src/TNC.Trading.Platform.Web/CredentialPresenceViewModel.cs:3-6`,
  `src/TNC.Trading.Platform.Web/PlatformConfigurationViewModel.cs:3-11`, and
  `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModel.cs:39-41`
  mirror the old three booleans. `ConfigurationFormModelMapper.cs:39-40` resets
  the credential update model, preserving the no-secret-round-trip behavior.

### Separate IG Protocol Phase

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:35-45`
  creates `POST session`, sets `Version: 3` at line 37, sets JSON content, and
  has no explicit `Accept` header.
* The same adapter consumes CST and `X-SECURITY-TOKEN` at lines 55-59, then
  uses them for `/accounts` v1 at lines 117-119 and `/positions` v2 at lines
  129-131. Keep the request model internally consistent by changing only the
  session request to v2 plus explicit JSON `Accept` in the follow-up phase.

## Relevant Test Surfaces

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ProtectedCredentialServiceTests.cs:16-42`
  proves unavailable protection keys currently return empty secrets. Add a
  companion assertion that rows remain present but the new redacted usability
  state is false. Lines 47-73 and 80-108 verify secure initial save and full
  rotation; retain them and add a full re-save from unreadable rows.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:436-469`
  proves a readable complete set can become active. Add the permanent-regression
  test near the existing missing-credentials branch at lines 300-325: seed one
  invalid ciphertext and two readable credentials, then assert degraded
  remediation, no `AuthAttempted` event, and gateway call count zero.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs:69-90`
  is the focused manual-retry success test. Add an eligible-but-unusable
  configuration scenario and assert the fake gateway call count remains zero.
  Its configuration factory currently constructs complete presence at line 145.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:18-36`
  is the focused protocol test. Add a request inspection assertion for session
  `Version: 2`, `Accept: application/json; charset=UTF-8`, JSON content, and
  unchanged CST/header-token proof calls. The fake credential service begins at
  line 235 and will need any port-contract change.
* There are eleven current `new CredentialPresence(...)` call sites across
  product/test code. Adding non-optional positional parameters would create
  broad churn. Prefer named/defaulted additions, a static factory, or a small
  constructor-preserving design unless an intentional contract-wide update is
  accepted.

## Validation Commands

Run the narrow tests immediately after each phase, then the containing project,
and finally the solution:

```powershell
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ProtectedCredentialServiceTests|FullyQualifiedName~AuthRetryCycleTests"
```

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~TriggerManualAuthRetryHandlerTests"
```

```powershell
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~IgBrokerAuthenticationGatewayTests"
```

```powershell
dotnet build TNC.Trading.Platform.slnx --no-restore --nologo -v:minimal
dotnet test TNC.Trading.Platform.slnx --no-restore --nologo -v:minimal
```

`global.json` requires SDK `10.0.200`, and both focused test projects target
`net10.0`. The current terminal context records a prior solution build failure,
but its diagnostic output was not available in this research task; re-run the
final build without suppressing its output before treating that as a remediation
regression.

## Remaining Uncertainties

* Product decision required: expose three individual `Is*Usable` flags, one
  aggregate `RequiresCredentialReentry` flag, or both. The selected research
  favors both per-field diagnostic booleans and an aggregate remediation flag,
  all without secret data.
* Product decision required: manual retry can reject before writing a retry
  intent, or accept and persist the existing degraded remediation outcome. The
  current handler writes the intent before its outbound call, so preserving that
  audit sequence is the lower-risk behavior.
* Confirm the exact IG session v2 request fields against the authoritative IG
  reference before implementation. The selected v2 plus `Accept` direction is
  intentionally scoped as a protocol follow-up, not evidence that it caused
  this refactor regression.