---
description: Research account-preference persistence, trading startup orchestration, and IG verification boundaries
ms.date: 2026-08-31
ms.topic: reference
---

# Account Preferences and IG Verification Research

## Status

Complete as of 2026-08-31.

## Research scope

* Determine what the account preference controls.
* Identify where desired database intent and observed broker state are stored.
* Trace where trading or broker-dependent operation starts.
* Inventory the real IG state APIs and existing adapters that can observe account,
  session, and position state.
* Identify the safest verification point before trading.
* Classify verification as account-level, session-level, or position-level.
* Define the required meaning of unavailable state, failed verification, and
  mismatched state.

## Hypothesis outcome

The account preference is IG's account-level `trailingStopsEnabled` setting. The
current SQL model retains broker observations but does not own a durable desired
preference. The safest future trading gate should sit after session
authentication and account selection, but before any order-producing workflow,
with a fresh account-level provider read and fail-closed mismatch handling.

## Evidence

### Preference meaning and authority

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferences.cs:1-6`
  models one provider value, `TrailingStopsEnabled`, plus status and observation
  time. It has no desired-value or account-identity field.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-18`
  maps reads and writes to IG account preferences.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:69-90`
  calls `accounts/preferences` version 1, sends `trailingStopsEnabled` on PUT,
  and parses
  the same Boolean on GET.
* `docs/wiki/operator-guide.md:565-594` describes the control as live trailing
  stops for the configured account. The last confirmed provider value remains
  authoritative, and changing the UI selection is only pending until save and
  readback succeed.
* [IG `/accounts/preferences` reference](https://labs.ig.com/reference/accounts-preferences.html)
  defines GET version 1 as returning account preferences and PUT version 1 as
  updating account preferences.

### Persistence and intent

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:49-80`
  persists platform environment, broker environment, trading schedule, retry,
  notification, and credential changes. It has no trailing-stops field.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs:3-13`
  confirms the runtime configuration contract also has no desired
  trailing-stops value.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:21-51`
  sends the requested Boolean directly to IG, then performs a fresh GET. Exact
  equality produces a confirmed observation; a differing Boolean becomes
  `NotApplied`.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:21-39`
  appends only successful provider reads as observations. Failures become
  operational events, not preference rows.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/TrailingStopsPreferenceObservationEntity.cs:3-26`
  defines append-only observation data. It includes a nullable `AccountId`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:10-27`
  maps observation fields but never assigns `AccountId`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:29-55`
  queries
  history by platform and broker environment, not account.
* `docs/wiki/runtime-behavior.md:728-745` states that IG is authoritative, local
  observations are not a cache or fallback, and unknown state is not stored as
  false.

### Startup and broker supervision

* `src/TNC.Trading.Platform.Api/Program.cs:21-31` initializes SQL/configuration,
  then executes one blocking authentication reconciliation before the request
  pipeline starts.
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:29-44`
  performs another reconciliation in hosted-service startup and repeats the
  supervised tick in the background.
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:67-74`
  resolves the reconciliation
  handler for each tick.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:159-180`
  gates reconciliation by trading schedule and the Test-to-Live restriction.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:401-447`
  authenticates and marks runtime state active after login evidence
  succeeds.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:28-50`
  treats an unexpired `Active` session as active without another broker call.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:52-60`
  detects expiry from the locally stored interval.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:764-768`
  obtains that interval from `PlatformAuthSimulationSettings`, not IG.
* `docs/wiki/runtime-behavior.md:63-76` describes the startup control loop, and
  `docs/wiki/runtime-behavior.md:141-171` shows its concrete SQL initialization, reconciliation, IG session,
  `/accounts`, and `/positions` sequence.

### Existing IG observations

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:24-82`
  creates an IG Demo session and requires `currentAccountId`, `CST`, and
  `X-SECURITY-TOKEN` before reporting authentication success.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:108-153`
  reads `/accounts` version 1 and `/positions` version 2. It exposes the
  preferred-or-first account ID/name/balance and the active account's open
  position count as optional proof.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:719-744`
  explicitly leaves the session active when proof collection fails. Proof data
  is durable observation, not an activation prerequisite.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:35-80`
  separately creates a session and retrieves a validated account list,
  including account ID, status, type, preferred flag, balance, currency, and
  transfer capabilities.
* [IG REST API guide](https://labs.ig.com/rest-trading-api-guide.html) states that
  v1/v2 login returns a client `CST` and an `X-SECURITY-TOKEN` identifying the
  current account, and that subsequent requests carry both. It also says
  `/positions` returns positions for the active account.
* [IG `/positions` reference](https://labs.ig.com/reference/positions.html)
  defines GET version 2 as returning all open positions for the active account.
  Its documented failures include missing/invalid client or account tokens and
  missing/disabled preferred account.
* [IG `/accounts` reference](https://labs.ig.com/reference/accounts.html) defines
  GET version 1 as returning all accounts for the logged-in client.

### Current trading surface

* A source search under
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/`
  found no order or position write. Provider writes are limited to session
  creation and account-preference update; `/positions` is GET-only.
