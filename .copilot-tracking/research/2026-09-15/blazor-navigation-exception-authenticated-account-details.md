---
title: Blazor navigation exception and authenticated Account Details access
description: Evidence-based research into Account Details sign-in redirects, platform scope hierarchy, and operator access failures.
ms.date: 2026-09-15
ms.topic: troubleshooting
---

## Prior investigation

Date: 2026-09-15
Scope: Research only. No product files modified.

## Questions

- Why can a user who is already logged in, using a normal navigation link, be sent to sign-in from Account Details?
- Can the required scope/token check fail for an authenticated user?
- Is a race or expired token likely?
- Did recent HTTPS or managed-host changes cause the behavior?

## Control path and evidence

1. `src/TNC.Trading.Platform.Web/Components/Layout/PlatformNavItem.razor:1` renders a standard Blazor `NavLink`; there is no explicit sign-in navigation or force-load behavior in the link.
2. `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:23` supplies `/account-details` to that nav item. Normal client navigation therefore instantiates the page without a browser-level request to the sign-in endpoint first.
3. `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:1` applies the Viewer authorization policy, then `:84-90` runs `EnsureRequiredScopesAsync` for `platform.viewer`, runs it again for `platform.operator`, and only loads the presenter when the viewer check succeeds. The second check controls only `canRefresh`, but it still redirects on failure.
4. `src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs:16-33` first reads the Blazor authentication state. An authenticated principal does not bypass token validation: the provider is still called, and both `PlatformScopeChallengeRequiredException` and `InvalidOperationException` invoke `NavigateToSignIn`.
5. `src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs:46-58` calls `NavigationManager.NavigateTo(..., forceLoad: true)`. During component initialization this is an intentional navigation away from the current render; depending on the Blazor hosting/rendering mode and timing, the framework can surface this as a `NavigationException`. The redirect itself is not proof that the original nav link was a sign-in link.
6. `src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:21-28` obtains `access_token` from the current HTTP authentication ticket. Missing token throws `InvalidOperationException`.
7. `src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:30-43` rejects an unreadable, expired, or not-yet-valid JWT through `HasUsableSessionToken`, audits the failure, and throws `PlatformScopeChallengeRequiredException`.
8. `src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:45-63` compares required scopes with effective token scopes and throws when any are absent. `GetGrantedScopes` can infer viewer/operator/admin scopes from valid role claims, but only after the token passes validity checks (`:66-99`).
9. `src/TNC.Trading.Platform.Web/Authentication/PlatformTokenScopeEvaluator.cs:8-24` requires a JWT readable by `JwtSecurityTokenHandler`, `ValidFrom <= DateTime.UtcNow`, and `ValidTo > DateTime.UtcNow`. There is no clock-skew allowance and no refresh/token-exchange operation.
10. `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:53-74` uses the cookie as the default authenticate/sign-in/challenge scheme and registers OIDC separately. `:94-99` sets `SaveTokens = true`; therefore the access token is expected to be persisted in the cookie ticket, not silently reacquired later.
11. `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:112-123` requests `openid` plus configured required scopes during OIDC sign-in. `:127-167` adds a per-challenge requested scope, login hint, and prompt to the OIDC request.
12. `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs:22-67` turns the coordinator redirect into an OIDC challenge. `platform:scope` is carried in authentication properties and later used by the OIDC redirect event.
13. `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:21-35` configures the Web app with `platform.viewer` as its baseline required scope and Keycloak client/audience values. `:62-78` keeps the Web provider Keycloak even when API auth settings are varied.

## Concrete authenticated failure modes

- The cookie principal is authenticated but `GetTokenAsync("access_token")` returns null/empty, for example an old cookie created before `SaveTokens`, a cookie issued by a different auth flow, or a ticket whose token was not persisted.
- The saved token is malformed or not a JWT readable by `JwtSecurityTokenHandler`.
- The token is expired. Because validity uses `DateTime.UtcNow` with no skew, normal expiry boundary timing is enough to fail. A long-lived sliding cookie does not renew the saved access token.
- The token is not yet valid, including a clock mismatch between Keycloak and the Web process.
- The token is valid but lacks `platform.viewer` or `platform.operator`. Role-derived inference can cover this only when the valid token contains the configured role claims and the Blazor principal also has matching roles.
- The page’s second operator check can fail for a viewer who is fully authorized for the page. That failure is expected to disable refresh, but the current coordinator redirects the whole page instead.
- `IHttpContextAccessor.HttpContext` is unavailable at the time a component-side token check runs, producing `InvalidOperationException`. This is a hosting/lifecycle sensitivity, not an authorization failure.
- The page can run while the authentication state and HTTP cookie/token ticket are temporarily inconsistent after sign-in, reconnect, circuit creation, or cookie renewal. The current code performs no retry or state-change subscription.

