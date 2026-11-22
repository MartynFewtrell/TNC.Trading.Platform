---
title: Account Preferences Mitigation Plan Verification Research
description: Verifies exhaustive HTTP failure mappings and repository Markdown link validation options for the account preferences mitigation.
ms.date: 2026-08-31
ms.topic: reference
---

## Status

Complete.

## Research questions

1. What exact HTTP status, Problem Details meaning, and `Retry-After` eligibility should apply to every named `AccountPreferencesFailureCategory` member, with IG provider-session authentication distinguished from platform authentication?
2. What existing executable repository command validates Markdown or wiki links, or what concrete validation action should be used if no such command exists?

## Findings

`AccountPreferencesFailureCategory` has ten named members. The current mapper is
not exhaustive: it explicitly maps only `RateLimited`, `MalformedProviderData`,
and `Timeout`; every other named member falls through to the same `503` and
provider-unavailable title. The API unit theory covers only numeric values 2, 3,
5, and 6, so it does not detect semantic drift when enum members are reordered
and does not cover six categories or any response header.

All five Account Preferences routes require the platform Operator policy.
Platform authentication and authorization therefore occur before an Account
Preferences handler runs. Framework `401` means the caller lacks valid bearer
authentication for the platform API, and framework `403` means an authenticated
caller lacks the Operator policy. Those statuses are not
`AccountPreferencesFailureCategory` mappings.

The enum member `Unauthorized` is produced when IG returns `401` from
`accounts/preferences` after the platform request was authorized and an IG
session was created. A GET refreshes the IG session and replays exactly once; a
PUT is not replayed. Returning platform `401` for this category would be
incorrect because HTTP `401` challenges credentials for the target platform
resource and requires `WWW-Authenticate`. Use `502` with an IG-provider-session
problem type instead.

The migrated contract also limits where these mappings appear. The normal GET
is a SQL projection, and normal PUT commits desired state and returns `Pending`
without provider I/O. Background observation persists provider failures as
verification state. Explicit remediation returns state when one exists; its
endpoint currently uses separate `400`, `409`, `503`, and `200` branches rather
than `AccountPreferencesEndpointMapping`. The category table remains necessary
for the legacy outcome boundary and any future endpoint that exposes a typed
provider failure.

`Rejected` is currently overloaded. The SQL desired-state branch uses it for a
stale revision, while the IG gateway uses it for every otherwise-unclassified
non-success response. A deterministic one-category-to-one-contract mapping
requires this invariant: `Rejected` means an authoritative conflict with current
platform or provider state and maps to `409`; transport failures and provider
5xx responses must be classified as `Unavailable` or `Transient`, while an
unsupported deterministic provider response must be `Unsupported`. Without
that classification correction, no exact mapping for `Rejected` can be
semantically correct for all current producers.

## Recommended mapping

Use an explicit exhaustive switch with no default arm. Each Problem Details
response should include the listed `type`, `status`, stable `title`, and a
`failureCategory` extension containing the exact enum member name. Do not copy
`SafeReason`, provider response bodies, credentials, or session tokens into
`detail`. A controlled, platform-authored occurrence detail may be added only
when it gives corrective action without changing the problem type.

