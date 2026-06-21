# Handoff: Work Item 1 Task 1 — Documentation Specialist

## Target agent

Documentation Specialist

## Work item reference

`docs/006-ig-login/plans/002-real-ig-demo-connection-delivery-plan.md` — Work Item 1, Task 1 (Steps 1–3)

## Delivery context

- Branch: `006-ig-login`
- Baseline: `dotnet build` succeeds; 172/172 unit tests pass
- Both files are currently `Status: draft` and describe a simulation-only / foundation-only IG Test-environment outcome
- Plan `002` supersedes plan `001` and targets **real IG Demo connectivity** and **read-only proof-data retrieval**
- All existing `FR/NF/SR/DR/IR/TR/OR` requirement IDs must remain unchanged — this is an alignment update, not a requirement replacement

## Files to update

- `docs/006-ig-login/requirements.md`
- `docs/006-ig-login/technical-specification.md`

## Scoped edits — `requirements.md`

Apply all of the following changes. Do not restructure sections or renumber requirement IDs.

1. Change `Status: draft` → `Status: active`.

2. In section 1, Outputs list, add `plans/002-real-ig-demo-connection-delivery-plan.md` alongside the existing `plans/001-delivery-plan.md`.

3. In section 2.1 Background, append after the first paragraph:

   > This work package has been clarified to target real IG Demo connectivity rather than a simulation-only outcome. The platform must make real outbound calls to `https://demo-api.ig.com/gateway/deal`, establish a genuine authenticated session, and retrieve read-only proof data (such as account context and open positions) to prove real Demo connectivity safely, without placing trades.

4. In section 3.1 In scope, add two new bullet points:

   - Real outbound IG Demo REST API calls using a typed `HttpClient` registered for the Demo base URL `https://demo-api.ig.com/gateway/deal`.
   - Read-only proof-data retrieval after successful login (for example, account/session context or open positions snapshot) to prove real Demo connectivity without trade placement.

5. In section 3.2 Out of scope, add one new bullet point:

   - Simulation-only login paths — these are replaced by real IG Demo REST calls in this work package.

6. In section 11.1 Assumptions, update the first assumption to read:

   > A valid IG Demo account, API key, and network connectivity to `https://demo-api.ig.com/gateway/deal` are available for development and validation.

7. In section 11.2 Risks, add one new risk entry:

   - **External dependency risk**: the IG Demo API may reject, throttle, or be unreachable during development or validation.
     - **Mitigation**: deterministic automated tests use fake `HttpMessageHandler` implementations so `dotnet test` remains reliable; real-IG validation is explicit and opt-in, gated by user secrets or environment variables that are not committed to source control.

## Scoped edits — `technical-specification.md`

Apply all of the following changes. Do not restructure sections or renumber requirement IDs.

1. Change `Status: draft` → `Status: active`.

2. In section 1, update the Output field to add `plans/002-real-ig-demo-connection-delivery-plan.md`.

3. In section 2.1 Problem statement, prepend a new sentence before the existing opening:

   > The platform must make real authenticated calls to the IG Demo REST API (`https://demo-api.ig.com/gateway/deal`) to establish a genuine session and retrieve read-only proof data. The simulated login path is replaced by this work package.

4. In section 2.2 Assumptions, add two new assumptions at the end of the list:

   - A typed `HttpClient` named `IgDemoRestClient` targeting `https://demo-api.ig.com/gateway/deal` is registered via dependency injection and used exclusively in Test platform environments.
   - Live base URL calls remain blocked by the existing platform-environment guard; the Demo base URL is the only permitted outbound IG target in this environment.

5. In section 2.3 Constraints, add one new constraint:

   - A real IG Demo HTTP client must be registered as a typed `HttpClient`; the Demo base URL `https://demo-api.ig.com/gateway/deal` is the only permitted outbound IG target in Test platform environments. Live base URL calls remain blocked.

6. In section 3.1 Approach, update item 1 to read:

   > **Startup and maintenance login execution** — Replace the simulated login path with a real `POST /session` call to the IG Demo REST API. Schedule gating, retry timing, degraded transitions, and blocked-live behavior remain unchanged from the inherited auth foundation.

   Add a new item 5 after the existing four items:

   > **Read-only proof-data retrieval** — After successful login, issue one or more low-risk read-only queries (such as `GET /accounts` and/or `GET /positions`) to confirm real Demo data access. Proof query frequency is low and tied to meaningful runtime events (for example, immediately after a successful login) rather than a polling loop.

7. In section 5.1 Public APIs / Contracts, add a new internal contract row:

   | Area | Contract | Example | Notes |
   | ---- | -------- | ------- | ----- |
   | Internal | `IIgSessionClient` | `AuthenticateAsync`, `GetAccountsAsync`, `GetPositionsAsync` | Application-owned interface; implemented in Infrastructure as a typed `HttpClient` |

8. In section 5.3 Implementation Plan (technical steps), update Step 1 to read:

   > Replace simulated `IgAuthenticateResponse` creation with a real `IIgSessionClient.AuthenticateAsync` call; introduce the typed `HttpClient` registration targeting `https://demo-api.ig.com/gateway/deal`.

   Insert a new Step 1a immediately after Step 1:

   > Add proof-data models (`IgAccountsResponse`, `IgAccountSummary`, `IgPositionsResponse`, `IgPositionItem`) and proof-data query methods (`GetAccountsAsync`, `GetPositionsAsync`) to `IIgSessionClient`.

9. In section 5.5 Configuration, add one new configuration row:

   | Setting | Purpose | Default | Location |
   | ------- | ------- | ------- | -------- |
   | IG Demo base URL | Typed `HttpClient` base address for outbound IG calls | `https://demo-api.ig.com/gateway/deal` | DI registration in Infrastructure |

10. In section 8 Testing Strategy, update the Unit row Notes to add:

    > Include tests for IG REST request construction (JSON body, `X-IG-API-KEY` header, `Version` header), Demo base-URL selection, redaction of `X-IG-API-KEY`, `CST`, and `X-SECURITY-TOKEN`, and HTTP error-code classification. Opt-in real-IG tests must be guarded by user secrets or environment variables and must not run in default `dotnet test` execution.

## Validation checklist

After making all edits, confirm:

- [ ] Each file has exactly one H1 heading at the top
- [ ] Heading levels are in ascending order with no skipped levels
- [ ] All relative links (`../business-requirements.md`, `../systems-analysis.md`, `plans/001-delivery-plan.md`, etc.) still resolve
- [ ] Neither file contains any remaining text describing the outcome as simulation-only or foundation-only
- [ ] No existing `FR/NF/SR/DR/IR/TR/OR` requirement ID has been removed or renumbered
- [ ] Both files show `Status: active`
- [ ] `plans/002-real-ig-demo-connection-delivery-plan.md` is listed in both files' Outputs

## Next step

After completing edits and passing the validation checklist, report back to the Execute Delivery orchestrator so Work Item 1 Task 1 checkboxes can be updated and the Broker Auth Integration Agent handoff (Task 2) can proceed.