## Tests and gaps

Existing unit coverage is explicit:

- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformNavigationAccessCoordinatorTests.cs:46-80` verifies an authenticated user with a missing elevated scope is redirected.
- `:84-111` verifies an authenticated session with no token is redirected.
- `:145-174` verifies a valid token with a valid required scope proceeds.
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAccessTokenProviderTests.cs:25-247` covers missing, valid, missing-scope, expired, not-yet-valid, and malformed-token scenarios, including cases where role claims would otherwise satisfy the scope.
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:11-37` uses `ManagedAppHostFixture`, disables persistent Keycloak state, and exposes managed Web/API/Keycloak endpoints.
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs:35-70` proves a real authenticated session can load `/configuration` and later sign out, but does not navigate from an authenticated browser to `/account-details` and inspect the resulting page/redirect.

Important gap: no closed-box test proves the real Keycloak cookie’s saved access token remains available to a Blazor circuit during ordinary `NavLink` navigation, near expiry, after reconnect, or after a managed-host HTTPS endpoint change. There is also no Account Details functional test for a viewer who has `platform.viewer` but not `platform.operator`; current page code would redirect rather than render with refresh disabled.

## Ranked root causes

1. **Most likely: deliberate redirect caused by the second operator check.** Account Details is viewer-authorized, but initialization requires operator scope to decide refresh. A logged-in viewer or a token without `platform.operator` is redirected by design. This best fits “already logged in + normal link + unexpected sign-in” because the link itself is ordinary and the redirect originates inside initialization.
2. **Likely: saved access token is stale/expired while the cookie remains authenticated.** Cookie sliding expiration does not imply access-token renewal. The strict `ValidTo > UtcNow` check and no refresh path make this a realistic production condition, especially after a long-lived circuit/session.
3. **Plausible: cookie/token ticket and Blazor authentication state are temporarily out of sync.** The provider depends directly on `HttpContext` and the saved token during component initialization. A reconnect or auth transition can expose a missing context/token; there is no retry.
4. **Possible but less likely: token scope/claim mapping mismatch.** Keycloak must issue the custom platform scopes/roles, and the Web app expects role claim type `role`. A valid token with only viewer scope will still fail the operator check; a role claim mapping issue can also prevent inference.
5. **Unlikely as the primary cause: recent HTTPS or managed-host change.** `RequireHttpsMetadata` is development-sensitive at `PlatformWebAuthenticationServiceCollectionExtensions.cs:99`, while the managed fixture now consistently uses `ManagedAppHostFixture` and real Keycloak in `RealAuthenticationFunctionalTestFixture.cs:11-37`. HTTPS/endpoint changes would more naturally break OIDC callback, authority metadata, or cookie transport and prevent login, rather than produce a successful authenticated principal followed by a local scope redirect. They remain a secondary possibility if logs show missing token or callback ticket changes after the hosting refactor.

## Recommended changes

- Change Account Details initialization so the viewer gate controls page access, while operator acquisition controls only `canRefresh`. An operator-scope failure should leave the page rendered with refresh disabled, not force a whole-page sign-in redirect. This is the smallest behavior change aligned with the UI’s existing `canRefresh` condition.
- Separate “required to enter page” from “optional capability” in the coordinator API, or add a non-redirecting capability check that returns false for missing operator scope.
- Add an access-token refresh/reacquisition strategy for real OIDC sessions, or explicitly treat expired saved tokens as a session reauthentication event with a diagnostic reason. Consider bounded clock skew if provider/token clock drift is expected.
- Avoid doing a forced full navigation directly from `OnInitializedAsync` where possible; use a render-safe redirect mechanism or an authorization redirect boundary so expected redirects do not appear as unhandled `NavigationException` failures.
- Add unit coverage for Account Details as an authenticated viewer: viewer check succeeds, operator check fails, page renders, `canRefresh == false`, and no sign-in navigation occurs. Add tests distinguishing page-required and capability-optional checks.
- Add real-auth functional coverage: authenticate as viewer, request `/account-details` via a normal browser navigation, assert no sign-in redirect and refresh is absent/disabled; authenticate as operator and assert refresh is available. Capture response/circuit logs for the token state.
- Add diagnostics around token acquisition that record provider, request path, token presence, JWT validity result, expiry delta, required scopes, granted scopes, and exception category, without logging raw tokens. Correlate with `OnTicketReceived` and sign-in audit events.