| Enum member | HTTP | Problem `type` | Stable `title` | Exact meaning | `Retry-After` eligibility |
| --- | ---: | --- | --- | --- | --- |
| `UnsupportedEnvironment` | 409 | `/problems/account-preferences/unsupported-environment` | `Account preferences are unsupported in the current environment.` | The operation conflicts with the active platform/broker environment; no IG request is permitted. A missing SQL row is not this failure and must return `200` with `Unconfigured`. | No; header must be absent |
| `Unauthorized` | 502 | `/problems/account-preferences/provider-session-rejected` | `IG account preferences session was rejected.` | Platform authentication already succeeded, but IG rejected the downstream provider session for `accounts/preferences`. This is not a platform sign-in challenge. | No; header must be absent |
| `RateLimited` | 429 | `/problems/account-preferences/rate-limited` | `Account preferences allowance was exhausted.` | IG refused the operation because its account-preferences allowance was exhausted. | Conditional; emit only when a validated provider or server-owned delay is available |
| `MalformedProviderData` | 502 | `/problems/account-preferences/malformed-provider-data` | `Account preferences provider data was invalid.` | IG returned a success/acknowledgement representation that could not be parsed or lacked the required typed value. | No; header must be absent |
| `Rejected` | 409 | `/problems/account-preferences/state-conflict` | `Account preferences request conflicts with current state.` | An authoritative current-state or revision conflict prevented the requested operation. Generic IG errors must not use this member. | No; header must be absent |
| `Unavailable` | 503 | `/problems/account-preferences/provider-unavailable` | `Account preferences provider is unavailable.` | The IG dependency could not be reached or used temporarily. | Conditional; emit only when the server knows a concrete recovery delay |
| `Timeout` | 504 | `/problems/account-preferences/provider-timeout` | `Account preferences provider timed out.` | The platform gateway did not receive a timely IG response. | No; header must be absent. Safe reads may still be transport-retried by policy |
| `Unsupported` | 502 | `/problems/account-preferences/unsupported-provider-response` | `Account preferences provider response was unsupported.` | IG returned a syntactically readable response whose contract or shape is unsupported by this adapter. | No; header must be absent |
| `AccountMismatch` | 409 | `/problems/account-preferences/account-mismatch` | `IG session account does not match the configured account.` | The account bound to the IG session differs from the configured target, so observation or remediation is blocked before a write. | No; header must be absent |
| `Transient` | 503 | `/problems/account-preferences/transient-provider-failure` | `Account preferences provider failed temporarily.` | A temporary provider operation failure occurred but no more specific retryable category was retained. | Conditional; emit only when the server knows a concrete recovery delay |

`Conditional` is deterministic: with no validated delay input, the header must
be absent; with a non-negative delay input, it may be emitted only for the three
eligible categories. The current gateway outcome contains no retry delay and
discards IG response headers, so current direct mappings must emit no
`Retry-After`. A later implementation may carry a trusted IG delay for
`RateLimited` or a server-owned retry time for `Unavailable`/`Transient`.

Failed and indeterminate outcomes use the same category status, type, title, and
header rule. If an indeterminate write can reach HTTP without authoritative
read-back, add `operationOutcome: "Indeterminate"`; use `"Failed"` for a known
failure. Clients must not infer that a PUT was unapplied from a 5xx response and
must not blindly replay provider writes. The existing legacy update handler
correctly follows an indeterminate PUT with one GET and never retries the PUT.

Exhaustive implementation tests should enumerate named enum values, not integer
casts, and assert all fields in this table. Add a guard that the theory row count
equals `Enum.GetValues<AccountPreferencesFailureCategory>().Length`; assert that
an unknown cast throws rather than falling through. For each category, assert
that provider diagnostics are absent. For conditional categories, test both no
delay and validated-delay cases; for every other category, assert the header is
absent even when a delay is supplied.

## Documentation validation

No existing executable Markdown or wiki link validator was found. The repository
has no `package.json`, root/scripts command set, `.vscode/tasks.json`, or CI
workflow. The only discovered script,
`.github/scripts/Invoke-CleanArchitectureQualityGate.ps1`, runs `dotnet build`
and `dotnet test` only. Repository prose requires links to be checked but does
not name an executable command.

Use this repository-root PowerShell check after wiki edits. It creates no files
and requires no new package or tool. It checks all local link targets under
`docs/wiki`; it was executed during this research and returned
`All local docs/wiki link targets resolve.`

```powershell
$failures = foreach ($source in Get-ChildItem '.\docs\wiki' -Recurse -Filter '*.md' -File) {
	$text = Get-Content $source.FullName -Raw
	foreach ($match in [regex]::Matches($text, '(?<!!)\[[^\]]+\]\((?<href>[^)]+)\)')) {
		$href = $match.Groups['href'].Value.Trim('<', '>')
		if ($href -notmatch '^(?:https?://|mailto:|#)') {
			$relativePath = [Uri]::UnescapeDataString(($href -split '#', 2)[0])
			if ($relativePath -and -not (Test-Path (Join-Path $source.DirectoryName $relativePath))) {
				[pscustomobject]@{ Source = $source.FullName; Target = $href }
			}
		}
	}
}
if ($failures) {
	$failures | Format-Table -AutoSize | Out-String | Write-Error
	throw "Broken local wiki links: $($failures.Count)"
}
Write-Host 'All local docs/wiki link targets resolve.'
```

