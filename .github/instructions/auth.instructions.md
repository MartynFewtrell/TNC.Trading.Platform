---
description: 'Standardize authentication for local development and Azure by using Keycloak (Aspire-orchestrated) locally and Microsoft Entra ID in Azure, with protocols aligned to OIDC/OAuth 2.0/SAML 2.0 for compatibility.'
applyTo: 'src/**/AppHost/**/*, src/**/*AppHost*.csproj, src/**/*.cs, src/**/*.csproj, src/**/*.json'
---

# Keycloak (local) + Microsoft Entra ID (Azure) authentication

## Overview

These instructions standardize how authentication is configured so local development uses a Keycloak container orchestrated by .NET Aspire, while Azure deployments use Microsoft Entra ID. Application code should focus on OpenID Connect (OIDC) and OAuth 2.0 so it works with both environments; SAML 2.0 support should be handled at the identity-provider level when required.

## Scope

Applies to: `src/**/AppHost/**/*, src/**/*AppHost*.csproj, src/**/*.cs, src/**/*.csproj, src/**/*.json`

- Applies when adding or changing authentication for APIs, web apps, or Aspire orchestration.
- This is additive to `/.github/instructions/aspire.instructions.md` and `/.github/instructions/dotnet-stack.instructions.md`.

## Instructions

### MUST

- Use Keycloak for local authentication.
  - Keycloak MUST run in a container in the local environment.
  - If the repo uses an Aspire `AppHost`, Keycloak MUST be orchestrated via the Aspire Keycloak integration.

- Use Microsoft Entra ID for authentication in Azure environments.
  - When integrating ASP.NET Core apps/APIs with Entra ID, use `Microsoft.Identity.Web`.

- Prefer standards-based integration in application code:
  - Web apps MUST use OIDC Authorization Code flow (with PKCE where applicable).
  - APIs MUST validate access tokens using OAuth 2.0 / JWT bearer authentication.

- Keep identity configuration externalized.
  - Authority/issuer URLs, tenant/realm, client IDs, and secrets MUST NOT be hard-coded.
  - Secrets MUST NOT be checked into source control.

- When hosting Keycloak via Aspire in the `AppHost`:
  - Add Keycloak using `Aspire.Hosting.Keycloak`.
  - Use a stable local port for the Keycloak resource (for example `8080`) to avoid browser cookie/token issues across AppHost runs.
  - Use parameters / the AppHost secret store for Keycloak admin credentials when explicitly setting them.
  - If using `WithDataVolume()`, treat the volume as persistent state and delete/reset it if admin credentials or realm configuration changes.
  - Prefer `WithRealmImport("./Realms")` (or equivalent) to keep realm/client setup repeatable.

- When wiring apps to Keycloak:
  - Use `Aspire.Keycloak.Authentication` for Aspire-managed Keycloak instances.
  - `RequireHttpsMetadata` MAY be disabled only in Development.
  - In non-Development environments, `RequireHttpsMetadata` MUST remain enabled and the authority metadata endpoint MUST be HTTPS (explicit `Authority` where required).

### SHOULD

- Prefer OIDC/OAuth in app code even when SAML 2.0 interoperability is required.
  - If a partner/system requires SAML 2.0, prefer configuring SAML at the identity provider (Keycloak or Entra) and exposing OIDC/OAuth 2.0 to the application.

- Keep provider selection environment-driven.
  - Use configuration to choose the active provider (Keycloak locally, Entra ID in Azure) without changing application code per environment.

- Keep tokens and cookies scoped correctly per app.
  - Validate audience for APIs.
  - Use least-privilege scopes.

### MUST NOT

- MUST NOT use Keycloak as the production identity provider for Azure deployments (use Entra ID).
- MUST NOT implement time-based waits to “fix” auth readiness; rely on Aspire orchestration and application health/readiness patterns.
- MUST NOT disable HTTPS metadata validation outside Development.
- MUST NOT add direct SAML protocol handling to applications unless there is a concrete requirement and no viable IdP-level brokering approach.

## Output and Validation (optional)

- Expected artifacts (as applicable):
  - Aspire `AppHost` includes a Keycloak resource for local development.
  - Apps/APIs authenticate via OIDC/OAuth 2.0 (Keycloak locally, Entra ID in Azure).

- Validate success:
  - Run the `AppHost` locally and complete an authenticated user flow.
  - `dotnet test`

## References (optional)

- https://www.keycloak.org/
- https://aspire.dev/integrations/security/keycloak/
- https://learn.microsoft.com/entra/msal/dotnet/microsoft-identity-web/
- https://learn.microsoft.com/aspnet/core/security/authentication/azure-active-directory/?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0
