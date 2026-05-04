# Authentication and Authorisation Requirements

This document defines the work-package requirements for establishing local authentication and authorisation for the platform's Blazor UI and API while remaining aligned with the project-level business requirements, systems analysis, and repository authentication standards.

## 1. Summary

- **Work item**: Authentication and authorisation
- **Work folder**: `./docs/003-authentication-and-authorisation/`
- **Business requirements**: `../business-requirements.md`
- **Owner**: TNC Trading
- **Date**: 2026-04-12
- **Status**: draft
- **Outputs**:
  - `technical-specification.md`
  - `plans/001-delivery-plan.md`

### 1.1 Links

| Document | Path |
| --- | --- |
| Business requirements | `../business-requirements.md` |
| Systems analysis | `../systems-analysis.md` |
| Requirements | `requirements.md` |
| Technical specification | `technical-specification.md` |
| Initial delivery plan | `plans/001-delivery-plan.md` |

## 2. Context

### 2.1 Background

Work package `003-authentication-and-authorisation` establishes operator sign-in and access control for the platform's Blazor UI and API. It supports `BR12` by protecting platform secrets and privileged operational features, and it aligns with the candidate work package in `../systems-analysis.md` that calls for local authentication and authorisation for the API and Blazor UI. Microsoft Learn guidance for ASP.NET Core Blazor recommends standards-based OpenID Connect for web sign-in, protected API access, and role-based or policy-based authorization for routes, components, and endpoints.

## 3. Scope

### 3.1 In scope

- Operator sign-in and sign-out for local platform access.
- A sign-in-first UI entry route that always presents login on first access in the initial release.
- Authentication state for the Blazor UI so protected operator features can respond to signed-in and signed-out states.
- Protection of operator-only Blazor routes, pages, and UI actions.
- Protection of operator-only API endpoints.
- Public health and readiness endpoints in the initial release.
- Shared role-based authorization rules that can be applied consistently across the Blazor UI and API.
- Environment-driven identity configuration that remains compatible with the repository authentication standards and standards-based identity providers.

### 3.2 Out of scope

- `IG` session authentication and continuity, which are covered separately by project-level requirements for broker connectivity.
- External customer, multi-tenant, or public self-service identity scenarios.
- In-app management of users and role assignments in the initial release.
- Ending the external identity provider session during sign-out in the initial release.
- Business-domain risk controls or trading authorization decisions unrelated to operator access control.
- Work-package implementation details such as concrete libraries, container wiring, infrastructure manifests, or deployment steps.

## 4. Functional Requirements

Use `FR1`, `FR2`, ... for functional requirements.

| ID  | Requirement | Rationale | Acceptance criteria | Notes/Constraints |
| --- | ----------- | --------- | ------------------- | ---------------- |
| FR1 | Provide operator sign-in and sign-out for the platform UI. | The project owner must be able to establish and end a trusted operator session for personal/internal use. | A signed-out user accessing the UI entry route is presented with the sign-in experience; a successful sign-in establishes an authenticated session; a signed-in user can sign out; in the initial release, sign-out ends the platform session only; after successful sign-out, the user is returned to the UI entry route and is prompted to sign in again; if authentication state is lost or the session expires during use, the user is redirected to the sign-in entry point and must re-authenticate to continue; after sign-out, access to protected features requires a new sign-in. | Aligns with the work-package intent in `../systems-analysis.md` and with `BR12`. |
| FR2 | Provide the intended unauthenticated entry and platform-public surfaces for the initial release. | The platform needs a deliberate boundary between public and protected surfaces so authentication is consistently applied. | The initial release keeps the UI entry route, sign-in entry point, required authentication callback and end-session endpoints, and public health/readiness endpoints accessible without signing in; the UI entry route must immediately present the sign-in experience on first access rather than rendering operator data; protected operator routes and API endpoints still require authentication and authorization. | Public surfaces must expose only the minimum information required for their purpose. |
| FR3 | Restrict sign-in eligibility to pre-provisioned users with assigned platform roles. | Initial access should be tightly controlled for this personal/internal platform. | Only users pre-provisioned in the external identity system and explicitly assigned one of the platform roles can complete sign-in and obtain platform access; users without a platform role assignment do not gain access to protected platform features. | Sign-in eligibility is limited to pre-provisioned role-assigned users; user and role administration remain outside the application in the initial release. |
| FR4 | Expose authentication state to the Blazor UI. | The UI must be able to distinguish authenticated and unauthenticated states in order to protect operator workflows. | The UI can determine whether the current user is authenticated; protected navigation and page content respond consistently to authentication state; authenticated user context needed for access checks is available to the UI. | Applies to Blazor-first user experience in this repository. |
| FR5 | Protect operator-only Blazor routes and pages from anonymous access. | Sensitive operational features must not be reachable without authentication. | An unauthenticated user requesting a protected Blazor route is redirected to the sign-in entry point; after successful sign-in, authenticated access succeeds when authorization rules are satisfied; public content remains accessible where intended. | Related to `UC9` in `../systems-analysis.md`. |
| FR6 | Protect operator-only API endpoints from anonymous access. | Backend operations and data exposed by the API must be restricted to authenticated operator traffic. | Unauthenticated requests to protected endpoints receive a standard authentication challenge / `401 Unauthorized` response with no redirect; authenticated requests reach protected endpoints when authorization rules are satisfied; the protection mechanism applies consistently across relevant endpoints. | Applies to API endpoints used by the Blazor UI and other internal platform components. |
| FR7 | Provide named roles that can be applied consistently across the Blazor UI and API. | Authorization behavior should be testable and centrally understandable instead of being redefined per feature. | The application defines named roles for protected features; the initial release includes `Operator`, `Administrator`, and `Viewer`; Blazor UI features and API endpoints can require one or more named roles; access is denied when the required role is not present; role-based behavior is verifiable through automated tests. | User and role administration remain outside the application in the initial release. |
| FR8 | Provide authenticated user context needed for protected application behavior. | Protected UI and API features may require identity information and authorization data to enforce access decisions. | The application can access the authenticated user's display name and authorization data required by protected features; unauthenticated users do not receive authenticated operator context; only data required for access decisions and operator experience is exposed. | In the initial release, the Blazor UI exposes display name only as the minimum authenticated user information; Microsoft Learn guidance recommends minimizing claims exposure, especially in Blazor scenarios. |
| FR9 | Enforce the initial role boundaries for protected capabilities. | The initial release needs clear separation between full administration, platform operation, and read-only review. | `Administrator` can access all protected features; `Operator` can run and monitor the platform but cannot manage authentication or authorization settings; `Viewer` has read-only access to monitoring and reporting features; access outside each role boundary is denied consistently in the Blazor UI and API. | The initial role model is intended for personal/internal use and may be refined in a later work package. |
| FR10 | Provide a dedicated access-denied experience for signed-in users who lack the required platform role. | Signed-in users need a clear explanation when they are authenticated but not authorized for the requested feature. | A signed-in user who lacks the required platform role is shown a dedicated access-denied page; the page provides a concise explanation and guidance to contact an administrator outside the application; the response does not expose sensitive authorization internals. | Applies to protected Blazor UI navigation; protected APIs return a standard `403 Forbidden` response with no redirect for authenticated callers who lack the required platform role. |

