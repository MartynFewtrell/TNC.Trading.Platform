<!-- markdownlint-disable-file -->

# IG Login Flow Research

## Status

Complete. Research only. No product code, configuration, credentials, or tests
were changed.

## Research Questions

* Trace the exact path from saving IG credentials to IG HTTP authentication and
  the platform health state becoming `Degraded`.
* Identify the endpoint, headers, environment/base URL, HTTP client
  configuration, status handling, tests, and log-related evidence.
* Assess likely causes of a `403` response within this implementation.

## Exact Flow

1. An authenticated Operator calls `PUT /api/platform/configuration`.
   `PlatformEndpoints` maps the route and requires the Operator policy:
   `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:40-41`.
   The input carries a `Credentials` object containing `ApiKey`, `Identifier`,
   and `Password`:
   `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationRequest.cs:3-9`.
2. `UpdatePlatformConfigurationEndpointHandler` validates, maps the transport
   request, and invokes the Application handler:
   `src/TNC.Trading.Platform.Api/Features/Platform/UpdatePlatformConfigurationEndpointHandler.cs:9-27`.
   `UpdatePlatformConfigurationHandler` first commits, then immediately runs
   reconciliation in the same request path:
   `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:13-16`.
3. `SqlPlatformConfigurationStore.CommitAsync` stores all non-secret settings
   in SQL Server and invokes `ProtectedCredentialService.UpdateAsync` with the
   chosen broker environment and supplied secret fields:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:53-119`,
   especially line 80. It writes a redacted configuration-audit record and
   returns credential presence only, not secret material.
4. `ProtectedCredentialService.UpdateAsync` only upserts a credential when its
   submitted value is nonblank. A blank input retains the prior stored secret:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs:31-46`.
   Values are Data Protection encrypted before persistence. Later reads use
   `GetCredentialsAsync` (line 49); an undecryptable item becomes an empty
   string rather than throwing (lines 58-75).
5. The reconciler obtains the runtime configuration and chooses an action.
   Complete credentials select `TransitionToActive`; incomplete credentials
   select `TransitionToDegraded`:
   `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:31-56`.
   For complete credentials, `TransitionToActiveAsync` records an auth attempt
   and calls the IG gateway. A failed gateway outcome calls the overload of
   `TransitionToDegradedAsync` at
   `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:371-396`.
6. `TransitionToDegradedAsync` assigns the safe failure summary, begins the
   automatic retry cycle, calculates the next delay, creates a retry-cycle
   record, writes an `auth/FailureDetected` warning event, and dispatches a
   failure notification:
   `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:536-572`.
   The state engine persists the target session status and sets `IsDegraded`
   for `Degraded` or `Blocked` targets:
   `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:19-30`.
7. The reconciler persists state after each reconciliation:
   `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:179-198`.
   Startup also reconciles before serving, and the background supervisor runs
   another reconciliation approximately every second:
   `src/TNC.Trading.Platform.Api/Program.cs:28-35` and
   `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:28-60`.

## IG HTTP Contract

* The typed client is registered as
  `IBrokerAuthenticationGateway`/`IgBrokerAuthenticationGateway` with the
  fixed base URL `https://demo-api.ig.com/gateway/deal/`:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:91-94`.
* The initial authentication request is `POST session`, yielding the full URL
  `https://demo-api.ig.com/gateway/deal/session`:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:35-44`.
* Its headers are `X-IG-API-KEY: <stored API key>` and `Version: 3`. Its JSON
  body is `{ identifier, password, encryptedPassword: false }`.
* A successful session response must contain JSON `currentAccountId` and the
  HTTP response headers `CST` and `X-SECURITY-TOKEN`:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:47-65`.
  Neither token crosses the Infrastructure-to-Application boundary or is
  persisted.
* The gateway then best-effort queries `GET accounts` with `Version: 1` and
  `GET positions` with `Version: 2`. Both use `X-IG-API-KEY`, `CST`, and
  `X-SECURITY-TOKEN`:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:100-140` and `160-173`.
  A failure on either proof query returns authenticated evidence with null
  proof, so it does not degrade an otherwise valid login.
* `BrokerEnvironmentKind.Live` is rejected before any HTTP call:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:19-28`.
  The configuration validator also blocks Live broker selection while the
  platform environment is Test:
  `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs:65-71`.
  Changing platform or broker environment marks `RestartRequired`:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:57-61`.

## HTTP Client Configuration

* `AddServiceDefaults` applies the standard resilience handler and service
  discovery to all `HttpClient` registrations:
  `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:42-49`.
* It enables OpenTelemetry HTTP client metrics and tracing:
  `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:106-126`.
  No per-client timeout, proxy, retry override, request logger, or response
  body capture was found on the IG client registration.