* `docs/wiki/application-overview.md:185-190` lists order submission, trade
  lifecycle management, end-of-day flattening, and broader risk controls as
  future work.
* `docs/wiki/operator-guide.md:35-36` and
  `docs/wiki/runtime-behavior.md:20-23` explicitly say the account-preference
  feature does not authorize real orders or monetary exposure.

## Findings

### What the preference controls

`trailingStopsEnabled` is an IG account preference. In this repository it is a
Boolean operator control for the configured IG Test account. It is not a
strategy setting, per-order request, per-position flag, or permission to trade.
The available official reference confirms the account-preference resource and
field contract but does not explain whether IG uses the flag as an entitlement,
feature enablement, or default behavior for later order requests. That narrower
business meaning remains an external documentation gap.

### Where database intent is stored

There is no durable desired trailing-stops intent in the current database or
Application configuration contract. The only local desired value is the
request/UI pending value during an explicit update. SQL stores:

* the operator-managed environment, schedule, retry, notification, and protected
  credential configuration
* successful provider observations and confirmed-update observations
* operational failure events

The observation table is evidence, not desired state. Its nullable `AccountId`
column is currently always left unset by the mapper. Consequently, neither the
desired value nor the account to which an observed value applied can currently
be recovered reliably from SQL.

If automated pre-trading enforcement is required, add an explicit desired-state
record keyed at least by broker environment and IG account ID. Do not reinterpret
the latest observation as intent. Record desired value, actor, change time, and
version; keep observations separate as actual state.

### Where trading starts

No automated trading, order submission, position creation, amendment, closure,
or strategy runtime exists in current product code. Startup begins broker-auth
supervision:

1. `Program.cs` applies migrations/configuration/retention.
2. `Program.cs` runs a blocking authentication reconciliation.
3. `PlatformAuthenticationSupervisor.StartAsync` runs another tick.
4. The hosted service repeats reconciliation every second.

The current `Active` state means session authentication succeeded. It does not
mean account preferences, account details, or positions were all freshly read;
proof collection and daily Account Details capture are intentionally best-effort.

### What can observe real IG state

| Level | Existing call or adapter | Observable state | Limitation |
|-------|--------------------------|------------------|------------|
| Session | `POST /session` v2 through `IgBrokerAuthenticationGateway` | `currentAccountId`, CST, account token, Lightstreamer endpoint | Tokens are discarded after one adapter operation; expiry is locally assigned rather than read from IG. |
| Client/account list | `GET /accounts` v1 through the auth proof path or `IgAccountDetailsGateway` | Account IDs, names, status, type, preferred flag, balances, currency, transfer flags | Auth proof chooses preferred-or-first account without proving it equals `currentAccountId`; Account Details starts a separate session. |
| Account preference | `GET /accounts/preferences` v1 through `IgAccountPreferencesGateway` | `trailingStopsEnabled` | The adapter starts a separate session, does not parse `currentAccountId`, and does not attach account ID to the result or SQL observation. |
| Account preference write | `PUT /accounts/preferences` v1 through `IgAccountPreferencesGateway` | Acknowledgement followed by authoritative GET | Correctly avoids blind write retry, but the session/account identity is still not surfaced. |
| Position | `GET /positions` v2 through `IgBrokerAuthenticationGateway` | Open positions for the active account | The current inward model retains only a count, not deal IDs or position details; failure is non-fatal to `Active`. |

The official IG REST guide establishes why the account binding matters: for
v1/v2 authentication, CST identifies the client while `X-SECURITY-TOKEN`
identifies the current account. The same guide and `/positions` reference state
that positions are returned for the active account.

### Verification level

The trailing-stops value itself is **account-level**, scoped through a
**session-level** current-account token. It is not position-level.

A safe trading-readiness decision is composite:

* Session-level: a usable authenticated session exists and its
  `currentAccountId` equals the explicitly configured target account.
* Account-level: that account exists, is enabled/eligible, and a fresh
  `GET /accounts/preferences` for the same account/session equals durable desired
  intent.
* Position-level: current open positions are successfully reconciled before
  starting or resuming automated trading. This does not verify the preference;
  it prevents new automation from ignoring pre-existing or externally changed
  exposure.

### Safest verification point

Use two stages:

1. Prime readiness immediately after successful login and account selection,
   before the reconciler publishes a trade-capable state. Perform account,
   preference, and position reads using the same session/account context.
2. Enforce readiness again at the future order-command boundary immediately
   before any provider order submission. Require a fresh-enough verification
   snapshot bound to the target account and session generation. Invalidate it
   on re-authentication, token rejection, account switch, schedule exit,
   configuration change, or expiry.

