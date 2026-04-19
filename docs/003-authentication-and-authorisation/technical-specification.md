# Authentication and Authorisation Technical Specification

This document describes how work package `003-authentication-and-authorisation` will be implemented for the platform's Blazor UI and API. It traces the requirements in `requirements.md` into a concrete design aligned with the repository's Blazor-first, Aspire-based, Keycloak-local, and Microsoft Entra ID Azure standards.

## 1. Summary

- **Source**: See `requirements.md` for canonical work metadata, requirement identifiers, and acceptance criteria. See `../business-requirements.md` for project-level business context.
- **Status**: draft
- **Input**: `requirements.md` and `../business-requirements.md`
- **Output**: `plans/001-delivery-plan.md`

## 2. Problem and Context

### 2.1 Problem statement

The platform currently exposes a Blazor UI and API without the local operator authentication and authorization boundary required for protected internal use. This work package establishes standards-based sign-in, sign-out, shared role enforcement, and public-versus-protected surface rules so platform secrets and privileged operational features are only available to authenticated operators with the required roles.

### 2.2 Assumptions

- The platform remains a personal/internal system with a small, pre-provisioned operator user set.
- Local development authentication is provided by Keycloak running in a container orchestrated by the Aspire AppHost.
- Azure-aligned deployments use Microsoft Entra ID without changing the application's OIDC and OAuth 2.0 integration model.
- The existing Blazor Web project remains the primary operator UI and the existing API project remains the protected backend boundary.
- Public health and readiness endpoints continue to be exposed through the repository's shared service defaults.
- The initial release uses named roles `Administrator`, `Operator`, and `Viewer` sourced from the identity provider and exposed as role claims to the application.
- Local development uses a repeatable Keycloak realm import that seeds predefined development users with platform role assignments for sign-in and authorization validation.
- The seeded local development account set includes one account for each platform role: `Administrator`, `Operator`, and `Viewer`.
- Local validation also includes a seeded pre-provisioned user with no platform role so FR3 denial behavior can be validated directly.
- A successfully authenticated user with no platform role is allowed to complete sign-in and is then redirected to the dedicated access-denied page.
- The identity-provider topology uses separate Web and API clients or app registrations, with the API exposing delegated scopes consumed by the Web app.
- The protected API exposes separate delegated scopes for broad access levels: `platform.viewer`, `platform.operator`, and `platform.admin`.
- The Blazor Web app requests a baseline delegated scope at sign-in and acquires higher scopes only when a privileged area requires them.
- Higher delegated scopes are acquired through interactive consent prompts when a privileged area is first accessed.
- Delegated tokens may be renewed silently while the platform session remains valid, but a full sign-in is required when the platform session expires.
- Authorized operators return to the public landing page after sign-in, where the page renders authenticated navigation and operator context.
- The authenticated public landing page shows a welcome message, the operator display name, a sign-out action, and links to protected areas allowed by the operator's role.
- Seeded local development users use fixed local-only development passwords defined in the realm import and documented only for local validation.
- The seeded local development usernames are explicit local-dev identities: `local-admin`, `local-operator`, `local-viewer`, and `local-norole`.
- Seeded local development users share one common fixed local-only password to keep local validation simple and repeatable.
- The shared local-only development password for seeded users is `LocalAuth!123`.
- The Web app uses the standard OIDC callback path `/signin-oidc`.
- The operator display name mapping prefers the `name` claim and falls back to `preferred_username` when `name` is unavailable.
- The UI only needs to expose a minimum authenticated user profile for operator experience, starting with display name and role-derived authorization state.

### 2.3 Constraints

