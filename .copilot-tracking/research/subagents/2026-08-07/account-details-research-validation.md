---
title: Account Details Research Validation
description: Independent validation of the account-details primary research document.
ms.date: 2026-08-07
ms.topic: reference
---

## Research Scope

Validate `.copilot-tracking/research/2026-08-07/account-details-research.md` against the delegated reports, local repository evidence, and the cited IG guidance. Check requirement coverage, immutable retrieval design, security headers, architecture and persistence compatibility, retention and concurrency, and proposed test coverage.

## Status

Complete. The primary document needs targeted edits before it is used as an
implementation specification.

## Findings

### Correction Required: Include `Accept` on the Accounts Request

Primary context: `Research Executed > External Research` and `Refresh Command
and Security > Selected approach`.

The primary correctly identifies IG's `Accept: application/json; charset=UTF-8`
requirement, but the selected implementation only specifies `Version: 1` for
the new `GET accounts` call. The existing shared request helper does not add
`Accept`, so extracting or reusing it unchanged would produce a non-conforming
accounts request. Require the new accounts request or shared helper to set
`Accept` explicitly, and test the complete header set.

Evidence:

* IG's REST guide states that `Accept: application/json; charset=UTF-8` should
	always be set: [REST Trading API Guide](https://labs.ig.com/rest-trading-api-guide.html)
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L164-L175`
	sets `X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN`, and `Version`, but not
	`Accept`

### Correction Required: Model IG Allowance as a 403 Provider Error

Primary context: `Refresh Command and Security > Expected results` and
`Validation Plan > Infrastructure IG/API`.

The primary says upstream allowance maps to `429`. IG documents the relevant
accounts allowance errors as HTTP `403`, while the FAQ documents the 60
per-application and 30 per-account non-trading quotas. The platform may
intentionally normalize the recognized IG `403` allowance `errorCode` to a
client-facing `429`, but the document must say so. The adapter and tests must
distinguish that `403` from other IG `403` cases, such as a disabled API key.

Evidence:

* IG accounts endpoint: [Accounts reference](https://labs.ig.com/reference/accounts.html)
* IG quotas: [IG Labs FAQ](https://labs.ig.com/faq.html)
* `.copilot-tracking/research/subagents/2026-08-07/ig-accounts-api-contract.md`
	identifies the documented allowance `403` codes in `Errors, Limits, And
	Caching`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L200-L207`
	currently maps only an actual HTTP `429` to `RateLimited`; it is not evidence
	that IG allowance arrives as `429`

### Clarification Required: Identify the Account-Contract Evidence

Primary context: `Scope and Success Criteria > Assumptions`, `Supplied Account
Contract`, and `Complete Mapping Example`.

The primary repeatedly calls the two-account JSON "supplied," but the IG
contract delegated report states that no user-supplied JSON was present and
that its mapper derives from the official schema. This makes the provenance of
the required field set and the claim that two accounts are representative
ambiguous. Cite the original supplied payload if it exists outside the research
artifacts; otherwise describe the mapping as the official `/accounts` schema
plus a representative fixture and retain the pre-implementation Demo-response
validation requirement.

Evidence:

* `.copilot-tracking/research/subagents/2026-08-07/ig-accounts-api-contract.md`
	states `No user-supplied JSON was present` in `Scope And Evidence Classification`
* The official endpoint confirms `GET /accounts` version 1 returns the logged-in
	client's accounts: [Accounts reference](https://labs.ig.com/reference/accounts.html)

### Correction Required: Remove the Stale Fetchability Claim

Primary context: `Research Executed > External Research`.

The primary says the accounts reference was not fetchable on 2026-08-07. It
was retrieved during this validation on the same date. Remove the claim or
record the exact transient failure independently; retain the useful instruction
to revalidate the live provider contract before implementation.

Evidence:

* [Accounts reference](https://labs.ig.com/reference/accounts.html) returned
	the `GET (version: 1)` operation during this validation

### Approval: Requirement Coverage and Repository Fit

Primary context: `Scope and Success Criteria`, `Timestamped Account Retrieval`,
`Refresh Command and Security`, `API and Account Details Page`, `Delivery
Sequence`, and `Validation Plan`.

All explicit feature requirements are covered: retrieval of real data from the configured IG test account, SQL
persistence of every returned account and balance with a common UTC timestamp,
a Viewer-readable page, and an Operator-only update. The immutable retrieval
header and child rows are the supported choice. The current proof projection is
single-account, environment-scoped, and best effort, while the proposed model
represents one complete response atomically without storing provider tokens.

The Application-owned ports, Infrastructure-only IG session handling, thin API
routes, Keycloak role split, additive EF migration, startup baseline update,
90-day retention with latest-success preservation, process coalescing, and
SQL Server `sp_getapplock` lease all match local patterns. The proposed tests
cover the important application, adapter, persistence, API, Web, migration,
authorization, and cross-replica paths. Add the explicit IG `403` allowance
case above; no other coverage gap was found.

Evidence:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L13-L175`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:L7-L110`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlPlatformReconciliationLease.cs:L11-L60`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:L15-L95`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:L15-L97`
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:L24-L49`
* `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs:L64-L113`
* `.github/instructions/architecture.instructions.md`
* `.github/instructions/auth.instructions.md`
* `.github/instructions/tests.instructions.md`

## Follow-On Research

* [ ] Immediately before implementation, validate the final DTO against an
	anonymized current IG test-account `/accounts` response and document any contract drift
* [ ] Confirm whether `TriggeredBy` must retain the initiating operator subject
	for the required audit model

## Clarifying Questions

* Was a two-account JSON payload supplied outside the research artifacts? If
	so, add its stable provenance to the primary document; otherwise change the
	"supplied" assertions to the official-schema basis.

## Recommendation

Edit the primary document before implementation. Preserve its selected
immutable retrieval design and test matrix, then correct the header requirement,
the IG allowance translation, the payload provenance, and the stale provider
reference claim.