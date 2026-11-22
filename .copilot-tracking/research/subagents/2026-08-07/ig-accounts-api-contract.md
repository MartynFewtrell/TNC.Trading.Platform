---
title: IG Accounts API Contract Research
description: Research findings for the IG authenticated accounts endpoint and its minimal local extension point.
ms.date: 2026-08-07
ms.topic: reference
---

## Status

Complete. No product code was modified.

## Scope And Evidence Classification

This research covers `GET https://demo-api.ig.com/gateway/deal/accounts` after
an IG login, plus the current local implementation.

* Official documentation means content retrieved from `labs.ig.com` on
	2026-08-07.
* Local evidence means the current workspace source and tests.
* No user-supplied JSON was present in the delegated request or workspace
	search results. The C# proposal therefore maps the official schema, not an
	unprovided payload. The local test JSON is an implementation fixture, not
	user-supplied or official schema evidence.

## Official Request Contract

The official `/accounts` reference identifies the operation as `GET /accounts`
at version `1` and says it returns the logged-in client's accounts. The REST
guide describes the request envelope.

```text
GET https://demo-api.ig.com/gateway/deal/accounts
X-IG-API-KEY: <application-api-key>
CST: <client-session-token from POST /session v1 or v2>
X-SECURITY-TOKEN: <account-session-token from POST /session v1 or v2>
Accept: application/json; charset=UTF-8
Version: 1
```

* `X-IG-API-KEY` identifies and authorises the calling application.
* A `POST /session` v1 or v2 response returns `CST` and
	`X-SECURITY-TOKEN`; both must be passed on later API calls.
* The guide states that `Content-Type: application/json` should be used for
	requests. For this body-less `GET`, no request content is sent; adding that
	header is harmless but not necessary to serialize a body.
* The guide says `Version` defaults to `1`, but it should be explicit to pin
	the contract.
* The alternative v3 login uses `Authorization: Bearer <access-token>` and
	`IG-ACCOUNT-ID`, rather than `CST` and `X-SECURITY-TOKEN`. Do not combine
	the two authentication schemes.

The guide says v1/v2 session tokens are initially valid for six hours and may
be extended while active, up to 72 hours. Its FAQ separately says each request
within 12 hours resets the token expiry and that weekend maintenance can
invalidate tokens. Treat either documented duration as non-contractual
operational guidance: handle `401` by re-authenticating rather than relying on
a local expiration calculation.

