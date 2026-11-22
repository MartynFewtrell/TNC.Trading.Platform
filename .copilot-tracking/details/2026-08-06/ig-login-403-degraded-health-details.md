<!-- markdownlint-disable-file -->
# Implementation Details: IG Login 403 Degraded Health

## Context Reference

Sources: `.copilot-tracking/research/2026-08-06/ig-login-403-degraded-health-research.md`; `.copilot-tracking/research/subagents/2026-08-06/ig-login-implementation-surface-research.md`; `.github/copilot-instructions.md`; `.github/instructions/architecture.instructions.md`; `.github/instructions/configuration.instructions.md`; `.github/instructions/tests.instructions.md`.

## Implementation Phase 1: Credential Usability Contract And Storage

<!-- parallelizable: false -->

### Step 1.1: Extend the credential-readiness contract

Keep `HasApiKey`, `HasIdentifier`, and `HasPassword` as storage facts. Add redacted `IsApiKeyUsable`, `IsIdentifierUsable`, and `IsPasswordUsable`, plus `IsAuthenticationReady` and `RequiresCredentialReentry`. Avoid new required positional constructor arguments because eleven sites construct `CredentialPresence`; use defaults, a factory, or another compatibility-preserving design.

Files:

* `src/TNC.Trading.Platform.Application/Configuration/CredentialPresence.cs` - Add secret-free usability and policy predicates.
* `src/TNC.Trading.Platform.Application/Configuration/IProtectedCredentialService.cs` - Return the expanded application-owned projection.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs` - Propagate the new shape where mapping requires it.

Discrepancy references:

* Addresses DR-01.

Success criteria:

* Row presence by itself never permits authentication.
* The projection contains no secret, ciphertext, exception, protector, or key-ring data.

Context references:

* `.copilot-tracking/research/subagents/2026-08-06/ig-login-implementation-surface-research.md` (Lines 31-43 and 124-141).

Dependencies:

* None.

### Step 1.2: Derive usability in the protected credential adapter

Load the three stored values once. Attempt unprotect only inside the existing adapter and translate `CryptographicException` to `false` usability without logging it. Preserve `UpdateAsync`: nonblank fields are protected with the active provider; blank fields retain ciphertext; unreadable values are never automatically migrated.

Files:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs` - Calculate usability and retain safe writes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs` - Map the expanded projection.

Success criteria:

* One unreadable row makes `IsAuthenticationReady` false even when all rows exist.
* A full three-field update produces values protected by the active key ring.

Dependencies:

* Step 1.1.

### Step 1.3: Add persistence regression tests

Seed one unreadable row with two readable rows. Assert all rows are present, the affected field is unusable, and authentication is not ready. Add partial-save coverage proving an omitted unreadable row remains unusable, and complete re-save coverage using a fresh shared-provider instance. Document every test with incident traceability and secret-safety rationale.

Files:

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ProtectedCredentialServiceTests.cs` - Add deterministic readiness and re-save tests.

Success criteria:

* Tests run in-process with no real SQL, network, or IG account.
* Test output contains no secret or ciphertext.

Dependencies:

* Steps 1.1 and 1.2.

### Step 1.4: Validate the credential boundary

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ProtectedCredentialServiceTests|FullyQualifiedName~AuthRetryCycleTests"`

## Implementation Phase 2: Authentication Guards And Redacted Remediation

<!-- parallelizable: false -->

### Step 2.1: Gate automatic authentication on readiness

Replace presence-only checks in transition/reconciliation paths with `IsAuthenticationReady`. For unusable credentials, persist the existing degraded/blocked state using a fixed reason such as `IG Demo credentials must be re-entered.` Short-circuit before creating `AuthAttempted` or calling `IBrokerAuthenticationGateway`. Keep policy in Application and provider mechanics in Infrastructure.

Files:

* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs` - Use readiness for transition eligibility.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs` - Short-circuit broker calls and persist remediation.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs` - Reuse `BlockedReason`; do not add a migration unless code review proves it necessary.

Discrepancy references:

* Addresses DR-01.

Success criteria:

* Unusable credentials persist degraded state without an outbound request.
* Missing and unusable credentials retain distinguishable, non-secret reasons.

Dependencies:

* Implementation Phase 1.

### Step 2.2: Close the manual-retry bypass

Retain the current retry-intent commit, then apply the same readiness predicate before its direct broker call. For unusable credentials, preserve the current audit sequence and persist a redacted degraded remediation result rather than changing the endpoint into a conflict response.

Files:

* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs` - Apply the guard and existing degraded outcome behavior.

Success criteria:

* Eligible manual retry never sends unusable credentials to IG.
* The response and audit disclose no decryptability cause per secret.