- The design must remain compatible with OpenID Connect for web sign-in and OAuth 2.0-style protected API access, with any SAML 2.0 interoperability handled at the identity-provider boundary.
- Identity-provider configuration must remain environment-driven with no hard-coded authority, tenant, client identifier, or client secret values in product code.
- Local authentication must use Keycloak orchestrated by Aspire, and Azure authentication must use Microsoft Entra ID.
- Protected API endpoints must challenge with `401 Unauthorized` or deny with `403 Forbidden` without browser redirects.
- Protected Blazor navigation must redirect anonymous users to the sign-in entry point and show a dedicated access-denied experience to authenticated users who lack the required role.
- Sign-out in the initial release only ends the platform session and returns the user to the public landing page.
- Authentication and authorization outcomes must be observable without logging tokens, client secrets, or other sensitive protocol data.

## 3. Proposed Solution

### 3.1 Approach

Implement standards-based authentication around the existing Aspire-composed Web and API services by adding an environment-selected identity provider integration layer. The Blazor Web application will use OpenID Connect for interactive operator sign-in, the standard callback path `/signin-oidc`, and a server-side cookie session. Shared authorization constants and policies will define the `Administrator`, `Operator`, and `Viewer` role boundaries once and apply them consistently across Blazor routes, components, and API endpoints. Local development will integrate with an Aspire-managed Keycloak realm and client configuration, while Azure-aligned environments will switch to Microsoft Entra ID configuration using the same application authorization model. The identity provider configuration will use separate Web and API clients or app registrations so the API can expose delegated scopes and validate bearer tokens independently of the interactive Web sign-in client. The API will expose `platform.viewer`, `platform.operator`, and `platform.admin` delegated scopes for broad access alignment between the identity provider and protected API boundary.

The Web app will request the baseline `platform.viewer` scope at sign-in and acquire `platform.operator` or `platform.admin` only when the operator enters privileged areas that require them. This keeps the default delegated grant narrower while preserving a standards-based path for higher-privilege API access. When a higher-scope area is first accessed, the Web app will use an interactive consent flow to request the additional delegated scope.

While the platform session remains valid, delegated API tokens may be renewed silently to avoid unnecessary interruption during normal operator use. If the platform session itself expires or authentication state is otherwise lost, the operator is challenged to sign in again before protected UI or API access continues.

After successful sign-in for an authorized operator, the application will return the user to the public landing page, which becomes an authenticated entry surface showing a welcome message, the operator display name, a sign-out action, and links to protected areas allowed by role without requiring a separate default protected dashboard route. The displayed operator name will prefer the `name` claim and fall back to `preferred_username` when required.

The design adopts a backend-for-frontend style boundary: the Blazor server authenticates the operator, exposes only the minimum authenticated context to UI components, acquires delegated access tokens on behalf of the signed-in operator, and calls protected API endpoints with bearer tokens. This fits the existing Blazor Server architecture, keeps secrets and tokens off the browser, preserves a standards-based protected API boundary, and aligns with Microsoft Learn guidance for Blazor web apps secured with OIDC and protected API access.

For local development, the AppHost-managed Keycloak realm import will seed predefined development users and their platform role assignments so sign-in, role enforcement, and local validation can be exercised repeatably without manual identity setup steps. The initial seeded set will provide one account for each platform role: `Administrator`, `Operator`, and `Viewer`, plus a pre-provisioned no-role user for direct validation of role-assignment denial behavior. The seeded usernames will be `local-admin`, `local-operator`, `local-viewer`, and `local-norole`.

Those seeded local development users will use one shared fixed local-only development password, `LocalAuth!123`, defined in the realm import so local validation is simple and repeatable. These credentials are strictly for local development and validation and aren't intended for any shared, test, or production environment.

If a user authenticates successfully but has no platform role, the platform session is established only far enough to determine the authenticated identity and then routes the user to the dedicated access-denied experience rather than protected operator features.

### 3.2 Alternatives considered