This focused fallback does not validate heading fragments or remote URL health.
For each link added or changed by the mitigation, open the source in Markdown
Preview and follow the link once; confirm that any `#fragment` lands on the
intended heading. Remote links changed by the mitigation should be opened once
in the browser. This manual fragment/remote check is preferable to introducing
unrelated link-check tooling for this plan.

## Evidence

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesFailureCategory.cs:3-15` defines all ten members.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:18-52` maps outcomes, collapses seven categories through defaults, emits no problem type or retry header, and intentionally omits `SafeReason`.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-64` protects all five routes with the Operator policy.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:17-54` produces `AccountMismatch`, `Transient`, and `Unsupported` for asynchronous observation/remediation.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:77-103` distinguishes failed reads from indeterminate PUT timeout, transport, and malformed-acknowledgement outcomes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:125-141` maps IG `401` to `Unauthorized`, IG `403` to `RateLimited`, all other non-success responses to overloaded `Rejected`, and malformed bodies to `MalformedProviderData`.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:14-29` shows SQL-first PUT and stale-revision use of `Rejected`.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:37-78` proves legacy writes reconcile once after indeterminate PUT and return authoritative read-back or `NotApplied`.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:14-33` persists provider observation failures and computes server-owned retry times of 5, 30, 120, then 600 seconds.
* `src/TNC.Trading.Platform.Api/Features/Platform/RetryAccountPreferencesVerificationEndpointHandler.cs:7-16` maps retry validation and lease/state conflicts separately from the enum mapper.
* `src/TNC.Trading.Platform.Api/Features/Platform/RemediateAccountPreferencesEndpointHandler.cs:7-17` maps remediation validation, stale revision, lease contention, absent state, and success separately.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:10-25` covers only four integer-cast categories and diagnostic exclusion.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:79-113` proves one GET session replay, forbidden-to-rate-limit classification, and no PUT replay after IG `401`.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:52-95` proves indeterminate PUT reconciliation and no observation for unknown outcomes.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:140-184,199-219` sends platform Operator bearer tokens and converts non-success Problem Details title/detail into `HttpRequestException` while retaining HTTP status.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:22-80` exposes load/save failures as operator feedback and does not branch by category.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:178-190,322-328,365-385` expects failed load/save feedback and preservation of confirmed state after conflict.
* `docs/wiki/api-reference.md:553-571` documents Operator-only, SQL-first GET/PUT, asynchronous verification, and current public status families.
* `docs/wiki/architecture.md:604-621` documents desired/observed ownership, account-bound remediation, revision conflict, and the prohibition on blind PUT retries.
* `docs/wiki/operator-guide.md:569-596` documents unconfigured and degraded presentation plus the Test-only boundary.
* `.github/instructions/docs.instructions.md:17-56` requires maintained links but provides no executable validator.
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1:1-76` is the sole discovered script and runs only solution build and tests.
* RFC 9110 sections 10.2.3, 11.3, 15.5.2, 15.5.10, and 15.6.3-15.6.5 define `Retry-After`, platform-origin `401`, conflict, bad gateway, unavailable, and gateway timeout semantics: <https://www.rfc-editor.org/rfc/rfc9110.html>.
* RFC 9457 sections 3.1 and 4 require stable problem type/title/status semantics and permit a problem type to define `Retry-After`: <https://www.rfc-editor.org/rfc/rfc9457.html>.

## Remaining questions

No question blocks the mapping or documentation-validation recommendation.

Before implementation, the owning plan should decide whether to retain the
legacy provider-backed GET/PUT branch. If it is removed as part of the SQL-first
migration, the enum mapper may be limited to explicit provider-operation
endpoints rather than kept as a general Account Preferences response mapper.

## Prior mitigation research appendix

The material below was appended concurrently from the broader mitigation
investigation. It is retained as supporting implementation context. Where it
leaves the HTTP category contract or link-check action undecided, the exact
findings above supersede it.

## Prior mitigation research status

