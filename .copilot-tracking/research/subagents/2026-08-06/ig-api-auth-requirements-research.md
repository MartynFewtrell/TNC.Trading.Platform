---
title: IG API Authentication Requirements Research
description: Official IG Labs session-authentication requirements correlated with the platform request configuration
ms.date: 2026-08-06
ms.topic: troubleshooting
keywords:
  - IG Labs
  - REST API
  - authentication
  - session
  - 403
---

## Research Scope

* Verify the official IG Labs requirements for the session-authentication
  endpoint used by the platform.
* Correlate those requirements with the discoverable application request
  configuration.
* Identify evidence required to diagnose a `403` without disclosing credentials
  or session tokens.

## Status

Complete for static repository inspection and public official IG Labs guidance.
No credentials, runtime request capture, or real IG request was used.

## Official IG Labs Requirements

### Environment and credential pairing

IG Labs publishes two REST gateway base URLs:

| Environment | Base URL |
|-------------|----------|
| Demo        | `https://demo-api.ig.com/gateway/deal` |
| Live/Prod   | `https://api.ig.com/gateway/deal` |

The Demo account, Demo login credentials, and Demo API key must be used with
the Demo gateway. IG's Getting Started guidance says to switch to the Demo
environment before generating the Demo API key and choose Demo environment
login credentials. A Live API key or Live login credential is therefore not
interchangeable with the Demo gateway.

### Core REST request contract

The IG REST guide specifies the following request contract:

| Requirement | Official requirement |
|-------------|----------------------|
| Transport | HTTPS |
| API key | `X-IG-API-KEY` identifies and authorizes the calling application |
| Request format | `Content-Type: application/json` for JSON request bodies |
| Response format | `Accept: application/json; charset=UTF-8` |
| API version | `Version: <version>`; defaults to v1 when absent |
| Error evidence | Error response bodies have JSON shape `{ "errorCode": "..." }` |
| Correlation | `X-REQUEST-ID` identifies the request when returned by IG |

For a `POST`, the body is JSON. The platform's identifier and password fields
fit IG's session-login model, but the exact version determines its required
response and subsequent authentication scheme.

### Session endpoint versions and token model

The IG REST guide distinguishes the login protocols as follows:

| Session version | Official result | Subsequent request identity |
|-----------------|-----------------|-----------------------------|
| `POST /session` v1 or v2 | `CST` and `X-SECURITY-TOKEN` response headers | Send `CST` and `X-SECURITY-TOKEN` on subsequent REST requests |
| `POST /session` v3 | OAuth access and refresh tokens in the response | Send `Authorization: Bearer <access token>` and `IG-ACCOUNT-ID` |

IG's public guide does not establish that v3 emits the header-token pair. A
client that needs `CST` and `X-SECURITY-TOKEN` for `/accounts` and `/positions`
must use the v1/v2 session protocol, or it must be changed to consume the v3
OAuth response and use `Authorization` plus `IG-ACCOUNT-ID` consistently.

### Password encryption

The IG REST reference exposes both `/session` and `/session/encryptionKey`.
The inspected high-level guide does not prescribe an encrypted password for
every session request. `encryptedPassword: false` therefore represents a
plaintext-password session body over HTTPS, not a proof that encryption is
required or enabled. Before introducing encryption, verify the detailed
`/session/encryptionKey` operation contract in IG's interactive reference for
the selected session version. Do not log the encryption key, timestamp,
plaintext password, ciphertext, API key, identifier, CST, OAuth tokens, or
`X-SECURITY-TOKEN`.

## Platform Request Correlation

Static inspection found one effective broker-auth registration and one gateway:

| Concern | Discoverable platform behavior | Correlation |
|---------|--------------------------------|-------------|
| Environment host | Typed `HttpClient` base address is `https://demo-api.ig.com/gateway/deal/` | Correct Demo gateway. Live is not routed. |
| Environment guard | `Live` immediately returns `UnsupportedEnvironment` before HTTP | Correctly prevents accidental Live-to-Demo routing, but prevents Live validation. |
| Stored credential scope | API key, identifier, and password are loaded from protected storage by `BrokerEnvironmentKind` | The executed path reads the Demo credential set. |
| Session path and method | `POST` to relative `session` | Resolves to `https://demo-api.ig.com/gateway/deal/session`. |
| API key header | `X-IG-API-KEY` is added from the protected Demo credentials | Matches the published header name. |
| Version header | `Version: 3` | Conflicts with the gateway's later requirement for CST/X-SECURITY-TOKEN. |
| JSON body | `identifier`, `password`, `encryptedPassword: false` via `JsonContent.Create` | `Content-Type: application/json` is supplied by `JsonContent`. |
| Accept header | No explicit `Accept` header is set | Does not meet the guide's stated response-format header requirement. |
| Response handling | Requires `currentAccountId`, `CST`, and `X-SECURITY-TOKEN` | Internally inconsistent with IG's documented v3 OAuth protocol. |
| Dependent calls | `/accounts` v1 and `/positions` v2 send API key, CST, and X-SECURITY-TOKEN | Appropriate only after a compatible v1/v2 header-token login. |
| Error handling | Maps only status code, discarding the IG error payload and `X-REQUEST-ID` | Insufficient evidence for provider-side `403` diagnosis. |