| Option | Summary | Pros | Cons | Decision rationale |
| ------ | ------- | ---- | ---- | ------------------ |
| A | OIDC sign-in for the Blazor Web app, shared role policies, and delegated authenticated API access on behalf of the operator | Aligns with the requirements, fits the existing Blazor Web and API split, keeps the browser thin, and supports both Keycloak and Entra ID | Adds token acquisition and propagation complexity between Web and API | Preferred because it preserves a standards-based protected API boundary and gives the clearest path to consistent UI and API authorization |
| B | OIDC sign-in for the Blazor Web app with cookie-only protection for both UI and API | Simpler implementation inside a single web host boundary | Weak fit for a separately hosted API project and does not satisfy the intended protected API access model as cleanly | Rejected because the repository already has a distinct API service and the requirements call for standards-based protected API access |
| C | Custom local username/password store inside the platform | Full application control | Conflicts with repo standards, increases security risk, and reduces Azure portability | Rejected because repo instructions require Keycloak locally and Entra ID in Azure |

### 3.3 Architecture

The solution extends the existing distributed application with an authentication and authorization foundation spanning the Aspire AppHost, the Blazor Web app, and the API.

- **Components**:
  - `TNC.Trading.Platform.AppHost`: composes local dependencies, adds a Keycloak container for local development, and supplies environment-specific configuration to the Web and API services.
  - `TNC.Trading.Platform.Web`: hosts the public landing page, sign-in and sign-out entry points, callback handling, authentication state, protected Blazor routes, access-denied page, and operator-aware UI rendering.
  - `TNC.Trading.Platform.Api`: validates authenticated operator access for protected endpoints, leaves health/readiness endpoints public, and applies shared role policies to feature endpoints.
  - Shared authorization configuration: constants and registration extensions that define role names, policy names, claim mapping, and the minimum authenticated user context exposed to the UI.
  - Identity provider resources: a Keycloak realm with separate Web and API clients for local development, plus separate Microsoft Entra Web and API app registrations for Azure-aligned environments.
- **Data flows**:
  - Anonymous users can reach the public landing page and health/readiness endpoints.
  - A sign-in request from the Blazor Web app redirects the operator to the configured OIDC provider.
  - After a successful OIDC callback, the Web app establishes the platform session, derives shared authorization state from role claims, and stores the information required for delegated token acquisition.
  - Authorized operators are returned to the public landing page, which renders authenticated navigation and operator-aware state.
  - Protected Blazor routes and UI actions evaluate the authenticated user and applicable role policies.
  - When the Web app calls the API on behalf of the signed-in operator, it acquires a delegated bearer token for the configured API scope and sends it to the API.
  - Protected API endpoints validate delegated bearer tokens, derive the operator identity and roles from claims, and enforce shared role policies.
  - Sign-out clears the platform session and redirects the operator to the public landing page.
  - Authentication failures, sign-outs, and authorization denials emit structured operational signals without secret leakage.
- **Dependencies**:
  - ASP.NET Core authentication and authorization middleware.
  - Aspire AppHost resource composition and service discovery.
  - Keycloak and Aspire Keycloak integration for local development.
  - Microsoft Identity Web for Azure-aligned Microsoft Entra ID integration.
  - Existing shared service defaults, health/readiness endpoints, and API endpoint registration pattern.

## 4. Requirements Traceability