## Recommended diagnostic sequence

1. Reproduce with a real logged-in viewer and inspect whether the redirect query contains `scope=platform.operator`; that identifies the second check immediately.
2. Correlate Web logs for “missing delegated scopes”, “does not contain a usable delegated access token”, or “does not contain an access token” from `PlatformAccessTokenProvider`.
3. Inspect the sign-in callback audit/log event for saved-token presence and granted scopes; compare the token expiry to the reproduction timestamp without exposing the token.
4. Compare a fresh login versus the same browser after cookie sliding/reconnect. If fresh login works and later navigation fails, expiry/staleness is more likely than HTTPS configuration.
5. Compare direct HTTP GET `/account-details` after login with in-circuit `NavLink` navigation. If only in-circuit navigation fails, investigate Blazor lifecycle/`HttpContextAccessor` availability and redirect timing.
6. Review the managed-host change only after the above: verify Web authority, callback URL scheme/host, Keycloak reachability, cookie `Secure` behavior, and that `SaveTokens` remains active in the deployed Web process.

## Conclusion

The new fact does not indicate that the normal link initiated sign-in. It is consistent with the page’s own initialization redirect. The highest-confidence root cause is the unconditional operator-scope check on a viewer page, with stale/expired saved access tokens the main alternative. HTTPS or managed-host changes are not supported as the primary cause by the current code evidence, but a missing saved token or changed callback ticket should be confirmed through the proposed logs and a real authenticated Account Details functional test.

## 2026-09-15 operator-only evidence update

### Question

The reported user was a platform operator, rather than a viewer. The required
question is whether an operator is expected to imply viewer access and whether
an operator-only login can fail the first `platform.viewer` check on Account
Details.

### Verified hierarchy

The intended and implemented authorization hierarchy is `Viewer < Operator <
Administrator`:

* The Web and API `PlatformViewer` policies allow `Viewer`, `Operator`, or
	`Administrator`; `PlatformOperator` allows `Operator` or `Administrator`.
* Account Details reads use the Viewer policy; Account Details refresh uses the
	Operator policy.
* The Keycloak realm assigns `local-operator` only the `Operator` realm role.
	The API/Web role policies nevertheless grant that role viewer-level access.
* The Web scope evaluator infers `platform.viewer` from JWT `Viewer`,
	`Operator`, or `Administrator` claims, and infers `platform.operator` from
	JWT `Operator` or `Administrator` claims.
* The Web access-token provider repeats the same inference from the cookie
	principal after validating the access token.

An operator role is therefore intended to imply viewer capability. A valid
operator JWT or cookie principal with the mapped `role: Operator` claim passes
the initial `platform.viewer` check even if its explicit `scope`/`scp` claim
contains only `platform.operator`.

### Correction to prior conclusion

The previous ranking made the page's second operator-scope check the most
likely cause. That is incorrect for this new operator-only report. The first
viewer check is executed before the second operator check, and an intact
operator role satisfies viewer inference. The second check remains a separate
viewer-only defect: it invokes the redirecting coordinator to calculate an
optional refresh capability, so a Viewer can be sent to sign-in instead of
viewing Account Details with refresh unavailable. It cannot explain an
operator failing the first viewer check.

### Outstanding evidence

Git-history and current functional-test evidence are being examined to identify
whether recent changes can cause an otherwise valid operator session to lose
the usable token or mapped role needed by the first check.

## Final findings

### Intended semantics and hierarchy

`platform.viewer`, `platform.operator`, and `platform.admin` are delegated
scope names, while `Viewer`, `Operator`, and `Administrator` are identity
provider role names. The intended capability hierarchy is:

```text
Viewer < Operator < Administrator
```

`Operator` is explicitly expected to imply viewer-level access. This is a
role/capability hierarchy, not a claim that `platform.operator` alone is a
syntactic alias for `platform.viewer`.

* `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs:57-117`
	defines the three roles, policies, and scopes.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:24-40`
	grants `PlatformViewer` to Viewer, Operator, and Administrator, and grants
	`PlatformOperator` to Operator and Administrator. The API registration has
	the identical hierarchy at
	`src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs:16-31`.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:49-53`
	puts Account Details history behind `PlatformViewer` and refresh behind
	`PlatformOperator`.