The current unit tests verify the Demo host, Live rejection, token propagation
after a synthetic successful login, and status-code mapping. They do not assert
the session request's `Accept` header, version/body compatibility, or retention
of a redacted IG `errorCode` and `X-REQUEST-ID`.

## Likely `403` Causes

The following are evidence-based possibilities, ordered by diagnostic value
rather than probability.

1. The Demo API key is invalid, disabled, suspended, or does not match the
  IG account. IG explicitly lists an invalid or disabled application key as a
  login problem.
2. The API key, identifier, or password belongs to the wrong environment. IG
  explicitly lists login details for the wrong environment and publishes
  separate Demo and Live gateways and Demo API-key creation steps.
3. The session request omits `Accept: application/json; charset=UTF-8`. IG
  explicitly lists missing `Content-Type` or `Accept` headers as a common
  reason an API login fails. The application supplies `Content-Type`, but
  static inspection found no `Accept` header.
4. The request body is malformed or does not match the session-version
  contract. IG explicitly lists malformed JSON as a common cause. The body
  serializes as JSON, but a real capture is needed to prove exact wire shape.
5. IG rejects the API key for authorization, account entitlement, quota, or
  platform policy reasons. The response's `errorCode` is required to separate
  these from bad credentials or malformed requests.
6. The v3 request is accepted but returns OAuth material rather than CST/X-
  SECURITY-TOKEN. In the current code this becomes a malformed/incomplete
  response, not a login `403`; it is nevertheless a blocking protocol defect
  for the subsequent header-token calls.
7. The operator expects Live access. This deployment intentionally refuses
  Live before network I/O, so an IG `403` cannot originate from the current
  Live branch.

## Targeted Diagnostic Evidence

Capture one failed request through a secret-safe outbound HTTP diagnostic
handler or a controlled proxy. Redact values before persistence or support
sharing. Capture metadata only:

| Evidence | Expected value or purpose |
|----------|---------------------------|
| URL and method | `POST https://demo-api.ig.com/gateway/deal/session` for the supported path |
| Environment label | `Demo`; do not record credential values |
| Request headers by name | Presence of `X-IG-API-KEY`, `Version`, `Content-Type`, and `Accept`; record redacted API-key fingerprint or length only |
| Header values that are safe to record | Version, media types, user agent, and timeout policy |
| Request JSON schema only | Field names and JSON validity; redact identifier/password and record no raw body |
| HTTP status | Distinguish `401`, `403`, `429`, and other status classes |
| IG response body | Parse and record only the provider `errorCode`; never store arbitrary body text without redaction |
| IG response header | `X-REQUEST-ID` when present, for a support case with IG |
| OAuth/header-token response shape | Presence of `oauthToken`, `CST`, `X-SECURITY-TOKEN`, and `currentAccountId`, without values |
| Configuration provenance | Credential row environment and last update time/actor, without values, to prove Demo selection |
| Independent reproduction | Sign into IG's web dealing platform and run the same Demo account/key through IG's API Companion |

The final row is IG's recommended first-line troubleshooting sequence. A
successful API Companion request with the same Demo account/key isolates the
problem to the platform request construction or runtime network path. A failed
API Companion request points to credentials, key status, environment pairing,
or IG account/app authorization.

## Recommended Next Investigation

1. Obtain one redacted `403` response's `errorCode` and `X-REQUEST-ID`.
2. Reproduce the Demo login in the IG API Companion with the same Demo API key
  and Demo login identity, without putting those values in source control or
  chat.
3. Decide which protocol the platform requires:
  * Use session v1/v2 if account and position calls continue to depend on
    `CST` and `X-SECURITY-TOKEN`.
  * Use v3 only after implementing OAuth-token parsing, refresh handling,
    `Authorization: Bearer`, and `IG-ACCOUNT-ID` on every dependent call.
4. Add an explicit `Accept: application/json; charset=UTF-8` header and a
  focused request-construction test when implementation work is authorized.
5. Consult IG's detailed interactive `/session` and `/session/encryptionKey`
  reference for the chosen version before enabling encrypted passwords.

## Clarifying Questions

* What is the redacted IG `errorCode` and `X-REQUEST-ID` from the failing
  response?
* Was the configured API key generated after switching the web platform to the
  Demo environment, and do the configured identifier/password sign in to the
  Demo dealing platform?
* Is the intended long-term session protocol header-token authentication
  (v1/v2) or OAuth (v3)?
* Is password encryption a contractual requirement for this deployment, or is
  HTTPS transport with the existing protected credential store acceptable?

## Official Sources

* [IG Labs REST Trading API Guide](https://labs.ig.com/rest-trading-api-guide.html)
* [IG Labs FAQ](https://labs.ig.com/faq.html)
* [IG Labs Getting Started](https://labs.ig.com/getting-started.html)
* [IG Labs REST Trading API Reference](https://labs.ig.com/rest-trading-api-reference.html)
* [IG Labs API Companion](https://labs.ig.com/companion/api-rest-companion-release/index.html)

## Local Evidence Inspected

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs`
* `docs/wiki/ig-day-trading-with-ig-apis.md`