Complete. Current implementation, tests, documentation, instructions, and
project-level validation commands were verified on 2026-08-31. No product or
test code was modified. Three current test projects have pre-existing compile
failures, recorded under validation gaps.

## Prior mitigation research questions

* Where should Application represent explicit configured and unconfigured account-preferences query state?
* Which API endpoint symbols map query outcomes to HTTP statuses, and what are their current semantics?
* How is `PlatformApiClient` resilience registered, specialized, and testable without stacking handlers?
* How do closed-box tests create SQL state and control or inspect the IG request ledger?
* Which functional and end-to-end tests should prove the corrected behavior?
* Which `docs/wiki/` pages must be updated with the implemented contract and operating behavior?
* Which project-level commands provide focused validation for each mitigation slice?
* Which repository instructions constrain the implementation and tests?

## Prior mitigation findings

### Application query state

* `GetAccountPreferencesHandler.HandleAsync` owns current-state query semantics
	at
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:14-45`.
	Production takes the SQL branch whenever `IAccountPreferencesCurrentStateStore`
	is registered. A null store result is currently converted to a legacy
	`Failed(UnsupportedEnvironment, "Account preferences are not configured.")`
	at lines 17-23.
* `GetAccountPreferencesResponse` currently combines a required legacy
	`AccountPreferencesGatewayOutcome` with nullable `AccountPreferencesCurrentState`
	at
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesResponse.cs:3`.
	This shape cannot distinguish a normal unconfigured SQL result from a legacy
	provider failure without interpreting two independent fields.
* `AccountPreferencesVerificationStatus.Unconfigured` already provides the
	persisted and public status vocabulary at
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesVerificationStatus.cs:3-11`.
	The missing concept is an explicit query result, not another verification
	status.
* `IAccountPreferencesCurrentStateStore.GetAsync` correctly returns a nullable
	state at
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs:5-8`.
	`EfAccountPreferencesCurrentStateStore.GetAsync` preserves null for an empty
	SQL partition at
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:8-11`.
* Production always registers the SQL store at
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:90-96`,
	and API startup installs Application plus Infrastructure at
	`src/TNC.Trading.Platform.Api/Program.cs:20-21`. The initial production GET
	therefore does not use the legacy gateway branch.
* The mitigation should introduce one explicit Application-owned configured or
	unconfigured query result and make `GetAccountPreferencesResponse` carry it.
	Preserve the legacy provider outcome only while the provider-backed branch is
	intentionally supported. The repository's one-top-level-type-per-file rule
	means a new query-state type belongs in its own matching file under
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/`.
* `AccountPreferencesHandlerTests` currently constructs the handler without a
	current-state store at
	`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:150-175`.
	It exercises only the legacy branch. Extend its fixture with a fake
	`IAccountPreferencesCurrentStateStore` and add separate missing-row and
	populated-row tests. Both must assert zero `IAccountPreferencesGateway.GetAsync`
	calls.

### API endpoint and status mapping

* The protected route is registered at
	`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-56`
	and forwarded to `GetAccountPreferencesEndpointHandler` at lines 140-143.
* `GetAccountPreferencesEndpointHandler.HandleAsync` discards `result.State`
	and maps only `result.Outcome` at
	`src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs:8-11`.
	This is the direct boundary defect that turns a normal empty SQL projection
	into an error response.
* `AccountPreferencesEndpointMapping.ToResponse(AccountPreferencesCurrentState)`
	already maps configured SQL state at
	`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:9-13`.
	The endpoint needs an unconfigured response mapping with null desired,
	account, observation, and revision fields plus `verificationStatus` and
	`applicationStatus` set to `Unconfigured`.
* Failure categories are declared at
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesFailureCategory.cs:3-15`.
	The current HTTP map handles only `RateLimited` as 429,
	`MalformedProviderData` as 502, and `Timeout` as 504; every other category
	falls through to 503 and the title `Account preferences provider is
	unavailable.` at
	`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:37-51`.
* The mitigation plan should require explicit mappings for every category.
	Deterministic environment, account, rejection, and state conflicts must not
	be represented as retryable provider unavailability. `Unauthorized` is an IG
	provider-session failure, not platform sign-in failure. The exact choice
	between 409 and another 4xx response for each deterministic category remains
	a contract decision, but no category should reach an accidental default.
