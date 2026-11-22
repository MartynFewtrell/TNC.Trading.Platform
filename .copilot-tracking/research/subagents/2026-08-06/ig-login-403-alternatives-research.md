---
title: IG Login 403 Repair Alternatives Research
description: Evidence-led evaluation of the smallest repair for refactor-related IG session 403 responses
ms.date: 2026-08-06
ms.topic: troubleshooting
keywords:
  - IG
  - HTTP 403
  - data protection
  - session authentication
  - refactoring
---

## Protocol Evidence

IG's [REST Trading API Guide](https://labs.ig.com/rest-trading-api-guide.html)
states that `Content-Type: application/json` and
`Accept: application/json; charset=UTF-8` should always be sent. It also states
that session v1 and v2 return `CST` and `X-SECURITY-TOKEN`, while v3 returns
OAuth access and refresh tokens that require `Authorization: Bearer` and
`IG-ACCOUNT-ID` on later calls. Error bodies provide an `errorCode`, and
`X-REQUEST-ID` identifies the request.

The [IG FAQ](https://labs.ig.com/faq.html) explicitly lists missing
`Content-Type` or `Accept`, malformed JSON, wrong-environment login details,
and an invalid or disabled API key as common login failures. It recommends
verifying the same login in API Companion. API Companion success therefore
supports, but does not prove, a platform request-shape or platform-runtime
cause.

Current code supplies JSON content via `JsonContent.Create` but adds no
`Accept` header and sends `Version: 3` while requiring header tokens:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
lines 34-44 and 54-72. The typed client remains Demo-only at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs`
lines 91-94. A Demo credential set must therefore be used.

## Runtime Discriminators

Collect one failing attempt using a redacted outbound diagnostic or controlled
proxy, correlated with the existing `auth/AuthAttempted` and
`auth/FailureDetected` events. Do not record API keys, identifiers, passwords,
CST, `X-SECURITY-TOKEN`, or OAuth tokens.

| Observation | Interpretation | Action selected |
| --- | --- | --- |
| A row cannot be decrypted by the active API, or save audit says a field was omitted | Confirms refactor-induced Data Protection path | Rotate all three secrets; block outbound IG login until decryptability is restored |
| All fields were submitted after migration and decrypt in the active API | Falsifies migration for this attempt | Inspect provider error code and repair protocol compatibility |
| Request lacks `Accept`, has v3, and IG reports a header/request-format error | Confirms protocol issue; refactor timing remains unexplained | Use v2 with explicit `Accept` while retaining header-token proof calls |
| Correct v2/header shape and decryptable values still yield 403 while API Companion works | Error code and request ID distinguish network, proxy, entitlement, or account policy | Escalate to IG with request ID; do not migrate ciphertext blindly |
| 403 is on `PUT /api/platform/configuration`, not IG `POST /session` | Operator policy denied platform API; no credentials were saved | Diagnose API authorization, not IG authentication |

The platform does not currently retain IG `errorCode`, `X-REQUEST-ID`, outbound
version, header presence, or decryptability state. The gateway maps only HTTP
status at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
lines 174-183. API middleware logs a distinct API-route 403 at
`src/TNC.Trading.Platform.Api/Program.cs` lines 49-68.

## Executive Conclusion

The most likely refactor-induced cause is unavailable or partially replaced
Data Protection ciphertext after the shared SQL key-ring migration, not the
missing `Accept` header or incompatible session-version protocol.

This conclusion has one cheap runtime discriminator: the active API must be
unable to decrypt at least one Demo credential row, or the save audit must show
that a secret field was omitted. The July refactor introduced both the shared
key ring and a new behavior that maps an unavailable key to an empty secret.
Credential presence still reports the row as present, so the platform can send
empty or mixed credentials to IG and map a session `403` to degraded state.

The missing `Accept` header and v3/header-token inconsistency are verified
protocol defects. However, the pre-refactor client sent the same request:
`POST session`, `X-IG-API-KEY`, `Version: 3`, the same JSON body, no `Accept`,
then expected `CST` and `X-SECURITY-TOKEN`. They cannot explain a failure that
started with this refactor unless IG changed server behavior at the same time.
They should be repaired separately as a compatibility correction.

The smallest targeted remediation for the reported regression is a complete
three-field credential rotation through the running API after verifying the
active shared key ring. The existing update path already reprotects every
nonblank submitted value. Do not attempt automatic decrypt-and-reencrypt
migration for ciphertext that the active key ring cannot unprotect; its
plaintext is unavailable by definition.

## Scope And Status

Status: Complete. Research only; product code, configuration, credentials, and
tests have not been changed.

Question evaluated: choose the smallest repair for an IG session `403` and
degraded platform status after saving credentials that succeed in IG API
Companion. The comparison covers Data Protection migration, request protocol,
and redacted diagnostics.

## Request Comparison

| Request property | Pre-refactor | Current | Finding |
| --- | --- | --- | --- |
| Base URL | Demo gateway registration | Demo gateway registration | No relevant change |
| Method and path | `POST session` | `POST session` | No change |
| API-key header | `X-IG-API-KEY` | `X-IG-API-KEY` | No change |
| Session version | `Version: 3` | `Version: 3` | No change; incompatible with later header-token use |
| JSON body | `identifier`, `password`, `encryptedPassword: false` | Same three properties | No change |
| `Content-Type` | `JsonContent.Create` supplies JSON content type | Same | No change |
| `Accept` | Not explicitly set | Not explicitly set | No change; IG requires it |
| Success handling | Reads `currentAccountId`, `CST`, `X-SECURITY-TOKEN` | Same required response shape | No change; v3 expects OAuth tokens |
| Follow-up calls | Accounts v1 and positions v2 use header tokens | Same | No change |

Historical evidence: parent of refactor commit
`8df68d8880b2af3015722095ebbad30e1908a71c` at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs`
lines 22-44 constructs the old login request, lines 48-62 expect header tokens,
and lines 87-135 use them for proof requests. The replacement uses the same
request at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
lines 30-47, requires the same response shape at lines 54-72, and creates
header-token proof requests at lines 100-173.

## Data Protection Causal Path

1. The save path writes only nonblank fields. A blank API key, identifier, or
  password preserves its existing protected row:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs`
  lines 31-47. The write audit reports which fields were supplied, but not
  their values:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs`
  lines 53-119.
2. The refactor configured a shared SQL-backed key ring:
  `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs` lines 66-87. The
  API invokes this before Infrastructure registration:
  `src/TNC.Trading.Platform.Api/Program.cs` lines 14-20.
3. Credential reads catch `CryptographicException` and map unreadable rows to
  empty strings:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs`
  lines 49-75. Presence checks only row types at lines 17-28; they do not
  prove decryptability.
4. The state engine tries login whenever presence says all credentials are
  complete:
  `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs`
  lines 31-56. A configuration command commits and immediately reconciles:
  `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs`
  lines 10-18.
5. The gateway sends those values unchanged. A session `403` maps to
  `Forbidden`, then to degraded state:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
  lines 30-47 and 174-183;
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs`
  lines 371-396 and 536-572.

The focused regression test proves silent empty values for unavailable
ciphertext:
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ProtectedCredentialServiceTests.cs`
lines 10-37. The same test proves a complete current-process overwrite safely
reprotects all three values at lines 75-110.

## Ranked Alternatives

### 1. Conditional full credential rotation with key-ring verification

Recommendation: selected for the refactor regression when the decryptability
discriminator is positive. This is the smallest effective repair because the
existing save path already reencrypts each supplied nonblank field with the
active key ring. The operator must submit all three Demo credentials in one
save. A blank form field deliberately retains previous ciphertext, so a partial
save is not a migration.

Implementation impact if a permanent product change is authorized:

* Update `ProtectedCredentialService.cs` to expose only a redacted per-field
  decryptability result, or make `GetPresenceAsync` report usable rather than
  merely existing credentials.
* Update the configuration/reconciliation path to classify unreadable
  credentials as configuration remediation required before issuing an IG
  request. Relevant files are `SqlPlatformConfigurationStore.cs`,
  `PlatformStateTransitionEngine.cs`, and
  `PlatformAuthenticationReconciler.cs`.
* Do not add a background automatic re-encryption job unless the old key ring
  remains available. An unprotect failure cannot be repaired automatically
  without plaintext.

Focused tests:

* Seed one legacy unreadable row and two readable rows; assert the platform
  reports a remediation state and makes no session request.
* Verify a three-field save using the active provider makes all fields usable,
  preserves three rows, and a fresh provider sharing the SQL key ring decrypts
  them.
* Verify a partial save leaves an unreadable omitted row unusable and does not
  report a false complete credential set.

Risks: forcing a full resave asks the operator to reenter secrets; rotating an
API key may have external account consequences; changing presence semantics may
alter displayed configuration state. The change must preserve redaction and
must not emit values, ciphertext, or key IDs.

### 2. Explicit Accept plus session v2 header-token compatibility

Recommendation: required protocol correction, but not selected as the
root-cause repair for the reported refactor timing. Use
`Accept: application/json; charset=UTF-8` on session and proof calls, then
change the session version from v3 to v2 if the platform continues to require
`CST` and `X-SECURITY-TOKEN`. This preserves the header-token proof design. A
v3 OAuth implementation would be materially larger because it needs token
parsing, refresh, `Authorization`, and `IG-ACCOUNT-ID` propagation.

Implementation impact:

* Modify only
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
  for v2 and `Accept` request construction.
* Extend
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs`
  to inspect the captured session request's API-key-header presence, `Accept`,
  content type, version, JSON property names, and proof-request header set.

Focused tests:

* Assert the session request has `Accept: application/json; charset=UTF-8`,
  `Content-Type: application/json`, and `Version: 2`.
* Assert only header-token-compatible success responses proceed to `accounts`
  and `positions`.
* Keep 401/403/429 mapping tests and add no secret-bearing test output.

Risks: changing session version can expose provider-version-specific differences
and requires a controlled Demo verification. Applying only `Accept` is smaller
but leaves the documented v3 protocol inconsistency unresolved.

### 3. Redacted diagnostics only

Recommendation: a diagnostic prerequisite when direct runtime evidence cannot
be collected, not a repair. Add a narrow handler or gateway result that records
only the environment, URL host/path, method, header names and safe values
(`Version`, content type, `Accept`), HTTP status, provider `errorCode`,
`X-REQUEST-ID`, response token-shape flags, and per-field decryptability
booleans.

Implementation impact:

* Modify `IgBrokerAuthenticationGateway.cs` to parse only the known
  `errorCode` field from a non-success response and retain `X-REQUEST-ID`.
* Modify the authentication operational-event path in
  `PlatformAuthenticationReconciler.cs` and its side-effect collaborator to
  persist the sanitized diagnostic against the existing correlation ID.
* Add redaction-focused assertions in gateway and reconciliation tests.

Focused tests:

* A synthetic 403 with `{ "errorCode": "example" }` and `X-REQUEST-ID`
  records those values but not arbitrary response text.
* Assert diagnostics never contain credential fields, their values, session
  tokens, OAuth fields, or ciphertext.

Risks: a diagnostic payload can become a secret leak if it records arbitrary
headers or bodies. Restrict it to an allowlist and retain it according to the
operational-event policy.

## Recommended Execution Order

1. Confirm the failed 403 is the IG session request rather than the protected
   platform configuration endpoint.
2. Inspect the configuration audit's `SecretsUpdated` flags and run a
   secret-safe per-field decryptability check in the active API process.
3. If any field is unreadable or was omitted, verify the SQL key-ring
   migration, then submit all three Demo secrets in one save. This is the
   selected smallest remediation for the refactor regression.
4. If all fields decrypt, collect IG `errorCode` and `X-REQUEST-ID`; then
   implement the v2 plus `Accept` protocol correction and verify against Demo.
5. Add redacted diagnostics only if existing tracing/provider evidence cannot
   provide these discriminators, or as a separately approved operability
   improvement.

## Questions And Blockers

* Did the failing save send all three nonblank Demo secret fields, and does its
  audit record show all `SecretsUpdated` values as true?
* Does the active API process decrypt all three Demo rows after that save?
* Is the 403 from `PUT /api/platform/configuration` or from
  `POST https://demo-api.ig.com/gateway/deal/session`?
* What redacted IG `errorCode`, `X-REQUEST-ID`, request `Version`, and
  `Accept` value were returned or sent for the failing attempt?
* Is the intended durable protocol header-token v2 or OAuth v3? The smallest
  compatible correction assumes v2 because proof calls require CST and
  `X-SECURITY-TOKEN`.

## Related Research

* `.copilot-tracking/research/subagents/2026-08-06/ig-login-flow-research.md`
* `.copilot-tracking/research/subagents/2026-08-06/ig-login-refactor-history-research.md`
* `.copilot-tracking/research/subagents/2026-08-06/ig-api-auth-requirements-research.md`