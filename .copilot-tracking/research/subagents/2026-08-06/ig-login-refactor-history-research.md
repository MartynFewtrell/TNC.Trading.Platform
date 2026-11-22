---
title: IG Login Refactor History Research
description: Evidence-led investigation of refactor-related causes for IG 403 responses after correct credentials are saved
ms.date: 2026-08-06
ms.topic: troubleshooting
keywords:
  - IG
  - credentials
  - refactoring
  - HTTP client
  - health
---

## Research Scope

Investigate recent Git history, diffs, blame, and automated tests around IG login,
credential persistence, HTTP client setup, configuration, and health degradation. Identify
the most plausible refactor-induced behavior change that can produce an IG HTTP 403 after
correct credentials are saved.

## Initial Evidence

* The active API composition calls `AddServiceDefaults`, `AddPlatformDataProtection`,
  `AddPlatformInfrastructure`, startup initialization, then initial reconciliation:
  `src/TNC.Trading.Platform.Api/Program.cs` lines 16-36.
* The infrastructure registration creates the typed IG gateway client with base address
  `https://demo-api.ig.com/gateway/deal/`: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs`
  lines 91-94.
* The final gateway POSTs `session` with `X-IG-API-KEY`, `Version: 3`, and JSON
  `identifier`, `password`, and `encryptedPassword: false`:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
  lines 32-48.
* `8df68d8880b2af3015722095ebbad30e1908a71c` (2026-07-28, "Refactor Solution
  towards a Clean Architecture") deleted the old `IgSessionClient`, introduced
  `IgBrokerAuthenticationGateway`, and introduced the shared Data Protection key-ring
  migration. Its direct parent shows the old client constructed the same session URL,
  headers, and JSON body. The primary session request itself did not change.
* Global HTTP-client resilience and service discovery were introduced in the initial
  scaffold commit `abe477a343ed59a4c9d370165d25d0ee775c4b8b` (2026-03-15), not by
  the July refactor: `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs`
  lines 42-49. They are therefore not the most likely refactor-induced source.

## Findings

### Credential Persistence And Read Behavior

* Credential save is an upsert keyed by broker environment and credential type. Empty
  inputs deliberately leave the existing protected row untouched:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs`
  lines 30-47 and 80-102.
* Configuration save writes secret fields, persists the shared DbContext, then the
  application handler immediately invokes reconciliation:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs`
  lines 53-112; `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs`
  lines 10-18.
* Credential presence reports only that protected rows exist. It does not establish that
  their values can be decrypted: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs`
  lines 17-28.
* Before `8df68d88`, decrypting ciphertext with an unavailable key threw from
  `IDataProtector.Unprotect`. The refactor changed this at current lines 56-77: it catches
  `CryptographicException` for each row and returns an empty API key, identifier, or
  password. This was added alongside the new SQL-backed shared key ring:
  `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs` lines 61-87.
* The current regression test explicitly proves this changed contract: unavailable-key
  ciphertext yields all-empty runtime credentials, not a startup failure:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ProtectedCredentialServiceTests.cs`
  lines 10-37.

### IG Request And Degradation Behavior

* The replacement gateway obtains credentials from the protected store, sends the session
  request, and maps a non-success session response. HTTP 403 becomes the safe summary
  "IG authentication failed: access forbidden.":
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
  lines 30-48 and 174-182.
* A 403 from the later accounts or positions proof calls is intentionally non-fatal and
  returns a successful authentication without proof data:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
  lines 117-156. The focused test verifies this behavior at
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs`
  lines 139-155. Therefore, the observed degraded state identifies the session POST, not
  a proof request, as the 403 source.
* The reconciler converts every failed gateway outcome into a degraded runtime state and
  persists the same failure summary. It does not fabricate a 403:
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs`
  lines 359-409 and 536-574.
* Current gateway tests assert status-to-failure mapping, including 403, but inject a fake
  credential service and do not inspect outbound request headers or JSON:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs`
  lines 74-98 and 218-257.
* Runtime retry tests prove that rejected credentials, timeouts, unreachable responses,
  and rate limiting cause degradation, but omit the `Forbidden` failure kind:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs`
  lines 472-652.

### UI And API Save Path

* The Web mapper initializes secret input fields empty on load and copies all entered
  fields into the update DTO without revealing stored values:
  `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModelMapper.cs`
  lines 5-44 and 46-79.
* The Web client PUTs that DTO directly to `/api/platform/configuration`:
  `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` lines 41-56.
* API mapping forwards the three credential properties unchanged to the application
  update: `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs`
  lines 8-34.
* Existing Web tests exercise load, generic save, restart messaging, and error retention,
  but do not enter secret values and verify the request body or a subsequent authenticating
  reconciliation: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs`
  lines 8-123.

### Candidate Commits And Diff Assessment

| Commit | Date | Verified relevance |
|--------|------|--------------------|
| `8df68d8880b2af3015722095ebbad30e1908a71c` | 2026-07-28 | Replaced `IgSessionClient` with `IgBrokerAuthenticationGateway`; added SQL-backed shared Data Protection key-ring support; changed unavailable-key reads from exception to empty credentials |
| `0088651fec59f44722176cda2c01717f59db7d07` | 2026-05-30 | "Added login to actual IG test account"; introduced runtime persisted-credential reads used by the pre-refactor live IG path |
| `abe477a343ed59a4c9d370165d25d0ee775c4b8b` | 2026-03-15 | Added default HTTP resilience and service discovery; predates both the actual IG login implementation and the Clean Architecture refactor |

## Candidate Regression Hypothesis

The most plausible refactor-induced cause is **legacy or partially replaced ciphertext
being treated as complete credentials after the July shared Data Protection key-ring
migration**.

1. `8df68d88` changed an unavailable Data Protection key from a visible exception into
   three empty values returned to the IG gateway.
2. Presence remains `true` because it checks rows, not successful decryption. The
   scheduler therefore attempts IG session creation instead of reporting credentials as
   unavailable.
3. The gateway sends empty or mixed old/new values to `POST /session`; IG can respond
   403, which is correctly mapped to `Forbidden` and then to degraded health.
4. A configuration save can leave the condition in place when any credential field is
   omitted or whitespace, because `UpdateAsync` intentionally preserves the corresponding
   legacy row. This is compatible with write-only secret UI behavior.

The hypothesis is **strong for a post-refactor database containing legacy ciphertext or
an incomplete rotation**, but it is **not yet proven** for a save where all three secret
fields were newly supplied and accepted. A complete three-field overwrite in the running
API should protect all rows with the active shared key ring and disconfirm this hypothesis.

The gateway replacement itself is a lower-ranked candidate: the old and new session POST
request construction matches in method, relative URL, headers, and JSON fields. There is
no verified July change to HTTP-client defaults or health endpoints that can turn valid
credentials into a session 403.

## Follow-On Questions

* For the failing environment, did the save request contain non-empty values for API key,
  identifier, and password, and did it target the active `Demo` broker environment?
* Does `ProtectedCredentials` contain exactly one row for each of `ApiKey`, `Identifier`,
  and `Password` in that environment, with `UpdatedAtUtc` after the attempted repair?
* Can the running API decrypt all three values using its active SQL key ring? This should
  be checked without logging secret material, for example by recording only per-field
  decryptability and string length.
* Was the environment configured with a pre-`8df68d88` database/key storage state, or was
  the database recreated after the refactor?
* Does a test capture the real configuration-page save request and assert all three
  non-empty secret fields reach the API, then assert that a same-process protected-store
  read can decrypt each value?