Startup-only verification is insufficient because the account preference,
active account, positions, or session can change after startup. UI load is also
not a safety boundary. The order-producing Application use case should own the
fail-closed policy; the IG adapter should obtain provider state and submit the
order only after that policy authorizes it.

The present auth reconciler is the closest startup hook, but it must not reuse
the current optional proof contract unchanged. `Active` presently tolerates
missing account/position proof and the account-preference adapter creates a
different session. A future trade-ready state needs stronger, account-bound
evidence than the existing auth-active state.

### Failure and mismatch meaning

| Condition | Meaning | Required trading effect |
|-----------|---------|-------------------------|
| Session creation fails, token is rejected, or session identity is unavailable | Session state is unknown or unusable | Mark broker-dependent operation degraded, invalidate verification, place no order, and use bounded re-authentication/retry. |
| `currentAccountId` differs from configured target account | Wrong account context | Treat as a hard safety block, place no order, record both non-secret account IDs, and require explicit account correction or verified account switching. |
| Preference GET times out, is rate-limited, unavailable, unauthorized, or malformed | Actual preference is unknown | Fail closed for trading that requires the invariant. Do not infer `false`, use history as current state, or issue an automatic PUT merely because the read failed. |
| Observed preference differs from durable desired intent | Confirmed configuration drift | Block new trading, record a mismatch event, notify as policy requires, and expose remediation. Do not silently continue. Automatic repair should occur only if separately authorized; it must use one PUT followed by an exact GET, matching the existing update contract. |
| Account list cannot be read or target account is missing/disabled | Account eligibility is unknown or invalid | Block new trading independently of auth status. |
| Positions cannot be read | Exposure is unknown | Block starting or resuming strategies and new orders until reconciliation succeeds. |
| Positions exist but disagree with durable trade/strategy state | Exposure mismatch | Enter a stopped/reconciliation-required state. Do not flatten, adopt, or duplicate positions automatically without an explicit recovery policy. |
| No positions exist and durable state expects none | Position reconciliation passes | This satisfies the position-level prerequisite only; session, account, preference, market-data freshness, run-state, and risk gates still apply. |

These outcomes should not all collapse into authentication failure. Preserve
separate session, account-preference, account-eligibility, and exposure states so
operators can see why trading is blocked and retry only the failed operation.

## Gaps and clarifying questions

* No product requirement currently defines the desired
  `trailingStopsEnabled` value or says whether the platform should only verify it
  or automatically enforce it.
* No configured target IG account ID exists in the platform configuration
  snapshot. “Configured account” is therefore ambiguous when one login owns
  multiple accounts.
* The account-preference response and observation omit account identity despite
  the SQL entity already having a nullable `AccountId` field.
* The auth adapter's preferred-or-first account can differ from the session's
  `currentAccountId`; no invariant or test compares them.
* Provider tokens are operation-scoped and discarded, so separate adapters may
  authenticate into different active-account contexts. There is no shared
  session generation to bind verification to a later order.
* The local session expiry is based on configured simulation lifetime rather
  than a provider-observed expiry or active validity probe.
* The current position proof retains only a count. It cannot reconcile deal IDs,
  instrument, direction, size, stops, limits, or ownership against future
  durable trade intent.
* The official IG account-preferences reference available during this research
  names the endpoint and operations but does not document the business effect of
  `trailingStopsEnabled` in enough detail to decide when a strategy truly
  requires it.
* Product input is required on whether existing positions should always block
  startup, may be adopted after reconciliation, or may be closed through an
  explicit recovery workflow.

## Recommended next research

* Confirm with IG support or a current account-specific API schema exactly how
  `trailingStopsEnabled` affects order capabilities and whether it is supported
  uniformly for the intended account type and instruments.
* Exercise IG Demo with two linked accounts to prove which account a v2 login
  selects, whether `GET /accounts/preferences` follows the
  `X-SECURITY-TOKEN`, and how account switching changes that token.
* Define the durable target account ID and desired trailing-stops policy with the
  product owner before designing persistence.
* Research the official session account-switch operation and decide whether the
  platform will reject a wrong current account or explicitly switch and verify
  it.
* Define a position reconciliation contract with deal-level fields and recovery
  rules before implementing strategy startup or order submission.
* Define verification freshness, retry, notification, and manual-override rules
  for the future trade-readiness state.

## External references

* [IG REST Trading API guide](https://labs.ig.com/rest-trading-api-guide.html)
* [IG account preferences reference](https://labs.ig.com/reference/accounts-preferences.html)
* [IG accounts reference](https://labs.ig.com/reference/accounts.html)
* [IG positions reference](https://labs.ig.com/reference/positions.html)
* [IG session reference](https://labs.ig.com/reference/session.html)
* [IG Getting Started](https://labs.ig.com/getting-started.html)