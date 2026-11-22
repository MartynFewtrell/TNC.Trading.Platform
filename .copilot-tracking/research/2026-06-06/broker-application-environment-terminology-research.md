---
title: Broker and Application Environment Terminology Research
description: Research record for clarifying Test and Live application status terminology.
ms.date: 2026-08-17
ms.topic: concept
---

## Research Scope

* Inspect repository terminology for broker and application environments: Demo, Test, and Live.
* Identify the configuration UI, domain types, IG integrations, tests, docs, and instructions that use the terminology.
* Determine which research documents under `.copilot-tracking/research/2026-06-06/` require updates for the clarification.

## Findings

### Terminology Recommendation

Use one application-status vocabulary everywhere the platform describes or
persists its operating mode:

* `Test`: connects to the real IG test/demo endpoint for live-like, read-only
	market and account data. It does not place trades and cannot cause monetary
	gain or loss.
* `Live`: future configuration that will connect to the IG live endpoint and
	permit real trading with real monetary gain or loss.

Do not expose separate Platform and Broker selectors. The current UI permits
the incoherent combination `PlatformEnvironment=Live` with
`BrokerEnvironment=Demo`. Replace the application-owned `Demo` value with
`Test` in the environment model, configuration, APIs, credentials, status,
events, and test fixtures. Retain the provider-owned literals
`demo-api.ig.com`, IG "Demo" account names, and quoted provider documentation
only where they identify the external IG endpoint or provider resource.

### Implementation Evidence

* `src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentKind.cs:3-7`
	already defines the application status values `Test` and `Live`.
* `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs:3-7`
	instead persists `Demo` and `Live`, creating the contradictory second
	vocabulary.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:40-58`
	renders independent Platform (`Test`/`Live`) and Broker (`Demo`/`Live`)
	selectors, disabling only the Live broker option when Platform is Test.
* `src/TNC.Trading.Platform.Web/Components/Pages/Status/Status.razor:42-50`
	presents both environment values and Live availability; lines 93, 173-193
	also label the active Test read-only flow as IG Demo.
* `src/TNC.Trading.Platform.Api/appsettings.json:39-42` bootstraps the current
	status as `PlatformEnvironment=Test`, `BrokerEnvironment=Demo`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:91-102`
	routes both current IG clients to `https://demo-api.ig.com/gateway/deal/`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:21-31`
	rejects Live before any request, while
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:17-23`
	hard-codes `BrokerEnvironmentKind.Demo` for credentials.
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:22-27`
	blocks a Live broker target in Test, and
	`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs:69-77`
	expresses the same policy.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:195-205`
	projects the existing Test/Live availability split. The stored environment
	strings are also keys for protected credentials, events, snapshots, and
	configuration audit records, so `Demo` to `Test` is persisted-data migration
	work, not only a display-label change.

### Test Evidence

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs:38-56`
	asserts the displayed application/broker pair `Test / Demo`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:125-151`
	names the credential-reentry behavior as Demo even though it exercises the
	application configuration UI.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs:8-67`
	correctly asserts the Test-to-Live safety block but calls the allowed Test
	behavior a Demo broker.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:9-103`
	verifies the current external endpoint and rejects Live before networking.
	Assertions concerning `demo-api.ig.com` remain required after application
	terminology changes; test names and application-status assertions should use
	Test.

### Documentation and Instruction Evidence

* `docs/business-requirements.md:53-69` describes `demo`/`live` selection and
	an initial demo-only execution scope.
* `docs/002-environment-and-auth-foundation/requirements.md:29-91` calls Test
	application mode and IG demo authentication separate concepts, but uses
	Demo as the broker vocabulary throughout FR2-FR10.
* `docs/006-ig-login/requirements.md:34-65` inconsistently describes the same
	integration as IG Test and IG Demo within consecutive sections.
* `docs/wiki/api-reference.md:42-43,150,222,233,258-280,427,497,516,529-531`
	publishes `Demo` as the API broker-environment value while also correctly
	identifying the real IG test endpoint.
* `docs/wiki/application-overview.md:39-43,100-102`,
	`docs/wiki/architecture.md:436-464`,
	`docs/wiki/ig-day-trading-with-ig-apis.md:38-54`,
	`docs/wiki/local-development.md:124-142`,
	`docs/wiki/operator-guide.md:146-164`, and
	`docs/wiki/runtime-behavior.md:13-23,55-60,162,349,386-403` use IG Demo as
	the application status or user-facing label and need the distinction above.
* `.github/instructions/configuration.instructions.md:1-48` and
	`.github/instructions/auth.instructions.md:1-93` require protected,
	operator-managed configuration and environment-driven identity providers,
	but define no Demo/Test/Live broker-status vocabulary. They do not conflict
	with the recommendation.

### Research Documents Requiring Update

The only pre-existing document under
`.copilot-tracking/research/2026-06-06/` requiring an update is:

* `.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md:34,62,118,122-146,197`
	It assumes the selected supported environment is Demo and proposes a
	Demo-only prerequisite. Update it to describe Test as the current
	application status, preserve the IG test/demo host only as provider detail,
	and describe Live as future, trade-capable configuration.

This research record is new and already uses the clarified terminology.

### Conflicts

* The user clarification says Demo is misleading, while IG itself names its
	non-production host `demo-api.ig.com` and the repository's tests verify that
	host. Renaming the host or interpreting "Demo" as a synthetic test service
	would be incorrect.
* Current SQL-backed records use the broker-environment string as a partition
	key. Replacing `Demo` with `Test` requires a forward data migration or
	backward-compatible reader; a pure enum rename will strand existing
	credentials, login snapshots, proof data, audit rows, and event filters.
* Current safety logic only blocks `Test + Live`; it does not model the
	proposed semantic prohibition on order placement and monetary exposure in
	Test. A future implementation must introduce an explicit trading-capability
	policy rather than relying on a label rename.

## Follow-On Questions

* Does Test allow all non-trading IG account and market-data reads, or should
	particular read operations also be restricted?
* Is existing Demo-keyed SQL data expected to be migrated in place, retained as
	historical Demo-labelled records, or reset before the terminology change?