Dependencies:

* Step 2.1.

### Step 2.3: Test auth-guard regressions

Add automatic reconciliation and manual-retry scenarios with one unreadable credential. Assert fake gateway call count is zero, runtime state is degraded with the fixed reason, and automatic reconciliation emits no `AuthAttempted` event.

Files:

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs` - Automatic reconciliation behavior.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs` - Manual retry behavior.

Success criteria:

* Both entry points prove the same readiness rule in deterministic unit tests.

Dependencies:

* Steps 2.1 and 2.2.

### Step 2.4: Carry allowlisted session-403 diagnostics to failure events

Add an optional, provider-neutral fixed-shape diagnostic record to `BrokerAuthenticationFailure`, with only `ErrorCode` and `RequestId`. Preserve existing failure construction compatibility. For an IG session `403` only, extract JSON `errorCode` and `X-REQUEST-ID`; missing, invalid, malformed, or values longer than 256 characters become `null` without a new error path. Map the diagnostic object only to `FailureDetected` event details in automatic and manual flows. Keep summaries, runtime state, status payloads, and notifications unchanged.

Files:

* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationFailure.cs` - Add an optional fixed-shape diagnostic contract.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs` - Extract only the two allowlisted values for session `403`.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs` - Add diagnostics to `FailureDetected` event details only.
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs` - Add the same event-detail mapping.

Discrepancy references:

* Resolves DR-04.

Success criteria:

* The application boundary cannot accept arbitrary provider metadata.
* No request body, API key, identifier, password, CST, security token, bearer token, authorization header, ciphertext, exception text, or raw HTTP type crosses the gateway boundary.

Dependencies:

* Steps 2.1 and 2.2.

### Step 2.5: Test auth guards and diagnostics

Add a gateway `403` test with sentinel secret/header/token content in the body and unrelated headers, asserting only `errorCode` and `X-REQUEST-ID` reach the diagnostic object. Add accepted-at-256 and discarded-at-257 character cases, plus invalid/non-403 cases with no diagnostic. Add port, automatic-persistence, and manual-retry tests that assert exactly the two event-detail values and that sentinels remain absent from runtime state, summaries, events outside the typed detail, and notifications.

Files:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/PlatformAuthentication/BrokerAuthenticationOutcomeTests.cs` - Assert fixed diagnostic shape.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs` - Assert 403-only allowlist extraction.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs` - Assert automatic event persistence and summary isolation.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs` - Assert manual event-detail mapping.

Success criteria:

* Synthetic provider data cannot leak across application, persistence, status, notification, or summary boundaries.
* Each provider-controlled diagnostic field is accepted at 256 characters and discarded at 257 characters.
* Tests remain in-process and do not perform a provider call.

Dependencies:

* Step 2.4.

### Step 2.6: Validate auth guards and diagnostics

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ProtectedCredentialServiceTests|FullyQualifiedName~AuthRetryCycleTests"`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BrokerAuthenticationOutcomeTests|FullyQualifiedName~TriggerManualAuthRetryHandlerTests"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~IgBrokerAuthenticationGatewayTests|FullyQualifiedName~AuthRetryCycleTests"`

## Implementation Phase 3: Configuration Projection And Operator Recovery

<!-- parallelizable: false -->

### Step 3.1: Project safe readiness data

Map per-field usability and the aggregate re-entry signal through both configuration API response shapes and Web view models. Map booleans only. Never return a secret, raw exception, ciphertext, or key identifier.

Files:

* `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/CredentialPresenceResponse.cs`
* `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/GetPlatformConfigurationMapping.cs`
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatedCredentialPresenceResponse.cs`
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs`
* `src/TNC.Trading.Platform.Web/CredentialPresenceViewModel.cs`
* `src/TNC.Trading.Platform.Web/PlatformConfigurationViewModel.cs`

Discrepancy references:

* Addresses DR-01 and DD-02.

Success criteria:

* Empty secret-update model behavior remains unchanged.

Dependencies:

* Implementation Phase 1.

### Step 3.2: Render write-only operator remediation

Show an existing-form message requiring API key, identifier, and password together in one Demo save when `RequiresCredentialReentry` is true. Preserve blank initial secret values and avoid prompting a partial repair.

Files:

* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModel.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationFormModelMapper.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor`

Success criteria:

* UI contains no persisted secret and gives one complete re-entry action.

Dependencies:

* Step 3.1.

### Step 3.3: Test safe projection and conditional message behavior

Add API mapper tests that construct all-usable and re-entry-required application responses, assert every new safe boolean, and prove the response model has no secret/ciphertext/error/key fields. Extend the existing bUnit configuration tests with a `RequiresCredentialReentry` payload, a dedicated stable `data-testid`, an assertion that the message appears only for the required state, and blank secret-input/markup assertions.

Files:

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/GetPlatformConfigurationMappingTests.cs` - Create focused GET-mapping tests following `GetPlatformStatusMappingTests.cs`.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/UpdatePlatformConfigurationMappingTests.cs` - Create focused save-response-mapping tests with the same response property allowlist.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs` - Add conditional remediation-message coverage.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebTestData.cs` - Add optional remediation state to configuration payload construction.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor` - Add the stable message selector beside the credential handling hint.

Discrepancy references:

* Resolves DR-02 and DR-03.

Success criteria:

* Server mapping and rendered UI prove remediation state without secret disclosure.
* The message is absent for a fully usable credential set.

Dependencies:

* Steps 3.1 and 3.2.

### Step 3.4: Verify operational re-save behavior

Inspect only usability flags and existing `SecretsUpdated` audit metadata. When any field is unusable or omitted, save all three nonblank Demo values. Confirm a fresh active-process read reports all fields usable before interpreting a later IG response.

Files:

* No product file change - approved operational verification.

Success criteria:

* Verification captures no credential, ciphertext, key data, CST, or security token.

Dependencies:

* Step 3.3 plus active key-ring access.

### Step 3.5: Validate configuration projections

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~GetPlatformConfigurationMappingTests|FullyQualifiedName~UpdatePlatformConfigurationMappingTests"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ConfigurationTests"`
* `dotnet build src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --no-restore --nologo -v:minimal`
* `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore --nologo -v:minimal`

## Implementation Phase 4: IG Session Protocol Follow-Up

<!-- parallelizable: false -->

### Step 4.1: Confirm the documented v2 contract

Verify `/session` v2 method, body, required `Accept`, version header, Demo URL, and CST/header-token response contract at <https://labs.ig.com/rest-trading-api-reference.html>. Record any conflict in the Planning Log before editing code.

Files:

* `.copilot-tracking/plans/logs/2026-08-06/ig-login-403-degraded-health-log.md` - Record a provider-contract discrepancy if found.

Discrepancy references:

* Addresses DD-01.

Success criteria:

* The adapter change is grounded in the v2 reference, not inferred from v3 OAuth.

Dependencies:

* Operational re-save succeeds or a provider `403` persists with all fields usable.

### Step 4.2: Correct the session request only

Set session `Version: 2` and `Accept: application/json; charset=UTF-8`. Retain Demo URL, body, `X-IG-API-KEY`, CST plus `X-SECURITY-TOKEN` extraction, `/accounts` v1, and `/positions` v2. Do not introduce OAuth or Live support.

Files:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`

Success criteria:

* The application port and reconciliation behavior do not change.

Dependencies:

* Step 4.1.

### Step 4.3: Test the wire contract

Capture the session request in the fake handler. Assert v2, JSON `Accept`, JSON content, and secret field names without values. Assert CST/header-token proof calls are unchanged.

Files:

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs`

Success criteria:

* Tests fail if v3 or absent `Accept` returns.

Dependencies:

* Step 4.2.

### Step 4.4: Validate protocol tests

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-restore --nologo --filter "FullyQualifiedName~IgBrokerAuthenticationGatewayTests"`

## Implementation Phase 5: Final Validation And Operational Verification

<!-- parallelizable: false -->

### Step 5.1: Run automated validation

Validation commands:

* `dotnet build TNC.Trading.Platform.slnx --no-restore --nologo -v:minimal`
* `dotnet test TNC.Trading.Platform.slnx --no-restore --nologo -v:minimal`

Reproduce the known baseline build failure with full diagnostics first. Fix only failures attributable to this work and record unrelated failures.

### Step 5.2: Verify the actual operational route

Confirm the `403` comes from `POST /gateway/deal/session`, not `PUT /api/platform/configuration`. Confirm all usability flags after re-save. Verify active session state or proceed with restricted provider diagnostics.

### Step 5.3: Investigate a remaining provider rejection safely

Capture only route, status, `Version`, `Accept` presence, provider `errorCode`, and `X-REQUEST-ID`. Check Demo environment, API-key status, and provider account status. Never log request bodies, credentials, security headers/tokens, or Data Protection information.

## Dependencies

* .NET SDK `10.0.200` and restored packages.
* Authorized configuration UI access and active shared key-ring access.

## Success Criteria

* Full solution build/test passes except separately logged baseline defects.
* Undecryptable credentials become a secret-safe remediation state rather than an IG `403`.
* Protocol correction remains isolated and regression-tested.