## Status Handling And Health Semantics

* `POST session` non-success responses map `401` to `RejectedCredentials`,
  `403` to `Forbidden`, `429` to `RateLimited`, and all other non-success
  statuses to `UnexpectedResponse`:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:174-183`.
  For a session-level 403, the persisted blocked reason and latest failure
  summary are `IG authentication failed: access forbidden.`
* `Degraded` is an application authentication status exposed by the protected
  `GET /api/platform/status` endpoint, not an ASP.NET health-check result:
  `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:32-39`
  and `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusMapping.cs:29-73`.
* Default readiness/liveness only register the `self` health check:
  `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:157-163`.
  No auth-degraded health check was found. Therefore `/health/ready` can be
  200 while `/api/platform/status` reports `AuthState.IsDegraded = true`.

## Logging, Persistence, And Diagnostics

* Every reconciliation auth attempt records an `auth/AuthAttempted`
  operational event, including the broker environment and correlation ID:
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:448-470`.
* A failed session records an `auth/FailureDetected` event with the safe
  summary, retry-cycle ID, and warning severity. The side-effect helper also
  logs `Operational event recorded: {Category}/{EventType} - {Summary}`:
  `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconcilerSideEffects.cs:42-78`.
* The IG gateway does not log the raw HTTP status, response headers, response
  body, request endpoint, or provider error code. The persisted failure event
  retains only the summary, so a 403 cannot be attributed further from the
  application record alone.
* Separately, API callers denied access receive HTTP 401 or 403 before the
  configuration handler runs. Middleware logs the API status and route only:
  `src/TNC.Trading.Platform.Api/Program.cs:57-68`.

## 403 Differential Diagnosis

1. Confirm which 403 occurred. A `PUT /api/platform/configuration` 403 means
   the caller failed the Operator policy and no credentials were saved or IG
   request made. An IG `POST /gateway/deal/session` 403 maps to `Forbidden`
   and triggers degraded state.
2. For an IG session 403, validate the API key is valid for the Demo API host,
   enabled for the session endpoint/API version, and associated with the
   intended IG account. The fixed base URL is Demo, so a Live API key or
   environment-specific key mismatch is a high-probability cause.
3. Validate the identifier and password against the same Demo account and
   check IG account restrictions, access permissions, lock status, and any
   provider-side regional or security restriction. The application does not
   expose IG's response body or provider error code.
4. Check data-protection continuity. Credential presence checks only row
   existence, but a Data Protection unprotect failure becomes empty credentials
   while leaving presence complete. This can produce an outbound session
   request with blank credential values and a provider rejection.
5. Do not diagnose a 403 from an `accounts` or `positions` proof call as the
   degraded trigger. The test suite confirms that a proof-query 403 preserves
   authenticated state and returns no proof:
   `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:137-153`.

## Test Evidence

* Gateway tests prove the Demo host, three-call protocol, session-token
  forwarding, Live no-network rejection, and mappings for 401/403/429/500:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:13-153`.
  The explicit 403 mapping test data is at line 83.
* Reconciliation tests prove a rejected credential outcome becomes persisted
  `Degraded`, `IsDegraded = true`, a safe blocked reason, and no login
  snapshot:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs:467-502`.
* There is no found test that sends an actual IG request, captures a real IG
  403 payload, or verifies an IG-provider-specific reason code. Tests use a
  controlled `HttpMessageHandler` or fake gateway.

## Recommended Next Investigation

* Obtain the exact failing URL and status from outbound OpenTelemetry traces,
  correlated to the `AuthAttempted` and `FailureDetected` operational-event
  IDs. Do not log API keys, passwords, CST, or `X-SECURITY-TOKEN` values.
* Determine whether the observed 403 came from the protected platform API or
  IG. For IG, compare the Demo API key and account identity with the IG portal
  and inspect the provider's error response through an approved secret-safe
  diagnostic channel.
* Verify persisted Data Protection keys are shared and readable by the active
  API instance, then resave all three IG secret fields if decryptability is in
  doubt.
* Consider a narrowly scoped future enhancement that records only a sanitized
  outbound endpoint, HTTP status, provider error code classification, and
  correlation ID for session failures. It should retain the existing secret
  redaction guarantees.

## Questions Requiring Runtime Input

* Was the observed 403 returned for `PUT /api/platform/configuration`, for
  `POST https://demo-api.ig.com/gateway/deal/session`, or for a later proof
  request?
* What safe status, provider error code, timestamp, trace ID, and correlation
  ID are available for the failed attempt?
* Were the API key, identifier, and password all deliberately resubmitted,
  and is the active process using the expected shared Data Protection key
  ring?