| Requirement ID | Requirement | Implementation notes | Validation approach |
| -------------- | ----------- | -------------------- | ------------------- |
| FR1 | Provide operator sign-in and sign-out for the platform UI | Add OIDC sign-in, cookie session management, return authorized operators to the authenticated public landing page after sign-in, and redirect to the public landing page after sign-out | Functional tests for sign-in, sign-out, session expiry recovery, and post-sign-out access denial |
| FR2 | Provide the intended unauthenticated entry and platform-public surfaces for the initial release | Keep the landing page, auth endpoints, and health/readiness endpoints anonymous while marking operator routes and protected APIs as authenticated | Functional and integration tests proving anonymous access only to intended public surfaces |
| FR3 | Restrict sign-in eligibility to pre-provisioned users with assigned platform roles | Require provider-managed role claims, seed a local pre-provisioned no-role user for validation, complete authentication, and redirect no-role users to the access-denied experience instead of protected features | Role-based functional tests for users with no role and users with valid platform roles |
| FR4 | Expose authentication state to the Blazor UI | Register authentication state services and a minimal authenticated user context service exposing display name and authorization state so the landing page and protected navigation can render correctly for authenticated operators | Component and functional tests proving authenticated and anonymous UI behavior |
| FR5 | Protect operator-only Blazor routes and pages from anonymous access | Apply authorization to protected routes/components and redirect anonymous requests to sign-in | Functional tests for route challenge and post-sign-in success |
| FR6 | Protect operator-only API endpoints from anonymous access | Configure JWT bearer authentication and shared authorization policies for protected API endpoints, leaving health/readiness endpoints anonymous | API integration tests for `401` challenge, `403` deny, and authorized success |
| FR7 | Provide named roles that can be applied consistently across the Blazor UI and API | Define centralized role and policy constants for `Administrator`, `Operator`, and `Viewer` and reuse them across UI and API | Unit, integration, and functional tests covering each role boundary |
| FR8 | Provide authenticated user context needed for protected application behavior | Map only required claims into a minimal UI-facing user context model with display name and authorization data, preferring `name` and falling back to `preferred_username` for display-name rendering | Unit and functional tests proving minimum claim exposure and correct display name behavior |
| FR9 | Enforce the initial role boundaries for protected capabilities | Model administrator, operator, and viewer access through shared policies and apply them to routes and endpoints by feature | Role matrix tests across UI and API feature access |
| FR10 | Provide a dedicated access-denied experience for signed-in users who lack the required platform role | Add an access-denied page for the Blazor UI and rely on `403 Forbidden` for protected API access | Functional tests for unauthorized signed-in UI navigation and API denial behavior |
| NF1 | Authentication and authorization flows must remain compatible with repository standards-based protocols | Use OIDC for web sign-in, OAuth 2.0-style API protection, and provider-driven SAML interoperability only at the IdP boundary | Design review plus local validation against Keycloak and Azure-aligned configuration review for Entra ID |
| NF2 | Invalid, expired, or missing authentication state must fail closed | Use framework authentication challenges, authorization middleware, route protection, and silent delegated token renewal only while the platform session remains valid so protected features remain inaccessible without a valid session | Functional tests for expired, missing, and invalid authentication state |
| NF3 | Identity-provider configuration must be externalized by environment | Bind provider settings from configuration and secret stores, with separate local and Azure provider sections | Configuration review and local startup validation with no hard-coded secrets |
| NF4 | Authentication and authorization outcomes must be observable without exposing secrets | Emit structured logs and audit events for sign-in, sign-out, auth failure, and access denial without token or secret payloads | Log verification and automated tests for denial/failure flows |
| NF5 | The operator experience must make authentication and access state understandable | Provide a public landing page, clear sign-in path, authenticated landing-page state with welcome, display name, sign-out, role-allowed navigation, and a dedicated access-denied page | Functional tests and manual local validation |
| SR1 | Protected platform features must require an authenticated operator session and shared role-based authorization rules | Configure authentication and authorization middleware across Web and API with centralized role policies | Integration and functional tests covering anonymous, authenticated, and role-specific access |
| SR2 | Authentication-related secrets and sensitive protocol data must not be exposed to client code, logs, or reports | Keep secrets server-side, minimize claims exposed to the UI, and redact or avoid sensitive log fields | Code review, configuration review, and log inspection |
| SR3 | Identity-provider credentials and related secret configuration must be stored outside source control and be rotatable | Use Aspire parameters, secret stores, and environment configuration for provider credentials | Local configuration validation and repo review confirming no checked-in secrets |
| SR4 | Unauthorized, tampered, invalid, or expired authentication attempts must be rejected safely | Rely on framework token validation, OIDC error handling, authorization middleware, and fail-closed redirects/HTTP status responses | Functional and integration tests for invalid callback, missing auth, insufficient role, and expired session |
| DR1 | Retain authentication and authorization audit events needed for operator review | Extend the platform audit/event recording approach to include sign-in, sign-out, auth failure, and access denial events for 90 days | Integration tests for event creation and review against retention expectations |
| IR1 | Integrate with the identity provider used for local operator sign-in through a standards-based web authentication flow | Configure separate Web and API identity-provider clients for Keycloak locally and separate Web and API app registrations for Entra ID in Azure-aligned environments | Local end-to-end sign-in validation with provider configuration |
| IR2 | Support secure propagation of authenticated operator context to protected APIs when API calls are made on the operator's behalf | Use delegated bearer tokens acquired by the Web app with baseline `platform.viewer` access and higher `platform.operator` or `platform.admin` scopes only for privileged areas, then validate them with the API's own audience and issuer configuration | Integration and functional tests for authenticated API calls on behalf of a signed-in operator |
| TR1 | Automated tests must cover anonymous versus authenticated access to protected UI and API resources | Add API integration tests and Web functional tests for anonymous versus authenticated access | Test suite execution in unit, integration, and functional layers |
| TR2 | Automated tests must cover the named roles used by protected features | Add tests for `Administrator`, `Operator`, and `Viewer` access across UI and API behavior | Role matrix test execution |
| TR3 | Local validation must cover sign-in, sign-out, and recovery from an invalid or expired session | Provide repeatable local validation guidance using Aspire and Keycloak | Manual validation checklist and supporting automated coverage where feasible |
| OR1 | Identity configuration must be manageable per environment without changing source code | Split local and Azure-aligned provider settings into external configuration and secrets | Local startup validation and configuration documentation review |
| OR2 | Local development guidance must identify prerequisites and validation steps for operator sign-in | Document local Keycloak/Aspire prerequisites, seeded local development usernames, the shared local-only password `LocalAuth!123`, operator role setup, and validation steps | Documentation review and local run-through |