## 5. Non-Functional Requirements

Use `NF1`, `NF2`, ... for non-functional requirements.

| ID  | Category | Requirement | Measure/Target | Acceptance criteria |
| --- | -------- | ----------- | -------------- | ------------------- |
| NF1 | Standards Compatibility | Authentication and authorization flows must remain compatible with standards-based identity protocols used by the repository. | Compatible with OpenID Connect for web sign-in and OAuth 2.0-style protected API access; SAML 2.0 interoperability, if required later, is handled through the identity provider boundary rather than custom app protocol handling. | The resulting requirements and specification remain provider-agnostic at the application boundary and do not require a proprietary sign-in protocol. |
| NF2 | Reliability/Availability | Invalid, expired, or missing authentication state must fail closed. | Protected features remain inaccessible until a valid authenticated session is re-established. | Expired or invalid authentication state does not grant access to protected UI or API resources; the operator can re-authenticate and continue without needing a code or configuration change. |
| NF3 | Maintainability/Supportability | Identity-provider configuration must be externalized by environment. | No hard-coded authority, tenant, client identifier, or secret values in product code. | Environment-specific identity settings can be supplied through configuration; changing those settings does not require source changes; configuration guidance is documentable for local and Azure-aligned environments. |
| NF4 | Observability | Authentication and authorization outcomes must be observable without exposing secrets. | Sign-in, sign-out, authentication failure, and authorization denial events are recordable without secret values. | Operational outputs can distinguish successful sign-in, failed sign-in, sign-out, and access denial events without logging client secrets, tokens, or other sensitive values. |
| NF5 | Usability/Accessibility | The operator experience must make authentication and access state understandable. | Signed-out and access-denied states are distinguishable in the UI. | The UI provides a consistent sign-in path and a consistent response when access is denied, enabling the project owner to understand whether action is required to authenticate or to obtain the necessary authorization. |

## 6. Security Requirements

Use `SR1`, `SR2`, ... for security requirements.

| ID  | Category | Requirement | Acceptance criteria |
| --- | -------- | ----------- | ------------------- |
| SR1 | Authentication/Authorization | Protected platform features must require an authenticated operator session and enforce shared role-based authorization rules. | Protected UI routes, pages, and API endpoints cannot be accessed anonymously; role checks are enforced consistently wherever required roles are applied. |
| SR2 | Data Protection | Authentication-related secrets and sensitive protocol data must not be exposed to client code, logs, or reports. | No client secret or equivalent sensitive identity-provider credential is exposed in browser-delivered code, standard UI rendering, logs, or reports; only the minimum authenticated user context required by the application is exposed. |
| SR3 | Secrets/Key Management | Identity-provider credentials and related secret configuration must be stored outside source control and be rotatable. | Identity-provider secrets are supplied from external configuration or a secret store; rotating a secret does not require a code change; source control history does not contain newly introduced secrets. |
| SR4 | Threats/Abuse Cases | Unauthorized, tampered, invalid, or expired authentication attempts must be rejected safely. | Anonymous access, invalid credentials, invalid callback responses, insufficient authorization, and expired authentication state are denied; denial outcomes can be reviewed without exposing sensitive data. |

