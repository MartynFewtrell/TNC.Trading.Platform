---
title: IG Login Projection and Diagnostics Test Research
description: Research record for configuration credential projections and secret-safe IG 403 diagnostics.
ms.date: 2026-08-06
ms.topic: reference
---

## Research Scope

* Identify implementation and test surfaces for credential usability response projections in `GetPlatformConfigurationMapping` and `UpdatePlatformConfigurationMapping`
* Identify implementation, contracts, observability, persistence, and narrow test seams for secret-safe allowlisted IG 403 diagnostics

## Findings

### Credential Usability Response Projections

The current application contract exposes only row-presence facts. `CredentialPresence`
has `HasApiKey`, `HasIdentifier`, and `HasPassword`, with `IsComplete` derived
solely from those three properties. It has no usability or re-entry state:

* `src/TNC.Trading.Platform.Application/Configuration/CredentialPresence.cs:5-11`
* `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs:3-13`

Both API mappers construct three-property credential response records. These
are the only HTTP projection points for the promoted credential-usability
state:

* `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/CredentialPresenceResponse.cs:3-6`
* `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationMapping.cs:30-34`
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatedCredentialPresenceResponse.cs:3-6`
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:54-58`

The parent API responses already embed these records at their `Credentials`
properties:

* `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationResponse.cs:3-11`
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationResponse.cs:3-11`

Recommended minimum response shape after the application-owned credential
contract is expanded:

* Retain `HasApiKey`, `HasIdentifier`, and `HasPassword` as storage facts.
* Add only Boolean values: `IsApiKeyUsable`, `IsIdentifierUsable`,
	`IsPasswordUsable`, `IsAuthenticationReady`, and
	`RequiresCredentialReentry`.
* Map every new value directly from `configuration.Credentials` in the GET
	mapper and from `response.Result.Snapshot.Credentials` in the update mapper.
* Do not derive usability in API. Decryption and policy remain in Application
	and Infrastructure.

The API unit-test project has `InternalsVisibleTo` access and has no existing
configuration-mapping test. Its nearest style reference is a small direct
record test, rather than endpoint or JSON integration coverage:

* `src/TNC.Trading.Platform.Api/Properties/InternalsVisibleTo.cs:3`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/GetPlatformStatusMappingTests.cs:5-55`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:16-18`

Best new test files:

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/GetPlatformConfigurationMappingTests.cs`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/UpdatePlatformConfigurationMappingTests.cs`

Each test file should construct its corresponding application response around a
`PlatformConfigurationSnapshot`, run the internal `ToResponse` extension, and
assert every presence and usability Boolean for both an all-usable and a
re-entry-required case. Add a reflection-based property-name allowlist for the
nested credential response. This is a cheap regression check that falsifies an
accidental secret/ciphertext/error/key property before it can be serialized.
The update-mapping case must keep empty secret-update request behavior out of
scope because `ToResponse` receives a completed application result, not the
incoming secret model.

### IG 403 Diagnostics

The checked-in IG adapter posts the session request and maps all non-success
statuses through `MapFailure`. A session `403` becomes a `Forbidden` failure
with a fixed summary; the response body and headers are currently discarded:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:35-49`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:174-183`

The inward result is intentionally provider-neutral and currently has only a
kind and summary:

* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationFailure.cs:3-5`
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationFailureKind.cs:3-13`
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationOutcome.cs:3-16`
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/PlatformAuthentication/BrokerAuthenticationOutcomeTests.cs:8-54`

Recommended minimum behavior:

1. Add an optional, provider-neutral fixed-shape diagnostic value to
	 `BrokerAuthenticationFailure`, preserving compatibility for existing
	 failure construction. Its only fields should be `ErrorCode` and `RequestId`.
2. In `IgBrokerAuthenticationGateway`, populate that value only when the
	 session status is `HttpStatusCode.Forbidden`. Parse only JSON
	 `errorCode` from the body and read only the `X-REQUEST-ID` response header.
	 Missing, malformed, oversized, or non-string values become `null`; they do
	 not cause a second failure path.
3. Keep the existing generic summary, `Forbidden` failure kind, retry behavior,
	 and HTTP outcome unchanged. Do not append diagnostic values to the summary.
4. Carry the optional diagnostic object as `FailureDetected` event details for
	 both automatic reconciliation and manual retry. Do not add it to
	 `LatestFailureSummary`, `BlockedReason`, status responses, notifications, or
	 IG login snapshots.

This follows the incident research requirement to retain only provider
`errorCode` and `X-REQUEST-ID`, while avoiding a provider DTO or raw HTTP
surface in Application. The existing port-boundary test rejects token, header,
and HTTP-shaped properties on the outcome, so any test change must preserve the
provider-neutral result and assert the new fixed diagnostic type separately.

### Current Observability And Persistence Path

The gateway itself has no logger. Outbound `HttpClient` metrics and traces are
enabled by Service Defaults, so the existing telemetry path can identify the
outbound operation without application code adding request/response logging:

* `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:102-129`

Automatic reconciliation records `AuthAttempted`, calls the gateway, and turns
the failure summary into degraded runtime state. Its failure event currently
contains only `RetryCycleId`, and its notification receives only the summary:

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:385-396`
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:536-570`
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconcilerSideEffects.cs:46-78`

