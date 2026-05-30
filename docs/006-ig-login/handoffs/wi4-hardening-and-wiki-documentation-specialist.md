# Handoff: Work Item 4 — Documentation Specialist

## Target agent

Documentation Specialist

## Work item reference

`docs/006-ig-login/plans/002-real-ig-demo-connection-delivery-plan.md` — Work Item 4 (all tasks and steps)

## Delivery context

- Branch: `006-ig-login`
- Baseline: `dotnet build` clean; 209/209 unit tests pass
- Work Items 1, 2, and 3 are **complete**:
  - WI1: `IIgSessionClient`, `IgSessionClient`, proof-data models, DI registration, and sanitizer extension in place
  - WI2: `PlatformStateCoordinator` calls `igSessionClient.AuthenticateAsync` for real; `IProtectedCredentialService`/`IgCredentials` added; session tokens remain strictly ephemeral
  - WI3: `IgProofDataSnapshot`, `IPlatformIgProofDataStore`, `InMemoryPlatformIgProofDataStore`, proof-data capture in coordinator, `IgProofDataResponse`, `IgLoginStatusResponse.LatestProofData`, `GetPlatformStatusMapping` extension, `IgProofDataViewModel`, `IgLoginStatusViewModel.LatestProofData`, and the "IG Demo proof data" accordion section in `Status.razor` are all in place; 8 new unit tests added

## Scope boundaries — read carefully

**In scope for this handoff:**

- Update `docs/wiki/application-overview.md` — remove the simulation-only limitation; describe the real IG Demo connection, proof-data capture, and read-only guarantee
- Update `docs/wiki/architecture.md` — add the outbound IG REST client to the topology, document the runtime token-handling boundary, and update any sequence or flow diagrams that still show simulated auth
- Update `docs/wiki/runtime-behavior.md` — update the startup sequence and auth supervision sections to reflect real IG calls, proof-data capture flow, non-fatal proof-data failure handling, and the `IgProofDataSnapshot` read model
- Update `docs/wiki/api-reference.md` — document the new `igProofData` nested object in the `GET /api/platform/status` response: its fields (`preferredAccountName`, `preferredAccountId`, `balance`, `openPositionCount`, `retrievedAtUtc`), the null case when no proof data has been retrieved, and the read-only guarantee
- Update `docs/wiki/operator-guide.md` — document the "IG Demo proof data" accordion section on the status page, explain the null/not-yet-retrieved state, document how to interpret the account name, balance, open position count, and retrieved timestamp, and add a note that the data is read-only and sourced from IG Demo
- Update `docs/wiki/local-development.md` — add a section explaining how to supply IG Demo credentials through the `/configuration` page, what happens at the next supervised tick, how to verify the real session is active on the status page, and the quota-aware note that proof queries are tied to auth events rather than polling
- Update `docs/wiki/testing-and-quality.md` — add a section describing deterministic proof-data unit tests (fake `IIgSessionClient` and `IPlatformIgProofDataStore`) vs. the opt-in real-IG smoke path; document how to enable the opt-in real-IG smoke tests when live credentials are available; note that `dotnet test` without extras remains fully deterministic
- Validate all affected wiki links resolve after updates

**Out of scope — do not touch:**

- Any source code (`.cs`, `.razor`, `.csproj`) — this handoff is documentation only
- `docs/006-ig-login/` work-package files (`requirements.md`, `technical-specification.md`, plan files) — those remain frozen as delivered
- `docs/wiki/codebase-guideline-alignment-delivery-plan.md` and `docs/wiki/ig-day-trading-with-ig-apis.md` — not affected by WI4 changes
- Work package handoff files — do not modify prior handoff reports
- Any new wiki pages — update only the seven pages listed above

## Key files to read before starting