## 7. Data Requirements (optional)

Use `DR1`, `DR2`, ... for data requirements.

| ID  | Requirement | Source | Retention | Acceptance criteria | Notes |
| --- | ----------- | ------ | --------- | ------------------- | ----- |
| DR1 | Retain authentication and authorization audit events needed for operator review. | Platform authentication and authorization activity | 90 days | Sign-in, sign-out, authentication failure, and authorization denial events are retained for review in line with project-level retention expectations. | Aligns with the project-level 90-day audit retention direction in `../systems-analysis.md`. |

## 8. Interfaces and Integration Requirements (optional)

Use `IR1`, `IR2`, ... for integration requirements.

| ID  | Requirement | System | Contract | Acceptance criteria | Notes |
| --- | ----------- | ------ | -------- | ------------------- | ----- |
| IR1 | The platform must integrate with the identity provider used for local operator sign-in through a standards-based web authentication flow. | Local identity provider | OpenID Connect-compatible web sign-in | The operator can sign in locally using the configured identity provider; callback and sign-out flows complete successfully when configuration is correct. | Repository guidance specifies the local identity approach separately from this requirements document. |
| IR2 | The platform must support secure propagation of authenticated operator context to protected APIs when API calls are made on the operator's behalf. | Platform API boundary | Standards-based authenticated API access | An authenticated operator can access protected APIs through the application flow; unauthenticated access is denied; the integration remains compatible with standards-based protected API patterns. | Microsoft Learn guidance distinguishes web sign-in from protected API access. |

## 9. Testing Requirements

Use `TR1`, `TR2`, ... for testing requirements.

| ID  | Requirement | Acceptance criteria | Notes |
| --- | ----------- | ------------------- | ----- |
| TR1 | Automated tests must cover anonymous versus authenticated access to protected UI and API resources. | Test coverage demonstrates that anonymous access is denied or challenged and authenticated access succeeds for protected routes and endpoints. | Include both Blazor UI and API protection paths. |
| TR2 | Automated tests must cover the named roles used by protected features. | Test coverage demonstrates that access is granted when the required role is present and denied when the required role is absent for the `Operator`, `Administrator`, and `Viewer` roles, including the defined separation between administrative, operational, and read-only capabilities. | Cover both Blazor UI authorization behavior and protected API authorization behavior. |
| TR3 | Local validation must cover sign-in, sign-out, and recovery from an invalid or expired session. | A local validation flow demonstrates successful sign-in, successful sign-out, denied access after sign-out, and successful re-authentication after session loss or expiry. | Supports safe iteration during local development. |

## 10. Operational Requirements (optional)

Use `OR1`, `OR2`, ... for operational requirements.

| ID  | Requirement | Acceptance criteria | Notes |
| --- | ----------- | ------------------- | ----- |
| OR1 | Identity configuration must be manageable per environment without changing source code. | Environment-specific identity settings can be supplied and updated outside source control; secret values are handled through secret-management mechanisms rather than code edits. | Aligns with repository-wide authentication and configuration instructions. |
| OR2 | Local development guidance must identify the prerequisites and validation steps for operator sign-in. | Documentation describes how to start the local identity setup, authenticate as the operator, and validate protected UI and API behavior. | Supports repeatable local development and review. |

## 11. Assumptions, Risks, and Dependencies

### 11.1 Assumptions

- The platform remains a personal/internal system with a single known project owner as the initial operator.
- Project-level guidance in `../business-requirements.md` and `../systems-analysis.md` remains the source of truth for this work package.
- The selected identity providers for local and Azure-aligned environments support standards-based authentication and authorization flows.

### 11.2 Risks

- Authentication configuration may be incorrect or incomplete, preventing local sign-in or protected API access.
  - **Mitigation**: Keep configuration externalized, validate it early, and document required values and callback paths clearly.
- Excessive claims or role data could increase session or header size and complicate operator flows.
  - **Mitigation**: Minimize claims exposure and prefer focused authorization rules that only require the data needed by the application.

### 11.3 Dependencies

- `../business-requirements.md`
- `../systems-analysis.md`
- A standards-compliant identity provider for local operator authentication.
- The platform's Blazor UI and API surfaces that require protection.

## 12. Open Questions

- None at this stage.

## 13. Appendix (optional)

- Related project-level references: `BR12`, `UC9`, and work package candidate `003-authentication-and-authorisation` in `../systems-analysis.md`.
- Microsoft Learn: [Secure an ASP.NET Core Blazor Web App with OpenID Connect](https://learn.microsoft.com/aspnet/core/blazor/security/blazor-web-app-with-oidc?view=aspnetcore-10.0)
- Microsoft Learn: [ASP.NET Core Blazor authentication and authorization](https://learn.microsoft.com/aspnet/core/blazor/security/?view=aspnetcore-10.0)
- Microsoft Learn: [Role-based authorization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/roles?view=aspnetcore-10.0)