## 5. Detailed Design

### 5.1 Public APIs / Contracts

| Area | Contract | Example | Notes |
| ---- | -------- | ------- | ----- |
| Web | `GET /` | Public landing page for anonymous users | Anonymous surface |
| Web | `GET /authentication/sign-in` | Initiates OIDC challenge | Anonymous surface |
| Web | `GET /authentication/sign-out` | Clears platform session and redirects to `/` | Signed-in operator action |
| Web | `GET /authentication/access-denied` | Dedicated access-denied page | Returned for signed-in users lacking required roles |
| Web | `GET /signin-oidc` | Provider redirect back to the Web app after sign-in | Anonymous provider callback surface |
| API | Existing platform feature endpoints | Protected operator endpoints | Require delegated bearer authentication and role policy unless explicitly public |
| API | Health/readiness endpoints from service defaults | Health and readiness responses | Remain anonymous in the initial release |

### 5.2 Data Model

| Entity/Concept | Fields | Constraints | Notes |
| -------------- | ------ | ----------- | ----- |
| Authenticated operator context | Display name, authentication state, assigned platform roles | Derived from validated identity claims; minimum necessary fields only | UI-facing model |
| Authorization policy catalog | Policy name, allowed roles | Centralized constants and registration | Shared by Web and API |
| Authentication audit event | Event type, timestamp, user identifier, result, correlation data | No tokens, secrets, or raw sensitive protocol values | Retained in line with platform audit policy |
| Identity provider configuration | Provider type, authority, realm or tenant, client identifier, callback paths, scopes | External configuration only; secrets stored separately | Supports local and Azure-aligned environments |
| Identity provider application topology | Web client identifier, API client identifier, exposed delegated scopes, allowed audiences | Separate Web and API registrations per environment with `platform.viewer`, `platform.operator`, and `platform.admin` scopes | Supports independent sign-in and protected API validation |
| Seeded local developer identity | Username, assigned platform roles, enabled state | Provisioned by Keycloak realm import only in local development; includes `local-admin`, `local-operator`, `local-viewer`, and `local-norole` | Supports repeatable local sign-in and role validation |
| Seeded local developer credential | Username, shared fixed local-only password `LocalAuth!123`, assigned platform role | Provisioned by Keycloak realm import only in local development | Supports repeatable local validation without manual password setup |

