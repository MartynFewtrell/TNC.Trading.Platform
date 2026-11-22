# Research: Keycloak randomized localhost redirects

## Question
Determine the exact Keycloak redirect URI and web-origin wildcard syntax for randomized localhost ports, especially whether `https://localhost:*/*`, `https://localhost/*`, or another form matches `https://localhost:<random>/signin-oidc`.

## Local evidence
- `src/TNC.Trading.Platform.AppHost/KeycloakRealmSeeder.cs` currently seeds `https://localhost:*/*` in both redirect URIs and web origins for the `tnc-trading-platform-web` client.
- The same seeder also seeds `http://localhost:*/*` entries.
- The web application callback is `/signin-oidc`, so the runtime callback has the form `https://localhost:<random>/signin-oidc`.

## Official Keycloak evidence
Source: https://www.keycloak.org/docs/latest/server_admin/#_clients-uris

- Keycloak documents valid redirect URI matching as exact, case-sensitive matching by default.
- Redirect URI wildcards may be used only at the end of the URL pattern, with the documented example `http://host.com/path/*`.
- The full wildcard `*` permits any HTTP or HTTPS redirect URI and is explicitly discouraged in production.
- The same section describes Web Origins as URL patterns used for CORS; it does not document a port-position wildcard syntax.
- Keycloak's security guidance says redirect URIs should be as specific as feasible because vague patterns can enable impersonation/open-redirect risks.

## Interpretation
- `https://localhost:*/*` is not the documented wildcard form: the `*` is in the port/authority portion, not at the end of the URL. It should not be relied on to match `https://localhost:<random>/signin-oidc`.
- `https://localhost/*` has no port wildcard. It matches the default HTTPS origin/port representation, not an arbitrary explicit randomized port.
- There is no documented Keycloak pattern that wildcard-matches an arbitrary port while retaining a specific redirect path.
- `https://localhost:<port>/*` is valid for a known fixed port, but cannot represent an unknown randomized port.
- `*` would match arbitrary HTTP/HTTPS redirect URIs, but is too broad for a repository-compatible recommendation and is explicitly discouraged for production use.

## Minimal repository-compatible recommendation
Keep fixture-randomized callback support out of the static realm import. At fixture startup, after the randomized HTTPS listener is allocated, update the `tnc-trading-platform-web` client through the Keycloak Admin REST API (or equivalent seeder/admin client) with the exact callback URI `https://localhost:<allocated-port>/signin-oidc` and, if required by the test flow, the exact origin `https://localhost:<allocated-port>`. Remove the dynamically registered URI during fixture teardown or overwrite it on the next isolated realm/client setup.

This preserves exact redirect validation and avoids relying on undocumented port wildcards. If the test harness cannot update the client dynamically, use a stable HTTPS test origin/port or a dedicated isolated test realm/client rather than broadening the client to `*`.

## Status
Complete. No product or test files were edited.