* Current API tests cover only four numeric category values and legacy success
	at
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:10-36`.
	No test calls `GetAccountPreferencesEndpointHandler`. Add endpoint-handler
	tests for configured and unconfigured query results, plus exhaustive category
	mapping tests that use named enum values rather than integer casts.

### PlatformApiClient resilience registration and testability

* `AddServiceDefaults` applies `AddStandardResilienceHandler` to every HTTP
	client at
	`src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:42-49`.
* Web startup registers `PlatformApiClient` with only its base address at
	`src/TNC.Trading.Platform.Web/Program.cs:15-18`. There is no
	`RemoveAllResilienceHandlers`, specialized retry predicate, or unsafe-method
	exclusion anywhere in the repository.
* `PlatformApiClient.GetAccountPreferencesAsync` sends the GET and translates
	non-success Problem Details at
	`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:140-147` and
	`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:199-219`. Its parsing and
	error shaping are not the source of the repeated attempts.
* The specialization must replace the inherited pipeline, not add another
	handler. The registration should call `RemoveAllResilienceHandlers()` before
	adding one client-specific standard handler. Its predicate should preserve
	safe transport recovery while excluding deterministic responses and unsafe
	request replay. Attempt count, not elapsed time, is the stable regression
	invariant.
* Existing `PlatformApiClientTests` resolve a directly constructed client from
	`PlatformComponentTestContext`, whose constructor creates `new
	PlatformApiClient(new HttpClient(ApiHandler), ...)` at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:96-118`.
	These tests bypass `IHttpClientFactory` and cannot verify registration,
	stacking, retry predicates, or physical attempt counts.
* Extract the typed-client registration from top-level `Program.cs` into one
	Web composition extension that can be called by startup and Web unit tests.
	Resolve the client through `IHttpClientFactory` with a counting primary
	handler in tests. Cover at least: deterministic unsignaled 503 makes one
	attempt; selected safe transient responses follow the policy; unsafe PUT and
	POST make one attempt; cancellation is preserved; and the effective pipeline
	is not multiplied by the global default.

### Ephemeral SQL and IG ledger closed-box setup

* SQL is unconditionally configured with `WithDataVolume()` and persistent
	container lifetime at
	`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:15-18`.
	Only Keycloak has a persistence switch, defined at lines 5 and 11-13 and
	applied at line 35.
* Both shared AppHost start paths set only
	`AppHost__UsePersistentKeycloakState=false` at
	`test/Shared/Authentication/RealAppHostProcessFactory.cs:13-24` and
	`test/Shared/Authentication/RealAppHostProcessFactory.cs:55-71`.
	The managed path uses `DistributedApplicationTestingBuilder` at lines 84-102
	and currently disables port randomization at lines 88-91.
* Add an AppHost configuration switch such as
	`AppHost:UsePersistentSqlState`, defaulting to persistent for local
	development. When false, omit both `WithDataVolume()` and persistent
	lifetime. Setting only session lifetime is insufficient because a named
	volume survives container disposal.
* Set `AppHost__UsePersistentSqlState=false` in both shared factory start paths.
	Add AppHost unit coverage beside
	`Create_ShouldRegisterExpectedInfrastructureResources_WhenBuilderIsConfigured`
	and
	`Create_ShouldUseSessionLifetimeForKeycloak_WhenPersistentKeycloakStateIsDisabled`
	at
	`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs:16-66`.
	Verify the persistent default and the volume-free, session-scoped test mode.
* Functional and E2E wrappers already forward
	`Ig__AccountPreferencesBaseUrl` at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs:7-12`
	and
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:7-12`.
	No additional provider routing seam is required.
* `ControllableIgProvider.Requests` exposes a snapshot at
	`test/Shared/AccountPreferences/ControllableIgProvider.cs:26`, and requests
	are appended at line 59. There is no reset operation. Add a lock-protected
	`ClearRequests` method and call it at each scenario boundary before asserting
	that initial account-preferences GET caused no
	`GET /gateway/deal/preferences` request.
* Functional and E2E collection fixtures share one AppHost and one provider for
	their collection at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:7-29`
	and
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:7-29`.
	Ledger reset is therefore required even after SQL becomes ephemeral per
	fixture.