### 5.3 Implementation Plan (technical steps)

| Step | Change | Files/Modules | Notes |
| ---- | ------ | ------------- | ----- |
| 1 | Add local identity provider composition to the Aspire AppHost | `src/TNC.Trading.Platform.AppHost` | Add Keycloak resource, local realm import, seeded `Administrator`, `Operator`, `Viewer`, and no-role development users, separate Web and API clients, client scopes, and service wiring |
| 2 | Add shared authentication and authorization configuration | `src/TNC.Trading.Platform.Web`, `src/TNC.Trading.Platform.Api`, shared supporting modules | Centralize schemes, role names, policies, claim mapping, delegated scope names `platform.viewer`, `platform.operator`, `platform.admin`, and environment selection |
| 3 | Implement Blazor Web sign-in, sign-out, callback, and access-denied flows | `src/TNC.Trading.Platform.Web` | Add landing page behavior, auth endpoints, route protection, and authenticated UI state |
| 4 | Protect API feature endpoints with shared authorization policies | `src/TNC.Trading.Platform.Api` | Leave health/readiness public, validate delegated bearer tokens, and protect operator endpoints consistently |
| 5 | Add authenticated operator context services and UI integration | `src/TNC.Trading.Platform.Web` | Expose minimum display name and auth state to components |
| 6 | Add delegated token acquisition and Web-to-API propagation | `src/TNC.Trading.Platform.Web`, `src/TNC.Trading.Platform.Api` | Acquire baseline viewer tokens at sign-in, request higher scopes only for privileged areas, and send bearer tokens to the API |
| 7 | Add observability and audit event coverage for auth outcomes | Web, API, and infrastructure audit/event modules | Log and record sign-in, sign-out, failures, token acquisition failures, and access denial without secret exposure |
| 8 | Add automated tests for authentication, authorization, and role boundaries | `test/TNC.Trading.Platform.Web/*`, `test/TNC.Trading.Platform.Api/*` | Follow repository test structure and requirement-traceable documentation rules |
| 9 | Update developer and operator documentation | `docs/003-authentication-and-authorisation/*`, `docs/wiki/*` | Add local run, validation, and operational guidance before completion |

### 5.4 Error Handling

| Scenario | Expected behavior | Instrumentation |
| -------- | ----------------- | --------------- |
| Anonymous user requests protected Blazor route | Redirect to sign-in entry point | Structured log for challenge flow without sensitive user data |
| Authorized operator completes sign-in | Return to the public landing page with welcome content, operator display name, sign-out action, and role-allowed navigation | Structured informational log for successful sign-in |
| Authenticated user has no platform role at sign-in | Redirect to the dedicated access-denied page and block protected operator features | Structured warning log and audit event for authorization denial |
| Signed-in user lacks required role for a Blazor route | Render access-denied page | Structured log and audit event for authorization denial |
| Higher delegated scope acquisition is required for a privileged area | Request the additional scope only when the operator enters that area; if acquisition is declined or fails, block the privileged action safely and keep the operator on an allowed surface | Structured warning or error log with correlation data and no token values |
| Anonymous request hits protected API endpoint | Return `401 Unauthorized` without redirect | Structured warning log and request telemetry |
| Authenticated request lacks required role for protected API endpoint | Return `403 Forbidden` | Structured warning log and audit event for authorization denial |
| Delegated token acquisition for API access fails | Attempt silent token renewal when the platform session is still valid; if renewal fails, fail the protected operation safely and require re-authentication or surface a controlled operator error | Structured error or warning log with correlation data and no token values |
| OIDC callback is invalid or tampered | Reject the callback, fail closed, and return the user to a safe unauthenticated experience | Structured error log with correlation data but no token payload |
| Session expires or auth state is lost during operator use | Challenge again for protected UI, deny protected API calls until re-authenticated, and do not attempt to continue with expired platform session state | Structured log and audit event for auth failure and recovery |
| Provider configuration is missing or invalid | Fail application startup or reject sign-in safely depending on where validation occurs | Startup log or auth failure log with configuration key names only |

