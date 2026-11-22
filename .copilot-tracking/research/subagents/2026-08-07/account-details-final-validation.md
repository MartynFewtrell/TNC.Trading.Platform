---
title: Account Details Final Validation
summary: Narrow final validation of the account-details research protocol corrections
status: complete
author: GitHub Copilot
ms.date: 2026-08-07
ms.topic: reference
---

## Research Scope

Validate `.copilot-tracking/research/2026-08-07/account-details-research.md`
only for remaining concrete contradictions, missing user requirements, or unsupported
implementation claims. The required checks are:

* Explicit `Accept: application/json; charset=UTF-8` request header
* Provider `403` to platform `429` normalization only for recognized allowance errors
* Immutable, complete snapshot consistency
* Viewer-read and Operator-refresh authorization split
* No session-token persistence
* The user-provided representative two-account JSON payload is authoritative evidence

## Status

Complete. Approved with no required edits. No product code was modified.

## Evidence Log

* The IG REST guide requires `Accept: application/json; charset=UTF-8` for JSON
	responses and describes CST plus `X-SECURITY-TOKEN` for v1 and v2 sessions.
	The research document requires the exact header on the new `GET /accounts`
	request and keeps both session tokens in Infrastructure-local request flow.
	[IG REST Trading API Guide](https://labs.ig.com/rest-trading-api-guide.html)
* `GET /accounts` is version 1 and documents `403` among its possible responses.
	IG documents JSON error bodies containing `errorCode`. The research document
	maps a provider `403` to platform `429` only after recognizing a documented
	allowance `errorCode`; it does not translate arbitrary `403` responses.
	[IG Accounts Reference](https://labs.ig.com/reference/accounts.html)
	[IG REST Trading API Guide](https://labs.ig.com/rest-trading-api-guide.html)
* The current gateway confirms the baseline token boundary: CST and
	`X-SECURITY-TOKEN` are read into local variables and supplied only to request
	headers. `BrokerAuthenticationProof` and `EfPlatformIgProofDataStore` persist
	account facts, not either token.
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`
	`src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationProof.cs`
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgProofDataStore.cs`
* The API defines distinct policies: `PlatformViewer` permits Viewer, Operator,
	and Administrator roles, whereas `PlatformOperator` permits only Operator and
	Administrator. The proposed Viewer GET and Operator POST match the established
	authorization model.
	`src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs`
	`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
* The source document requires validation of every item before one transaction
	inserts an immutable retrieval header and all child rows, rejects empty,
	duplicate, and incomplete responses, and reads a complete latest header with
	its children. This preserves an all-or-nothing snapshot and does not conflict
	with retention deleting expired historical snapshots.
	`.copilot-tracking/research/2026-08-07/account-details-research.md`
* The user explicitly established that the supplied two-account JSON is
	representative evidence. The source document honors this by requiring that
	every valid item in `accounts` is persisted and by excluding any two-row
	assumption. No external payload is needed to validate that requirement.

## Findings

No remaining concrete contradiction, missing user requirement, or unsupported
implementation claim was found in the reviewed scope.

The existing `CreateSessionRequest` helper does not add `Accept` to its current
GET calls. This is not a contradiction in the research document because the
proposed dedicated account-details gateway explicitly requires that header. It
is a delivery acceptance criterion and should be asserted by its focused HTTP
test.

## Recommended Next Research

* None required for this final-validation scope

## Clarifying Questions

None.