### Functional, E2E, and lower-level coverage

* SQL integration already proves a migrated empty database returns null at
	`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:176-188`.
	Keep this test; the Application and API layers must translate its valid
	absence into public `Unconfigured` success.
* The current functional scenario at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:24-52`
	reads during provider outage, updates state, then verifies convergence. It
	does not guarantee a fresh database, assert `verificationStatus` equals
	`Unconfigured`, or prove that the initial GET caused no IG request.
* Add a separate fresh-install functional test that clears the provider ledger,
	marks the provider unavailable, authenticates an Operator, calls the public
	GET, asserts 200 and `Unconfigured`, and asserts no preferences GET in the
	provider ledger. Keep convergence as a distinct scenario so its provider
	calls do not blur the fresh-read invariant.
* The current E2E scenario at
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:25-35`
	checks only that `Desired setting` and `Last observed at IG` labels appear.
	It does not assert the rendered `Unconfigured` state, absence of the
	provider-unavailable alert, or completion of the loading state.
* Strengthen the retained E2E smoke with web-first assertions for loading-state
	exit, visible `Unconfigured`, and absence of
	`[data-testid='account-preferences-error']`. Preserve accessibility-first
	locators where possible.
* Presenter and bUnit ownership remains at
	`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:24-51`
	and
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:309-346`.
	Add a successful unconfigured render test. Keep the current sequencing where
	history loads after successful current state at presenter lines 39-48 unless
	healthy-path measurements separately justify progressive history rendering.

### Documentation updates

* `docs/wiki/api-reference.md:551-568` already promises a SQL-only GET and
	`Unconfigured`; update it with the final explicit response shape, exhaustive
	status map, and retry-signaling contract.
* `docs/wiki/runtime-behavior.md:728-747` already says initial GET remains
	available during IG failure; document that unconfigured absence is normal
	and that Web transport retries do not replay deterministic failures or unsafe
	methods.
* `docs/wiki/operator-guide.md:565-589` needs the fresh-install UI behavior:
	prompt `Unconfigured` display without a provider-unavailable alert and the
	distinction between query failure and no saved preference.
* `docs/wiki/testing-and-quality.md:553-590` currently overstates coverage by
	saying API tests cover the SQL-only GET and bUnit/Playwright prove independent
	outage display. Replace those claims with the exact added Application, API,
	resilience-attempt, AppHost, functional, and E2E tests.
* `docs/wiki/local-development.md:146-164` needs the persistent local SQL versus
	ephemeral closed-box test distinction and any new AppHost setting.
* `docs/wiki/application-overview.md:195-215` and
	`docs/wiki/architecture.md:600-625` already describe the intended SQL-only
	ownership. Update them only if the final query-state type or ownership model
	changes those architectural statements.

### Relevant repository instructions

* `.github/copilot-instructions.md` requires one top-level C# type per matching
	file, feature-local Minimal API mappings, focused startup orchestration,
	fully documented tests, and affected `docs/wiki/` updates before plan
	completion.
* `.github/instructions/architecture.instructions.md` assigns query meaning to
	Application, HTTP translation to API, external mechanisms to adapters, and
	concrete registration to composition roots. It requires direct handler tests.
* `.github/instructions/csharp.instructions.md` requires nullable-safe explicit
	outcomes, RFC 9457 Problem Details at HTTP boundaries, and behavior tests.
* `.github/instructions/configuration.instructions.md` keeps operator-managed
	state in SQL and prohibits secret leakage. Test-only AppHost persistence is
	harness configuration, not operator preference storage.
* `.github/instructions/aspire.instructions.md` keeps AppHost changes in
	composition, resources externalized, and local dependencies Aspire-managed.
* `.github/instructions/aspire-tests.instructions.md` requires closed-box
	`DistributedApplicationTestingBuilder` tests, external configuration seams,
	deterministic cleanup, and no in-process DI replacement.
* `.github/instructions/tests.instructions.md` requires xUnit, test-type project
	separation, deterministic isolation, requirement-traceable XML comments, and
	`MethodName_StateUnderTest_ExpectedResult` names.
* `.github/instructions/playwright.instructions.md` requires `PageTest`,
	`Test.StepAsync`, accessibility-first locators, web-first assertions, and no
	hard-coded waits.
* `.github/instructions/docs.instructions.md` and the repository-wide wiki rule
	require affected wiki pages and links to match delivered behavior.

### Verified validation commands

Run from the repository root. The `--list-tests` form of each command was
checked on 2026-08-31; actual mitigation validation should remove
`--list-tests`.

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences|FullyQualifiedName~PlatformApiClient"
dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --filter "FullyQualifiedName~AppHostInfrastructureRegistrationTests"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet build TNC.Trading.Platform.slnx
dotnet test TNC.Trading.Platform.slnx
```