### 5.5 Configuration

| Setting | Purpose | Default | Location |
| ------ | ------- | ------- | -------- |
| `Authentication:Provider` | Select active identity provider integration | `Keycloak` locally | External configuration |
| `Authentication:Keycloak:Authority` | Local OIDC authority | None | External configuration |
| `Authentication:Keycloak:Realm` | Local Keycloak realm name | Work-package-specific realm | External configuration |
| `Authentication:Keycloak:ClientId` | Local Web client identifier | None | External configuration |
| `Authentication:Keycloak:ClientSecret` | Local confidential client secret if required | None | Secret store or Aspire parameter |
| `Authentication:Keycloak:RealmImportPath` | Realm import source for local seeded users, roles, and clients | Repository realm import folder | AppHost configuration |
| `Authentication:Keycloak:SeededUserPassword` | Shared local-only password for seeded development users | `LocalAuth!123` | Realm import configuration |
| `Authentication:Keycloak:ApiClientId` | Local Keycloak API client identifier | None | External configuration |
| `Authentication:Entra:TenantId` | Azure tenant binding | None | External configuration |
| `Authentication:Entra:ClientId` | Azure Web app registration identifier | None | External configuration |
| `Authentication:Entra:ApiClientId` | Azure API app registration identifier | None | External configuration |
| `Authentication:Entra:ClientSecret` or certificate reference | Azure confidential client credential | None | Secret store |
| `Authentication:CallbackPath` | OIDC callback path for the Web app | `/signin-oidc` | External configuration |
| `Authentication:SignedOutRedirectPath` | Post-sign-out landing path | `/` | External configuration |
| `Authentication:RequiredScopes` | Delegated API access scopes requested by the Web app | Baseline `platform.viewer`, with `platform.operator` and `platform.admin` requested only for privileged areas | External configuration |
| `Authentication:ApiAudience` | Expected API audience or resource identifier for bearer token validation | Provider-specific API identifier | External configuration |
| `Authorization:RoleClaimType` | Claim type used for role mapping | Provider-specific (`role` or `roles`) | External configuration |
| `Authorization:DisplayNameClaimType` | Primary claim type used for display name | `name` | External configuration |
| `Authorization:DisplayNameFallbackClaimType` | Fallback claim type used when the primary display name is absent | `preferred_username` | External configuration |

## 6. Security Design

- **AuthN/AuthZ**: The Web app uses OIDC interactive sign-in with a server-managed session. For Web-to-API calls, the Web app acquires delegated bearer tokens on behalf of the signed-in operator. The API validates bearer tokens and applies the same shared authorization policies used by the Web app to enforce `Administrator`, `Operator`, and `Viewer` role boundaries. Anonymous users only reach explicit public surfaces.
- **Secrets**: Identity-provider secrets remain external to source control and are supplied through Aspire parameters, developer secret storage, or Azure secret-management mechanisms. No secret values are rendered back to the UI or written to logs.
- **Data protection**: HTTPS is used for auth flows outside Development. ASP.NET Core Data Protection protects server-side auth state and related application secrets. Authentication cookies remain server-managed, and browser-delivered code does not receive client secrets.
- **Threat model notes**:
  - Anonymous access is mitigated by default-authenticated protected routes and endpoints.
  - Insufficient-role access is mitigated by shared policies and an explicit access-denied experience.
  - Invalid or tampered callback responses are mitigated by OIDC middleware validation and fail-closed handling.
  - Secret leakage is mitigated by external configuration, minimum claim exposure, and structured logging rules.
  - Session expiry or loss is mitigated by challenge-and-reauthenticate behavior rather than stale access reuse.

## 7. Observability