Manual retry independently calls the gateway and commits a `FailureDetected`
event. It is a second persistence surface and must receive the same optional
event-details mapping:

* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:65-83`
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:146-162`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/ManualAuthRetryCommitter.cs:15-52`

Operational events serialize their `Details` value through the redactor before
writing `DetailsJson`. Runtime state has a 512-character summary column and is
projected to status, which is why diagnostics should not be put in summaries:

* `src/TNC.Trading.Platform.Application/Configuration/PlatformEventRecord.cs:3-13`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformEventStore.cs:38-54`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalDataRedactor.cs:12-42,118-119`
* `src/TNC.Trading.Platform.Application/Services/OperationalDataSensitivityPolicy.cs:3-28`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:61`

The current redactor is denylist-oriented. `ErrorCode` and `RequestId` are not
classified as sensitive, so it is not sufficient to make body/header capture
safe. The gateway extraction must be an explicit allowlist before the data
crosses into Application or persistence.

### Narrow Test Seams

Gateway extraction and allowlisting belong in the existing deterministic
`HttpMessageHandler` tests. Add a `403` response with a JSON body containing
`errorCode`, `X-REQUEST-ID`, credentials, CST, security token, authorization,
and unrelated headers. Assert that only the two intended values reach the
failure diagnostic and that the failure summary does not include any supplied
value:

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:82-103`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:156-232`

Add companion cases for missing/invalid JSON and a non-403 response with the
same body/header. Both must produce no diagnostic. This discriminates the
required `403`-only behavior from accidentally general response capture.

The automatic persistence seam is already an in-memory EF test that supplies a
fake gateway and inspects runtime state and stored events. Extend the nearby
unauthorized scenario or add a `Forbidden` scenario that returns a failure with
the new diagnostic. Assert the two allowlisted values in the `FailureDetected`
event `DetailsJson`, the generic summary in runtime state, and absence of every
sentinel secret/token/header value:

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:467-502`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:550-580`

The manual-retry handler uses a fake committer, so it can prove that its second
`FailureDetected` intent carries the same two-key diagnostic detail without SQL
or network setup:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs:69-90`
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs:165-209`

Add a small port-contract test to
`BrokerAuthenticationOutcomeTests.cs` that asserts the diagnostic record has
exactly `ErrorCode` and `RequestId`. It protects the Application boundary from
later expansion into arbitrary provider metadata.

## Recommended Validation

Run the narrow tests immediately after the corresponding implementation slice:

```powershell
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~GetPlatformConfigurationMappingTests|FullyQualifiedName~UpdatePlatformConfigurationMappingTests"
```

```powershell
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~IgBrokerAuthenticationGatewayTests|FullyQualifiedName~AuthRetryCycleTests"
```

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BrokerAuthenticationOutcomeTests|FullyQualifiedName~TriggerManualAuthRetryHandlerTests"
```

After focused tests pass, validate the affected production projects:

```powershell
dotnet build src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --no-restore --nologo -v:minimal
dotnet build src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj --no-restore --nologo -v:minimal
dotnet build src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --no-restore --nologo -v:minimal
```

## Security Constraints

* Permit only the JSON property `errorCode` and header `X-REQUEST-ID` from an
	IG session 403 response. Do not retain raw content, other headers, header
	collections, HTTP request/response instances, exception text, or provider
	DTOs.
* Never log or persist request bodies, API keys, identifiers, passwords, CST,
	`X-SECURITY-TOKEN`, bearer/OAuth tokens, `Authorization`, ciphertext,
	Data Protection key identifiers, or decryption exceptions.
* Keep diagnostics off operator-facing summaries, status payloads, and
	notifications. Persist only as typed `FailureDetected` event details and
	guard that serialization with sentinel-value assertions.
* Do not rely on `OperationalDataRedactor` to sanitize a response captured in
	bulk. Its denylist cannot establish the required two-field allowlist.
* Preserve cancellation behavior and do not add a retry, second provider call,
	or diagnostic parsing exception path.

## Follow-On Questions

* Confirm the maximum accepted length for `errorCode` and `X-REQUEST-ID` before
	implementation. The current evidence establishes the two fields, not a
	provider-documented size limit. A conservative local bound should be chosen
	and tested if no authoritative limit is available.

## Sources

* .copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md
* .copilot-tracking/research/2026-08-06/ig-login-403-degraded-health-research.md
* .copilot-tracking/research/subagents/2026-08-06/ig-login-implementation-surface-research.md
* Current source and tests cited in the findings sections