The Application, API, Web unit, and AppHost commands currently build and list
their focused tests. Functional and E2E execution requires Docker, the
Aspire-managed SQL and Keycloak resources, and access to fixed Keycloak port
8080 through the shared lease. The SQL integration suite requires its configured
SQL Server test connection.

## Prior mitigation evidence

* Primary research:
	`.copilot-tracking/research/2026-08-31/account-preferences-load-performance-research.md`
* Current SQL absence proof:
	`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:176-188`
* Current Application misclassification:
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:17-23`
* Current endpoint state discard:
	`src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs:8-11`
* Current broad 503 fallback:
	`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:37-51`
* Current inherited resilience pipeline:
	`src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:42-49` and
	`src/TNC.Trading.Platform.Web/Program.cs:15-18`
* Current persistent SQL setup:
	`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:15-18`
* Current append-only provider ledger:
	`test/Shared/AccountPreferences/ControllableIgProvider.cs:26,59`
* Current high-level scenarios:
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:24-52`
	and
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:25-35`

## Gaps and Questions

### Current validation blockers

* The Infrastructure integration project does not compile because
	`AccountPreferencesSqlIntegrationTests.cs:240` calls
	`NudgeAuthenticationAsync` without the current `authenticationSnapshotId`
	and `dueAtUtc` arguments. This is pre-existing and unrelated to the research
	document.
* Functional and E2E projects do not compile because
	`test/Shared/AccountPreferences/ControllableIgProvider.cs:40-62` returns
	WireMock interfaces/builders that no longer match the callback delegate's
	`ResponseMessage` return type. Both projects link this shared file.
* Package audit warnings are present across the verified builds, including
	known advisories for `Microsoft.OpenApi`, Data Protection dependencies,
	`MessagePack`, `Scriban.Signed`, `System.Linq.Dynamic.Core`, and `AngleSharp`.
	They are outside this mitigation's behavioral scope but should remain visible
	in the final validation report.

### Planning decisions requiring confirmation

* Decide whether the legacy provider-backed branch in
	`GetAccountPreferencesHandler` remains temporarily supported. If not, remove
	the optional store and dual-result response instead of extending both paths.
* Approve the exact deterministic failure status contract. The recommended
	direction is explicit 4xx conflict/rejection semantics and reserved
	502/503/504 dependency semantics, but category-by-category API compatibility
	must be agreed before implementation.
* Decide whether `PlatformApiClient` should receive one policy suitable for all
	its operations or whether account preferences should move to a dedicated
	typed client. The smaller change is one specialized client registration with
	unsafe retries disabled globally.
* Confirm whether 429 and 503 retries require a valid `Retry-After` response.
	The primary research recommends requiring it so deterministic unsignaled 503
	responses make one attempt.
* Confirm that adding `AppHost:UsePersistentSqlState` is acceptable as the test
	isolation seam and that local development should retain persistent SQL by
	default.

## Recommended Next Research

No additional repository research is required to draft the mitigation plan.
During implementation, complete these checks:

* [ ] Repair the pre-existing SQL integration compile error before using that
	suite as a gate
* [ ] Repair the shared WireMock provider compile errors before functional or
	E2E validation
* [ ] Verify the chosen resilience predicate against the exact
	`Microsoft.Extensions.Http.Resilience` version resolved by the Web project
* [ ] Capture one post-fix Aspire trace proving one SQL current-state request,
	the subsequent history request, and no IG preferences request from initial
	page load
* [ ] Run affected wiki link validation after documentation updates