| File | What to note |
| ---- | ------------ |
| `docs/wiki/application-overview.md` | Contains the `### Important current limitation` section that still says "does not yet perform real IG authentication" — this must be replaced with a description of the real Demo connection and read-only proof data |
| `docs/wiki/architecture.md` | The topology and data-flow sections describe the pre-WI2 architecture; the outbound IG REST client and token-handling boundary are not yet documented here |
| `docs/wiki/runtime-behavior.md` | The startup sequence still shows simulated auth; update to show real `IgSessionClient.AuthenticateAsync` call, proof-data capture step, and non-fatal failure path |
| `docs/wiki/api-reference.md` | The `GET /api/platform/status` section needs a `igLoginStatus.igProofData` field documented with all five subfields and the null-state description |
| `docs/wiki/operator-guide.md` | The status page section ends before the new "IG Demo proof data" accordion; it must be extended with guidance on reading the proof-data panel |
| `docs/wiki/local-development.md` | No current guidance for supplying IG Demo credentials or verifying a real Demo session — add a dedicated subsection |
| `docs/wiki/testing-and-quality.md` | Proof-data unit test coverage added in WI3 is not yet described; deterministic vs. real-IG validation strategy must be documented |
| `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` | Read to understand `TryCaptureLiveProofDataAsync` — proof data is captured once per successful auth tick, failure is non-fatal, tokens are consumed within the tick only |
| `src/TNC.Trading.Platform.Application/Configuration/IgProofDataSnapshot.cs` | `PreferredAccountName`, `PreferredAccountId`, `Balance`, `OpenPositionCount`, `RetrievedAtUtc` — these map directly to the API response fields |
| `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/IgProofDataResponse.cs` | The exact API response shape for the proof data nested object |
| `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor` | Read the "IG Demo proof data" accordion markup to understand the UI fields and the null/empty-state message |
| `docs/006-ig-login/plans/002-real-ig-demo-connection-delivery-plan.md` | WI4 scope table, wiki file list, and acceptance checklist — use as the authoritative scope boundary |

## Deliverables

### 1. Update `docs/wiki/application-overview.md`

Replace the `### Important current limitation` section. The current text says the platform does not yet perform real IG authentication. This is no longer true after WI2 and WI3. Replace it with content that:

- States that the platform now establishes a real authenticated session against the IG Demo REST API on each scheduled auth tick
- Describes the read-only proof-data retrieval: after a successful Demo session the platform queries account information and open positions as a safe connectivity proof
- States clearly that this is read-only — no trades, orders, or write operations are issued
- States that the proof data (account name, balance, open position count, retrieved timestamp) is surfaced on the status page and through the API
- Removes or updates bullet items in the "Delivered capabilities" list that implied authentication was simulated — capture and retention of the IG login snapshot and real Demo connectivity are now delivered capabilities

### 2. Update `docs/wiki/architecture.md`

Add documentation of the outbound IG REST client and runtime token-handling boundary:

- Add `IgSessionClient` / `IIgSessionClient` to the solution topology description as the outbound broker integration adapter in the Infrastructure layer
- Add a prose section explaining that session tokens (`CST`, `X-SECURITY-TOKEN`) are consumed transiently during a single auth tick inside `PlatformStateCoordinator` and are never written to persistence, the API response, or the UI
- Update any existing diagrams or flow descriptions that show simulated auth; replace with the real outbound IG Demo call pattern
- If the architecture doc has a data-flow or component diagram that omits the IG REST dependency, add it. The Demo base URL is `https://demo-api.ig.com/gateway/deal`. Live calls remain blocked when the platform environment is Test

Example prose addition for the token-handling boundary section:

```markdown
## IG REST client and token boundary

The `IgSessionClient` infrastructure adapter issues authenticated HTTP calls to the IG Demo REST API at `https://demo-api.ig.com/gateway/deal`.

Session tokens returned by IG (`CST` and `X-SECURITY-TOKEN`) are consumed transiently within the same `PlatformStateCoordinator` tick that received the authentication response. They are:

- never written to the `platformdb` database
- never included in any API response
- never surfaced in the Blazor UI or operator log output
- passed directly from the `IgAuthenticateResponse` into the proof-data query calls and then discarded