Sources: [accounts reference](https://labs.ig.com/reference/accounts.html),
[REST guide](https://labs.ig.com/rest-trading-api-guide.html), and
[FAQ](https://labs.ig.com/faq.html).

## Official Response Contract

The official reference documents a top-level `accounts` collection. Each
account has the following documented fields and wire types.

| JSON path | IG documented type | Notes |
| --- | --- | --- |
| `accounts[].accountAlias` | `String` | Account alias |
| `accounts[].accountId` | `String` | Account identifier |
| `accounts[].accountName` | `String` | Account name |
| `accounts[].accountType` | `Constant` | `CFD`, `PHYSICAL`, or `SPREADBET` |
| `accounts[].balance` | `Object` | Balance object |
| `accounts[].balance.available` | `Number` | Amount available for trading |
| `accounts[].balance.balance` | `Number` | Balance of funds |
| `accounts[].balance.deposit` | `Number` | Minimum margin deposit |
| `accounts[].balance.profitLoss` | `Number` | Profit and loss |
| `accounts[].canTransferFrom` | `Boolean` | Transfer capability flag |
| `accounts[].canTransferTo` | `Boolean` | Transfer capability flag |
| `accounts[].currency` | `String` | Account currency |
| `accounts[].preferred` | `Boolean` | Default login account flag |
| `accounts[].status` | `Constant` | The reference lists `DISABLED`, `ENABLED`, and `SUSPENDED` |

IG's prose descriptions for `canTransferFrom` and `canTransferTo` appear
reversed in the rendered reference page. Retain the wire names and verify their
business meaning using the configured IG test account before driving any transfer workflow.

The reference provides no indication that fields are required in every
successful payload. An external DTO should therefore use nullable properties,
validate only the fields needed by the consuming operation, and preserve an
unknown account type/status as a string until a business requirement warrants a
closed enum.

Source: [accounts reference](https://labs.ig.com/reference/accounts.html).

## Errors, Limits, And Caching

IG's generic error body is JSON shaped as `{ "errorCode": "..." }`. The
response can also include `X-REQUEST-ID`, which identifies the request.

Relevant documented `/accounts` errors include:

* `400` request or validation errors
* `401` `error.security.client-token-missing` or
	`error.security.client-token-invalid` for `CST`, and
	`error.security.account-token-missing` or
	`error.security.account-token-invalid` for `X-SECURITY-TOKEN`
* `401` OAuth-token errors when the OAuth path is used
* `403` disabled or invalid API-key errors and
	`endpoint.unavailable.for.api-key`
* `403` `error.public-api.exceeded-account-allowance` and
	`error.public-api.exceeded-api-key-allowance`
* `404` endpoint/resource errors and `500` provider failures

IG's FAQ lists default limits of 60 non-trading calls per minute per app and
30 non-trading calls per minute per account. The accounts endpoint is a
non-trading read. It should not be polled at an intraday cadence: collect it on
successful login or deliberate account refresh, coalesce concurrent callers,
and cache the derived account snapshot with an explicit local freshness policy.
No IG documentation retrieved for this task defines an HTTP cache-control,
ETag, or accounts-specific cache lifetime. Do not assume a server cache
validator exists; use a short, bounded application cache only if a feature
needs repeat reads, invalidate it after an account-changing operation, and
honour the rate-limit outcome with backoff.

Sources: [REST guide](https://labs.ig.com/rest-trading-api-guide.html),
[accounts reference](https://labs.ig.com/reference/accounts.html), and
[FAQ](https://labs.ig.com/faq.html).

## IG Test And Live Environments

Official IG guidance gives these base URLs:

* IG test environment: `https://demo-api.ig.com/gateway/deal`
* Live: `https://api.ig.com/gateway/deal`

The route, version, and header scheme are the same endpoint contract. The
important operational differences are environment-specific credentials,
accounts, API access, and risk. A test-account token/API key must not be assumed valid
against Live. The platform currently intentionally rejects Live before issuing
HTTP, and its typed client is permanently configured for the IG test environment. Any Live support
requires environment-selected base-address configuration, isolated credential
retrieval, and explicit safeguards; it must never route a Live request through
the IG test-environment client.

The `demo-api` hostname identifies IG's test environment. Account data returned
for the configured test account is real test-account data, not synthetic data.

Official source: [FAQ](https://labs.ig.com/faq.html).

Local evidence: `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs:L3-L7`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L23-L28`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:L91-L94`;
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:L18-L35`
and `:L91-L105`.

## Local Implementation And Minimal Extension Point

The narrow integration path is already implemented:

1. `IgBrokerAuthenticationGateway` posts `session` at version 2 with the API
	 key and reads the `CST` and `X-SECURITY-TOKEN` response headers.
2. Its private `TryCollectProofAsync` calls `accounts` with version 1 and the
	 three required headers, then calls `positions`.
3. It chooses the preferred account, falling back to the first account, and
	 projects only name, ID, and `balance.balance` into `BrokerAuthenticationProof`.
4. A failed proof request returns `null` without invalidating successful login
	 evidence.

The minimal extension point for a richer accounts contract is the private
`IgAccountsResponseBody`, `IgAccountRaw`, and `IgAccountBalanceRaw` records in
the same Infrastructure gateway. Add fields there and selectively project them
only when an inward-owned use case needs them. Do not pass IG DTOs across the
`IBrokerAuthenticationGateway` port. A new use case that lists accounts should
own a narrow port in Application and have this adapter implement it; a change
limited to login proof data does not need a second HTTP client or token store.

Local evidence:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L19-L108` handles login and proof collection.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L113-L161` constructs the accounts call, deserializes it, and applies the preferred-account fallback.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L163-L175` owns common session headers.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L178-L218` reads allowlisted diagnostics and translates primary login failures.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L222-L230` is the existing narrow DTO boundary.
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/IBrokerAuthenticationGateway.cs:L3-L8` is the current inward port.
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationProof.cs:L3-L8` exposes only safe proof fields.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:L65-L89` proves headers and endpoint versions, while `:L286-L302` proves proof failure remains non-fatal.

## Proposed C# Wire Mapping

This is a proposal derived from official schema types. It is not validated
against user-supplied JSON, because none was provided. It deliberately uses
nullable fields at the provider boundary and leaves provider constants as
strings so a future IG value cannot make a successful response fail to
deserialize.

```csharp
private sealed record IgAccountsResponseBody(
		[property: JsonPropertyName("accounts")] List<IgAccountRaw>? Accounts);

private sealed record IgAccountRaw(
		[property: JsonPropertyName("accountAlias")] string? AccountAlias,
		[property: JsonPropertyName("accountId")] string? AccountId,
		[property: JsonPropertyName("accountName")] string? AccountName,
		[property: JsonPropertyName("accountType")] string? AccountType,
		[property: JsonPropertyName("balance")] IgAccountBalanceRaw? Balance,
		[property: JsonPropertyName("canTransferFrom")] bool? CanTransferFrom,
		[property: JsonPropertyName("canTransferTo")] bool? CanTransferTo,
		[property: JsonPropertyName("currency")] string? Currency,
		[property: JsonPropertyName("preferred")] bool? Preferred,
		[property: JsonPropertyName("status")] string? Status);

private sealed record IgAccountBalanceRaw(
		[property: JsonPropertyName("available")] decimal? Available,
		[property: JsonPropertyName("balance")] decimal? Balance,
		[property: JsonPropertyName("deposit")] decimal? Deposit,
		[property: JsonPropertyName("profitLoss")] decimal? ProfitLoss);
```

With the existing case-insensitive JSON options, the attributes are optional
for current field matching but make the external wire contract explicit.
`decimal?` is appropriate for IG `Number` money values when currency precision
must be preserved. Before using a field to make a decision, validate presence
and map to a small inward-owned record. Continue to catch `JsonException`,
avoid logging response bodies or tokens, and use the existing bounded
`errorCode` plus `X-REQUEST-ID` diagnostic approach. If `/accounts` becomes a
required operation rather than optional proof, do not reuse the current
`null`-on-error branch: translate `401`, allowance errors, malformed JSON,
and transport failures into explicit typed outcomes.

The current local fixture only validates
`accountId`, `accountName`, `preferred`, and `balance.balance`:
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:L315-L326`.

## Recommended Tests

* Add a gateway unit test with every documented field and assert that money,
	booleans, strings, and unknown constant strings deserialize correctly.
* Add null/missing-field cases proving the preferred-account fallback does not
	throw and that only the required proof fields determine whether proof is
	available.
* Add malformed JSON and non-success accounts tests. Preserve the existing
	best-effort login behavior unless a new use case deliberately changes it.
* Add `401` tests for invalid/missing CST and account tokens. Confirm a caller
	can trigger re-authentication without logging either token.
* Add `403` allowance tests with bounded retry/backoff policy tests. Do not use
	live or real IG test-account network calls in unit tests.
* If a future account-list feature supports Live, add composition tests that
	prove the IG test and Live environments select different base URLs and never share credentials or
	cached snapshots.

## Risks And Unanswered Gaps

* No user-supplied `/accounts` JSON was available. Validate the proposed DTO
	against an anonymized real IG test-account payload before treating every field as
	observed in this environment.
* IG's token-expiry statements differ between its REST guide and FAQ. Re-login
	on authentication failure rather than scheduling from either duration.
* The transfer-flag prose appears reversed in IG's reference. Do not infer
	transfer direction without an IG support confirmation or controlled IG test-account
	observation.
* The current accounts request targets the IG test environment and is best effort. It suppresses
	proof-call failure, which is correct for login evidence but unsuitable for a
	future account-list operation that needs user-visible error semantics.
* No endpoint-specific IG caching contract was found. Any local TTL requires a
	product decision about acceptable account-balance staleness.