| Signal | What | Where | Notes |
| ------ | ---- | ----- | ----- |
| Logs | Sign-in started, sign-in succeeded, sign-out completed, auth failure, token acquisition failure, authorization denial | Existing application logs and Aspire-observed outputs | No tokens, client secrets, or raw protocol payloads |
| Metrics | Counts of successful sign-ins, failed sign-ins, sign-outs, token acquisition failures, and access denials | Existing metrics pipeline from service defaults | Useful for operational review rather than high-volume alerting |
| Traces | Auth-related request traces for sign-in callbacks and protected API requests | Existing tracing pipeline from service defaults | Correlate failures without capturing secret values |
| Audit events | Operator sign-in, sign-out, auth failure, access denial, and local provisioning validation outcomes where relevant | Existing platform audit/event retention mechanism | Retained for 90 days in line with DR1 |

## 8. Testing Strategy

| Test type | Coverage | Location | Notes |
| --------- | -------- | -------- | ----- |
| Unit | Role policy registration, claim mapping, minimum authenticated user context mapping, access-denied decision helpers | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` or closest existing Web unit test project if added, plus existing API/Application unit test projects where appropriate | Use xUnit and requirement-traceable comments |
| Integration | Protected API endpoint bearer authentication and authorization, provider configuration validation, delegated token validation, audit event creation | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` | Verify `401`, `403`, delegated token validation, and authorized success behaviors |
| Functional | Anonymous versus authenticated landing-page behavior, sign-in/sign-out flows, access-denied experience, role-based UI protection, baseline viewer API access, incremental higher-scope Web-to-API calls, consent-prompt outcomes, and silent delegated token renewal while the platform session remains valid | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication` | Requirement-traceable tests for FR1-FR10 and TR1-TR3 |
| E2E | Aspire-orchestrated local sign-in path using Keycloak and protected UI/API behavior across service boundaries | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` | Validate distributed behavior with the AppHost, including bearer token propagation where practical |
| Manual local validation | Start AppHost, sign in via seeded `local-admin`, `local-operator`, `local-viewer`, and `local-norole` Keycloak users, verify landing page, protected routes, delegated API protection, denied access for the no-role user, sign-out, and session recovery | Documented in work-package docs/wiki | Supports TR3 and OR2 |

## 9. Rollout Plan

| Phase | Action | Success criteria | Rollback |
| ----- | ------ | ---------------- | -------- |
| 1 | Introduce local Keycloak composition and non-production auth wiring behind environment configuration | Web and API start successfully, public surfaces remain available, and local sign-in can be exercised in development | Disable auth feature wiring through configuration and remove Keycloak dependency from the local run path |
| 2 | Protect operator UI routes and API endpoints with shared policies | Anonymous protected access is denied and role-based access works for seeded operator accounts | Revert authorization registration and endpoint protection changes |
| 3 | Add audit, observability, automated tests, and updated local guidance | Required tests pass and local validation succeeds without secret leakage | Revert documentation and test changes together with auth feature rollback if required |

## 10. Open Questions

- None at this stage.

## 11. Appendix

- Related business requirement: `BR12` in `../business-requirements.md`
- Related systems analysis candidate: `003-authentication-and-authorisation` in `../systems-analysis.md`
- Microsoft Learn: [Secure an ASP.NET Core Blazor Web App with OpenID Connect](https://learn.microsoft.com/aspnet/core/blazor/security/blazor-web-app-with-oidc?view=aspnetcore-10.0)
- Microsoft Learn: [ASP.NET Core Blazor authentication and authorization](https://learn.microsoft.com/aspnet/core/blazor/security/?view=aspnetcore-10.0)
- Microsoft Learn: [Role-based authorization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/roles?view=aspnetcore-10.0)
- Microsoft Learn: [Configure OpenID Connect Web (UI) authentication in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0)
- Microsoft Learn: [Microsoft Identity Web authentication library](https://learn.microsoft.com/entra/msal/dotnet/microsoft-identity-web/)
- Aspire reference: [Aspire Keycloak integration](https://aspire.dev/integrations/security/keycloak/)