The `IgProofDataSnapshot` that is persisted to the in-memory store contains only safe read fields: account name, account ID, balance, open-position count, and the retrieval timestamp.
```

### 3. Update `docs/wiki/runtime-behavior.md`

Update the startup sequence and supervision sections:

- In the startup sequence diagram, replace the implied simulated auth step with the real `IgSessionClient.AuthenticateAsync` call
- Add step 6 to the numbered runtime model list: "retrieves read-only IG Demo proof data (account, balance, positions) after a successful session and persists the result as a non-secret `IgProofDataSnapshot`"
- Add a new `## Proof-data capture` section (or extend the existing auth section) explaining:
  - proof-data capture runs once per successful auth tick immediately after `CaptureSuccessfulLoginSnapshotAsync`
  - if the accounts or positions query fails, the failure is logged as a warning and the session remains active — proof-data failure is non-fatal
  - the last successful `IgProofDataSnapshot` is held in the `IPlatformIgProofDataStore` in-memory store and survives until the process restarts or a new successful capture overwrites it
  - when no proof data has ever been captured, the status projection returns `null` for `LatestProofData`

### 4. Update `docs/wiki/api-reference.md`

Extend the `GET /api/platform/status` section with the new `igProofData` nested field documentation. Add an example JSON block showing both the populated and null cases:

```markdown
#### `igLoginStatus.igProofData`

When proof data has been successfully retrieved after a Demo session, the `igProofData` field is populated:

```json
"igLoginStatus": {
  ...
  "igProofData": {
    "preferredAccountName": "Demo Account",
    "preferredAccountId": "ACC12345",
    "balance": 5000.00,
    "openPositionCount": 2,
    "retrievedAtUtc": "2025-01-15T10:30:00+00:00"
  }
}
```

When no proof data has been retrieved yet (for example, on first startup before an auth tick has completed), the field is `null`:

```json
"igLoginStatus": {
  ...
  "igProofData": null
}
```

The `igProofData` object is read-only and derived from IG Demo account and position queries. It does not contain session tokens, credentials, or any write-capable context.
```

### 5. Update `docs/wiki/operator-guide.md`

Extend the status page section to document the "IG Demo proof data" accordion panel. Add content that:

- Names the accordion section ("IG Demo proof data") and describes when it appears open or closed
- Explains the null/not-yet-retrieved state: operators will see "No IG Demo proof data has been retrieved yet" until the platform has completed at least one successful auth tick
- Describes each field:
  - **Preferred account** — the display name of the account marked as preferred in the IG Demo account list, or the first account when no preference is set
  - **Account ID** — the IG account identifier for the preferred account
  - **Balance** — the preferred account's balance in the account's base currency, formatted to two decimal places
  - **Open positions** — the number of currently open positions in the Demo account
  - **Retrieved at** — the local time when the proof data was last successfully captured
- Includes the hint text that operators will see: "Data sourced from IG Demo (read-only). No trades or orders have been placed."
- Notes that the data is refreshed after each successful Demo auth tick, not continuously polled

### 6. Update `docs/wiki/local-development.md`

Add a new `## IG Demo credentials and proof-data verification` section (or a subsection under the existing local run guidance). Include:

- How to supply IG Demo credentials: navigate to `/configuration` while signed in as an operator, enter the IG API key, identifier, and password in the credential fields, and save. The credentials are stored using ASP.NET Core Data Protection and are never displayed in plaintext after saving.
- What happens next: the background supervisor will attempt a real IG Demo auth on the next scheduled tick. In a local development run this is typically within a few seconds.
- How to verify: navigate to `/status` and look for the "IG login" accordion showing a successful auth state and a recent `LastSuccessfulLoginAtUtc` timestamp. The "IG Demo proof data" accordion should then show real account data.
- Quota note: the proof-data queries (accounts and positions) are issued once per successful auth tick, not on a polling timer. They are low-frequency, read-only calls. Do not manually trigger auth retries in rapid succession if credentials are correct and the session is active.
- Troubleshooting: if the "IG login" accordion shows a failed state, check that the credentials are correct and that the IG Demo API (`https://demo-api.ig.com`) is reachable from the local machine.

### 7. Update `docs/wiki/testing-and-quality.md`

Add or extend coverage in the "What the tests already cover" section and add a new `## Deterministic vs. real-IG validation` section:

- In the Application behavior subsection, add that proof-data query paths are now covered: successful capture, accounts-query failure (session remains active), positions-query failure (session remains active), fallback account selection, status projection with proof data, and status projection with null proof data
- In the API behavior subsection, add that `GetPlatformStatusMappingTests` validates the `IgProofDataResponse` contract shape for both populated and null cases
- Add a new section explaining the deterministic vs. real-IG split:

```markdown
## Deterministic vs. real-IG validation

All proof-data tests in `TNC.Trading.Platform.Application.UnitTests` and `TNC.Trading.Platform.Api.UnitTests` use fake implementations of `IIgSessionClient` and `IPlatformIgProofDataStore`. Running `dotnet test` without any additional configuration is fully deterministic and does not require IG credentials or network access.

### Opt-in real-IG smoke verification

A real IG Demo session can be verified manually by:

1. Starting the platform through AppHost: `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`
2. Navigating to `/configuration` and entering valid IG Demo credentials
3. Waiting for the next background supervisor tick (typically a few seconds)
4. Navigating to `/status` and confirming:
   - The "IG login" accordion shows a successful state and a recent `LastSuccessfulLoginAtUtc` timestamp
   - The "IG Demo proof data" accordion shows real account data with a recent `Retrieved at` timestamp

This manual path requires valid IG Demo credentials and network access to `https://demo-api.ig.com`. It is not required for normal `dotnet test` runs and should not be used as a substitute for the deterministic unit test suite.
```

### 8. Link validation

After updating all seven wiki pages, verify that:

- all internal relative links between wiki pages still resolve (check any links added or moved during the updates)
- the `docs/wiki/README.md` navigation does not need updating (read it to confirm it does not list page summaries that contradict the new content)

## Validation gates

After all deliverables are complete:

1. `dotnet build` — must succeed with zero errors (no code changes, but verify the build is still clean)
2. `dotnet test --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~E2ETests&FullyQualifiedName!~FunctionalTests"` — all 209 unit tests must still pass
3. Confirm all seven wiki pages have been updated
4. Confirm `docs/wiki/codebase-guideline-alignment-delivery-plan.md` and `docs/wiki/ig-day-trading-with-ig-apis.md` have **not** been modified
5. Confirm no source code files (`.cs`, `.razor`, `.csproj`) have been modified
6. Confirm all relative wiki links in the updated pages resolve to existing files
7. Confirm `docs/wiki/README.md` navigation is consistent with the updated page content

## Assumptions to validate

- The `docs/wiki/README.md` navigation index may need a summary line update if it lists per-page summaries — read it before editing other pages to avoid contradicting its content
- `docs/wiki/application-overview.md` has a "Delivered capabilities" bullet list that may need individual bullet edits, not just section replacement — read the full list before rewriting
- `docs/wiki/architecture.md` may already have partial outbound-dependency documentation from WI2; read the full file before adding new sections to avoid duplication
- `docs/wiki/runtime-behavior.md` has a numbered list describing the startup sequence; the item numbers must remain correct after inserting the proof-data capture step
- `docs/wiki/api-reference.md` may already have a `GET /api/platform/status` response shape example from WI2; extend it rather than replacing it

## Agent report

### Files created

None.

### Files modified

 - `docs/006-ig-login/handoffs/wi4-hardening-and-wiki-documentation-specialist.md`
 - `docs/wiki/application-overview.md`
 - `docs/wiki/architecture.md`
 - `docs/wiki/runtime-behavior.md`
 - `docs/wiki/api-reference.md`
 - `docs/wiki/operator-guide.md`
 - `docs/wiki/local-development.md`
 - `docs/wiki/testing-and-quality.md`

### Unit tests added

N/A — this handoff contains documentation changes only.

| Test class | Added tests |
| --- | ---: |
| N/A | 0 |

### Build gate outcome

`dotnet build` succeeded with zero errors.

### Test gate outcome

`dotnet test --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~E2ETests&FullyQualifiedName!~FunctionalTests"` succeeded; 209/209 unit tests passed.

### Assumptions validated

- `docs/wiki/README.md` did not require navigation updates.
- The updated wiki pages kept their internal relative links consistent.
- No source code files (`.cs`, `.razor`, `.csproj`) were modified.

### Deviations from handoff

None.

### Escalations

None. Ready for next handoff.