* `docs/003-authentication-and-authorisation/technical-specification.md:29-45`
	specifies named roles, separate delegated scopes, baseline viewer scope at
	sign-in, and higher scopes only for privileged areas. Its implementation
	design at `:59-67` repeats the baseline-plus-elevation model.
* `docs/wiki/api-reference.md:19-22,55-60` describes Account Details history
	as viewer-capable and refresh as Operator-only. The wiki is therefore
	consistent with the current API policies.

### Keycloak configuration and test identities

The imported realm defines three independent client scopes at
`src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json:21-34`.
It assigns `platform.viewer` as the Web client's default client scope and
`platform.operator`/`platform.admin` as optional scopes at `:45-71`. Its
`platform-role-claim` mapper emits realm roles under the configured `role`
claim in access and ID tokens at `:100-116`.

The seeded `local-operator` deliberately has only the `Operator` realm role,
not an additional `Viewer` role, at `:219-235`. `local-admin` and
`local-viewer` similarly have one role each at `:201-217` and `:237-253`.
This proves the local configuration intends policy/evaluator inheritance, not
multiple role assignments.

The normal AppHost keeps Keycloak persistent unless
`AppHost:UsePersistentKeycloakState` is explicitly `false`:
`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:5-43`.
It imports the realm with `WithRealmImport("./Realms")` at `:38`. A persisted
container created before the role mapper/default-scope configuration can
therefore retain stale identity configuration instead of reflecting the JSON
import. The current real-auth functional fixture disables that persistence at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:18-27`.

### What the Web app actually grants and infers

The OIDC registration sets `SaveTokens = true`, configures `role` as the role
claim type, requests `openid` plus configured required scopes, and always
includes configured baseline scopes even during elevation at
`src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:94-147`.
The AppHost supplies `platform.viewer` as that required baseline and supplies
`role` as the role claim type at
`src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:17-32,64-78`.

`PlatformTokenScopeEvaluator` reads `scope` and `scp`, then infers:

* `platform.viewer` for a JWT `Viewer`, `Operator`, or `Administrator` role
* `platform.operator` for a JWT `Operator` or `Administrator` role
* `platform.admin` for a JWT `Administrator` role

The precise implementation is
`src/TNC.Trading.Platform.Web/Authentication/PlatformTokenScopeEvaluator.cs:25-63`.
`PlatformAccessTokenProvider` repeats that inference from the cookie principal
after token validity is established at
`src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:20-63,66-99`.
`PlatformOperatorContextAccessor` independently computes viewer/operator/admin
capability from the Blazor principal's configured role claims at
`src/TNC.Trading.Platform.Web/Authentication/PlatformOperatorContextAccessor.cs:13-46`.

Thus, current code does grant/infer viewer from Operator. A valid JWT with
`role: Operator` passes a required `platform.viewer` check even if its explicit
`scope` claim contains only `platform.operator`. A cookie principal with only
the `Operator` role also supplies the same fallback.

### Exact Account Details behavior and correction

`src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:1-5`
protects the page with `PlatformViewer`. Its initialization at `:82-90` runs a
viewer check first and then an operator check only to decide whether refresh is
available. Both checks invoke the same redirecting coordinator.

The coordinator redirects to sign-in for an unauthenticated Blazor principal,
`PlatformScopeChallengeRequiredException`, or `InvalidOperationException` at
`src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs:13-33`.
It builds a full sign-in navigation at `:35-58`.

The prior claim that the second operator check was the most likely cause is
**contradicted for this report**. An operator failure at the *initial*
`platform.viewer` check happens before that second check. With intact operator
claims it also passes the first check. The second check remains a real but
separate defect: a legitimate Viewer can be sent to sign-in while the page is
only determining whether to show the optional Refresh control. It cannot be
the cause of an operator-only failure of the first check.

### Exact first-check failure conditions for an operator login

For an authenticated user whose Blazor principal reports `Operator`, the first
viewer check redirects only when `PlatformAccessTokenProvider` cannot return a
usable token:

1. `IHttpContextAccessor.HttpContext` is unavailable, producing
	 `InvalidOperationException` at
	 `src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:13`.
2. The cookie ticket has no saved `access_token`, producing
	 `InvalidOperationException` at `:20-25`.
3. The token is blank, malformed, expired, or not-yet-valid. The evaluator
	 requires a readable JWT with `ValidFrom <= UtcNow` and `ValidTo > UtcNow`,
	 with no clock-skew allowance, at
	 `src/TNC.Trading.Platform.Web/Authentication/PlatformTokenScopeEvaluator.cs:8-23`.
	 The provider deliberately rejects such a token before any role fallback at
	 `src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:27-44`.
4. The token has no direct viewer scope and no usable `role: Operator` claim,
	 and `HttpContext.User` also has no configured `role: Operator` claim. This
	 can occur with a stale/different Keycloak realm, a missing/renamed protocol
	 mapper, or a Blazor principal and HTTP-context principal that do not carry
	 the same claims. An explicit `platform.operator` scope alone does not add
	 viewer; the role inference does.

The first three conditions are sufficient even when the Blazor route policy
accepts the operator. The page policy consumes the cascading principal, while
the scope provider requires the HTTP context and the cookie's saved access
token, creating a distinct failure boundary.

### Likely root cause and selected remediation

The evidence rules out a missing Viewer role as the likely root cause for a
correctly imported `local-operator`. The most likely local cause is a stale
persisted Keycloak realm/session cookie that does not contain the current
default scope or `role` mapper, followed by an expired or missing saved access
token. Both fit an authenticated operator reaching Account Details and then
being challenged by its initial scope check.

Selected remediation:

1. Reset the local persistent Keycloak state, reimport the checked-in realm,
	 clear the platform session cookie, and sign in again as `local-operator`.
	 This restores the configured default viewer scope and role mapper without
	 weakening authorization.
2. Add redacted diagnostic logging around the first-check failure category:
	 HTTP-context availability, token presence, JWT validity/expiry delta,
	 token roles/scopes, principal roles, and missing scopes. Do not log raw
	 token material.
3. Change Account Details' second, optional operator capability check to a
	 non-redirecting capability query. Keep the initial viewer check redirecting.
	 This fixes the actual viewer-only redirect defect without changing the
	 operator hierarchy or concealing first-check session failures.

If a fresh reimported operator session still fails, capture the redacted
diagnostic category before changing role or scope semantics. The evidence then
points to missing token persistence or Blazor HTTP-context lifecycle, not to
operator-versus-viewer authorization.

### Test gaps and tests to add

Existing tests do not prove the required scenario:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs:18-42`
	only proves Viewer denial from `/configuration`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs:38-66`
	authenticates an operator for `/configuration`, not `/account-details`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:10-25`
	renders Account Details as a viewer but does not assert that the optional
	operator check avoided sign-in navigation.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TestAuthenticationTokenFactory.cs:57-60`
	always adds `platform.viewer`; existing operator tests therefore cannot prove
	viewer inference from an operator-only token.

Add these tests:

* Unit-test `PlatformTokenScopeEvaluator.ReadEffectiveScopes` with a valid JWT
	containing `role: Operator` and only `platform.operator`, asserting both
	effective operator and viewer scopes.
* Unit-test `PlatformAccessTokenProvider.GetAccessTokenAsync` with a valid
	operator-role JWT and principal that both omit direct viewer scope, asserting
	a `platform.viewer` request succeeds with no redirect/audit failure.
* bUnit-test Account Details as Viewer: history loads, Refresh is absent, and
	`TestNavigationManager.LastNavigationUri` remains null. This protects the
	selected non-redirecting optional-capability remediation.
* Real-Keycloak functional browser test: sign in as `local-operator`, navigate
	normally to `/account-details`, assert the account-details surface renders
	and no `/authentication/sign-in` redirect occurs. Assert the Refresh control
	is available when the protected API response is supplied.
* Real-Keycloak API integration test: an `Operator` token reaches the viewer
	Account Details `GET` endpoint and reaches the operator refresh `POST`
	endpoint. This closes the API side of the hierarchy matrix.

### Recent-change assessment

The Account Details page and both checks were introduced by `8863cd9` on
2026-08-07; `git blame` attributes its viewer and operator checks to that
commit. The strict valid-token-before-role-fallback behavior was added by
`13d3842` on 2026-05-07. It is intentional security behavior and predates
Account Details.

Commit `3ef847e` on 2026-09-15 changed AppHost Keycloak authority wiring from
a fixed localhost URL to the Keycloak endpoint reference at
`src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:177-201`, and
broadened local realm redirect URIs/origins at
`src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json:51-63`.
It did not change client scopes, user role assignments, the role mapper, the
scope evaluator, the access-token provider, or Account Details. It can affect
OIDC issuance/callback addressing and needs the proposed regression test, but
the code evidence does not make it the direct cause of an operator failing
viewer inference.

The same commit's real-auth fixture now opts out of persistent Keycloak state,
which makes tests more repeatable and does not cause a persistent local
developer realm to become stale. No uncommitted product or test changes were
present in the inspected authorization files.
