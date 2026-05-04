# UI Update and Refactor Technical Specification

This document defines how work package `004-ui-update-and-refactor` will be implemented. It translates the approved requirements into an implementable design for the Web UI refresh, supporting refactoring, validation, and documentation updates.

## 1. Summary

- **Source**: See `requirements.md` for canonical work metadata, requirement identifiers, and acceptance criteria. See `../business-requirements.md` for the project-level business context and `../systems-analysis.md` for the implementation-agnostic system baseline.
- **Status**: draft
- **Input**: `requirements.md`, `../business-requirements.md`, `../systems-analysis.md`
- **Output**: `plans/001-delivery-plan.md`

## 2. Problem and Context

### 2.1 Problem statement

The current authenticated Web UI is functional but visually basic, inconsistent across pages, and not yet structured for the maintainable operator experience described in work package `004-ui-update-and-refactor`. The implementation must improve the shared shell, home, status, configuration, and authentication-related presentation while preserving current routes, authorization behavior, existing backend integrations, and the AppHost-backed local/testing path.

### 2.2 Assumptions

- The existing Web project at `src/TNC.Trading.Platform.Web` remains the operator UI for this work package.
- The application continues to use Blazor with interactive server components.
- Existing API contracts exposed through `PlatformApiClient` remain the primary backend integration path for this package.
- Existing page routes, page identities, and navigation ordering are preserved exactly.
- The package should standardize on the current Web UI approach and adopt Radzen Blazor free components broadly across the shared shell and prioritized pages rather than only using Radzen selectively.
- Theme preference persistence is per browser, not per user account or cross-device profile.
- The Radzen Software theme family is the visual baseline for both dark and light modes in the refreshed UI.
- The current AppHost composition using SQL Server and Keycloak remains the baseline local/testing orchestration model, but this package may include a broad Aspire-aligned cleanup of AppHost wiring and structure as long as it introduces no new supporting resources and no intentional behavior changes.
- Existing authorization and protected route rules remain unchanged unless a requirement explicitly calls for presentation-only updates.

### 2.3 Constraints

- The work package must stay within the scope of UI refresh and maintainability improvements and must not introduce new trading capabilities.
- The project targets .NET 10 and must remain aligned with repository standards for Blazor, Aspire, Keycloak, SQL Server, and documentation.
- For Blazor UI work, Radzen Blazor free components are the preferred default component library.
- Requirements-level constraints must be honored:
  - current routes, page names, and navigation order are unchanged
  - desktop and laptop usage are primary
  - narrower widths must remain usable without a mobile-first redesign
  - the operator UI defaults to dark theme when no saved preference exists
  - theme controls are signed-in only
  - signed-out/authentication surfaces must not adopt the full signed-in shell
- The package must preserve existing authentication, authorization, secret handling, and protected workflow boundaries.
- AppHost refactoring in this package must remain within the existing SQL Server and Keycloak-backed local/testing path and must not add resources or widen platform scope.
- The technical specification must remain self-contained within `docs/004-ui-update-and-refactor/`.

## 3. Proposed Solution

### 3.1 Approach

Implement the package as a focused UI-shell and presentation refactor inside `src/TNC.Trading.Platform.Web`, using the existing Blazor Server interactive application and existing API/view-model surface, while adopting Radzen Blazor free components as the primary UI building blocks for shared shell, page layout, cards, accordions, badges, inputs, buttons, notifications, and theme switching across the prioritized pages.

In parallel, apply a broad but behavior-preserving AppHost cleanup in `src/TNC.Trading.Platform.AppHost` so the local/testing composition remains simple, more maintainable, and more aligned with Aspire best practices without changing the supported resource set or runtime expectations.

The implementation will proceed in layers:

1. Introduce shared UI infrastructure:
   - app-level Radzen registration and theme/script setup
   - a shared theme state service with per-browser persistence
   - reusable layout/header/navigation primitives
   - shared CSS tokens and theme-aware styling tied to the Radzen Software theme family
2. Refresh the shared operator shell:
   - collapsible left navigation with preserved route order
   - compact top header with site title, operator identity, sign-in/sign-out affordances, and active environment indicator
   - dark-default, light/dark theme switching from the header
   - improved spacing, typography, and container hierarchy
3. Refresh prioritized pages with broad Radzen adoption:
   - `Home.razor` becomes an operator overview page using Radzen layout, cards, badges, and compact summary patterns
   - `Status.razor` becomes an accordion-driven information surface using Radzen accordion and panel patterns with multiple open sections permitted
   - `Configuration.razor` becomes an accordion-driven editable surface using Radzen form, fieldset or panel, input, and validation-oriented primitives while preserving in-progress edits
4. Refresh signed-out and authentication surfaces:
   - hero-style, single-column, dark-default experience
   - same visual language as the signed-in UI without using the signed-in shell
5. Refactor duplicated UI patterns:
   - extract shared card, section, status badge, environment badge, theme toggle, and navigation item patterns
   - keep the current backend, view-model, and API flow intact
   - keep `Program.cs` focused on orchestration and move UI registration concerns into dedicated extension or service classes if needed
6. Refactor AppHost structure broadly but safely:
   - simplify Web, API, and AppHost wiring and helper extraction
   - improve readability of infrastructure composition and environment configuration
   - preserve current SQL Server and Keycloak resource usage
   - keep local/testing ergonomics stable for the targeted path

This approach satisfies the work-package requirements while minimizing behavioral risk, preserving IR1 compatibility, and aligning with the repository preference for Radzen in Blazor UI development.

### 3.2 Alternatives considered

| Option | Summary | Pros | Cons | Decision rationale |
| ------ | ------- | ---- | ---- | ------------------ |
| A | Broad Radzen adoption plus broad AppHost cleanup | Aligns with repo standards, yields stronger visual consistency, reduces custom component duplication, and improves AppHost maintainability | Larger refactor surface and higher validation burden | Preferred because the repo treats Radzen as the default Blazor UI component library and the package explicitly allows broader refactoring where it materially improves maintainability |
| B | Broad Radzen adoption plus minimal AppHost changes | Strong UI outcome with lower infrastructure risk | Leaves known AppHost cleanup opportunities in place | Rejected because the package explicitly allows broader cross-project refactoring when justified |
| C | Selective Radzen adoption inside the existing Blazor UI | Lower UI change surface, easier short-term integration | Leaves more mixed patterns and duplicated custom markup behind | Rejected because it achieves less consistency and maintainability than the package intends |
| D | Keep the existing HTML/CSS-only approach and refresh styling without Radzen | Lowest dependency change, smallest short-term surface area | Misses the repository default UI direction, duplicates component work, and weakens long-term consistency | Rejected because the repo now prefers Radzen for Blazor UI and the package explicitly targets reusable UI improvements |
| E | Broad rewrite of the Web UI into a new component architecture or different UI foundation | Could produce a fully uniform UI in one pass | High regression risk, exceeds the light/moderate refresh scope, and conflicts with standardizing on the existing UI/component approach | Rejected because it expands scope and increases delivery risk |

### 3.3 Architecture

The package remains inside the current Web project and AppHost composition. The primary architectural change is the addition of a reusable Radzen-first UI layer over the existing authenticated Web app, combined with a cleanup of AppHost composition structure.

- **Components**:
  - `Program.cs` updated for Radzen component registration and theme/script support
  - shared theme state service for dark/light mode and browser persistence
  - refreshed `MainLayout.razor`
  - shared header and navigation components
  - shared visual components for overview cards, badges, accordion sections, empty/loading/error states
  - refreshed `Home.razor`, `Status.razor`, `Configuration.razor`, and authentication or signed-out pages
  - supporting CSS refactor in `wwwroot/app.css` and optional feature-specific styles if introduced
  - AppHost helper extraction and cleanup in `AppHost.cs` and any new support files needed to clarify resource and wiring composition
- **Data flows**:
  - existing `PlatformApiClient` remains the source for status and configuration data
  - header environment indicator binds to available platform or broker environment data already surfaced in the UI or fetched via existing view models
  - theme selection flows from UI control to browser storage and then back into the app shell on load
  - home page alert and activity summaries reuse existing available status and event data rather than introducing a new backend interface
  - AppHost continues to compose Web, API, SQL Server, Keycloak, and existing notification/testing support without adding new supporting resources for this package
- **Dependencies**:
  - `Radzen.Blazor` package and setup
  - existing Blazor interactive server rendering
  - existing authentication stack with Keycloak/Test auth
  - existing AppHost-driven local development path with SQL Server and Keycloak only for the required targeted path

## 4. Requirements Traceability

| Requirement ID | Requirement | Implementation notes | Validation approach |
| -------------- | ----------- | -------------------- | ------------------- |
| FR1 | The Web project UI must present a more usable operator experience for the existing platform features | Refresh shared shell first, then home, status, and configuration pages; preserve routes and platform feature access; use Radzen broadly on prioritized pages | Focused UI verification of prioritized pages plus regression checks of existing operator flows |
| FR2 | The Web project UI must use a more consistent navigation and presentation approach | Introduce shared shell, header, navigation, badges, card, and accordion patterns used across in-scope pages | Manual visual consistency review and functional navigation checks |
| FR3 | The project must be refactored where needed to support cleaner UI structure and better maintainability | Extract shared components and services, keep API and view model contracts stable, and include broad AppHost cleanup where it simplifies composition without changing behavior | Build review, code review, targeted regression checks, and technical documentation review |
| FR4 | The shared operator UI shell must include a compact utility-style top-level header area | Add shared header above page content with title left, auth and environment right, and non-sticky behavior | Manual validation of signed-in and signed-out header states |
| FR5 | The AppHost-supported local and testing path must remain simple and rely only on the existing containerized SQL and Keycloak resources | No new supporting runtime dependencies; broad AppHost cleanup is limited to structure and ergonomics | Validate local path through AppHost with existing SQL and Keycloak only |
| FR6 | The refreshed operator UI must provide a basic light and dark theme switching capability | Add shared theme state service, a shared header control, and immediate apply behavior | Manual validation of dark/light switching in shared shell and prioritized pages |
| FR7 | The refreshed operator UI must remember the operator's chosen light or dark theme preference across sessions | Persist theme selection in browser storage and restore on load; default to dark without saved preference | Manual verification across browser refresh and later session in the same browser |
| FR8 | Signed-out and authentication surfaces must align visually with the refreshed operator UI without adopting the full signed-in shell | Apply the same design language to sign-in, access-denied, and signed-out surfaces without sidebar shell | Manual validation of authentication presentation states |
| NF1 | The updated UI and supporting project structure must be easier to understand and change | Consolidate layout primitives, reduce duplicated page styling, create reusable UI state patterns, and simplify AppHost structure | Review structure against the spec and confirm reduced duplication in touched UI/AppHost areas |
| NF2 | Existing supported operator workflows must continue to work after the changes | Keep existing API contracts, routes, policies, and page purposes unchanged | Build plus targeted flow validation for home, status, configuration, and auth entry/exit |
| NF3 | The updated UI must improve usability for the project owner | Improve hierarchy, spacing, typography, status grouping, and operator orientation | Visual review against requirements and operator flow walkthrough |
| NF4 | The work must preserve current observable runtime and operator-facing status information | Reuse current status/configuration data and event summaries without removing information required for flows | Compare before/after status/configuration coverage and validate key operator information remains visible |
| NF5 | The work package must simplify the targeted local and testing composition | Avoid additional resources, preserve current AppHost support model, and improve composition clarity | AppHost run validation using current SQL and Keycloak containers |
| NF6 | The refreshed UI must use a clean, minimal professional dashboard style | Introduce restrained palette, consistent spacing, clear headings, compact chrome, readable panel treatment using the Radzen Software theme family | Manual UI review against shared-shell and prioritized-page acceptance criteria |
| NF7 | The refreshed operator UI must be optimized for desktop and laptop usage while remaining basically usable at narrower widths | Use responsive shell collapse behavior and simple narrower-width layout adjustments | Manual checks at representative desktop/laptop widths and reduced widths |
| NF8 | The refreshed status page must present operational information with clearer grouping and hierarchy | Rebuild the status page into accordion sections with lower-priority sections collapsed by default | Manual status-page verification and targeted UI review |
| NF9 | The refreshed configuration page must present configuration options with clearer grouping and form readability | Rebuild the configuration page into accordion sections with grouped Radzen form controls and preserved drafts in memory | Manual configuration-page verification including section switching while editing |
| NF10 | The refreshed UI must apply the selected light or dark theme consistently | Use centralized theme tokens/service and apply theme-aware styling across shared shell and refreshed pages | Manual review in dark and light themes |
| NF11 | The refreshed UI must restore the operator's previously selected theme choice consistently across sessions | Browser persistence and restore-on-start behavior | Manual refresh/session validation in the same browser |
| NF12 | Signed-out and authentication surfaces must feel visually consistent with the refreshed operator UI | Reuse visual language for signed-out/access-denied surfaces without the signed-in navigation shell | Manual auth-surface validation |
| SR1 | The work must preserve the current authentication and authorization protections | Do not change route protection or role/scope checks; shell updates remain presentation-focused | Regression verification of protected routes and signed-out behavior |
| SR2 | The work must not expose secrets or sensitive platform data through new UI changes | Preserve current write-only secret handling on the configuration page; no sensitive values rendered in new UI elements | Review rendered content plus configuration page regression checks |
| SR3 | The work must not introduce new checked-in secrets or secret-handling paths | No new secrets stored in source; theme persistence limited to non-sensitive browser preference data | Code/config review and build validation |
| SR4 | Refactoring must not weaken existing safeguards on protected navigation, protected actions, or operator-only workflows | Retain current navigation protection, access denied behavior, and operator-only flow boundaries | Route and action regression checks |
| IR1 | The updated Web UI must continue to work with the platform's existing backend interfaces | Keep `PlatformApiClient` calls and current view model contracts as the integration baseline | Build and UI regression testing using existing backend endpoints |
| TR1 | The work package must include validation of the affected operator UI flows | Include focused validation for shared shell and prioritized operator flows | Delivery plan will include build, targeted tests, and manual UI flow checks |
| TR2 | The work package must validate that refactoring changes do not introduce regressions | Keep changes layered and verify behavior after refactor | Build/test plus focused regression walkthroughs |
| TR3 | The work package must validate the targeted AppHost-supported local/testing path | Do not introduce extra resources and verify the touched local/testing path remains operable | AppHost validation using current SQL and Keycloak containers |
| TR4 | The work package must include focused manual validation of refreshed operator UI presentation states | Explicit manual validation matrix for shell, themes, auth states, narrower widths, and remembered preference | Include manual verification checklist in the delivery plan |
| OR1 | The work package must update operator-facing and local-development documentation if needed | Update `docs/wiki/` pages if shell, theme behavior, operator guidance, or local run expectations change | Documentation review before plan completion |
| OR2 | The work package must keep the affected AppHost local orchestration guidance aligned | Reflect any startup or validation changes in affected wiki/dev docs | Documentation validation against the delivered local/testing approach |

## 5. Detailed Design

### 5.1 Public APIs / Contracts (optional)

| Area | Contract | Example | Notes |
| ---- | -------- | ------- | ----- |
| UI Route | `/` | Home overview page | Route preserved; page role changes toward operator overview |
| UI Route | `/status` | Status page with accordion sections | Route preserved; moderate targeted refresh only |
| UI Route | `/configuration` | Configuration page with accordion sections | Route preserved; existing save workflow retained |
| UI Route | `/authentication/access-denied` | Signed-in but unauthorized surface | Presentation refresh only |
| Existing HTTP/API | Existing `PlatformApiClient` requests | `GetStatusAsync`, `GetAuthEventsAsync`, `GetConfigurationAsync`, `UpdateConfigurationAsync` | Existing backend integration retained; no new backend contract assumed in this package |

### 5.2 Data Model (optional)

| Entity/Concept | Fields | Constraints | Notes |
| -------------- | ------ | ----------- | ----- |
| ThemePreference | `ThemeMode`, `StoredAtUtc` | Per-browser only; non-sensitive | Stored in browser storage; defaults to dark if absent |
| ShellState | `IsSidebarCollapsed` | Defaults to expanded on new session | Collapse state resets per session unless later expanded by implementation decision |
| HeaderContext | `SiteTitle`, `DisplayName`, `IsAuthenticated`, `PlatformEnvironment`, `BrokerEnvironment`, `LiveOptionAvailable` | Auth-sensitive rendering only | Used to drive shared header content |
| HomeOverviewModel | `StatusSummary`, `AlertItems`, `RecentActivityItems` | Must reuse existing available data only | Summary-focused, no new platform capability |
| AccordionSectionState | `SectionKey`, `IsExpanded`, `DisplayOrder` | Multiple sections may remain open simultaneously | Used by status/configuration page section behavior |
| AuthenticationSurfaceModel | `Headline`, `PrimaryActionUrl`, `Message` | Must remain minimal and text/form only | For sign-in and access denied visual consistency |
| AppHostCompositionModule | `Name`, `Responsibility`, `ReferencedResources` | No new required supporting resources | Documents the intended extracted AppHost structure |

### 5.3 Implementation Plan (technical steps)

| Step | Change | Files/Modules | Notes |
| ---- | ------ | ------------- | ----- |
| 1 | Add Radzen UI infrastructure and theme registration | `src/TNC.Trading.Platform.Web/Program.cs`, shared imports, app shell files | Keep startup orchestration minimal and aligned with repo standards |
| 2 | Add theme state service and browser persistence | New service(s) under the Web project, likely service/state folder plus JS interop or supported browser storage approach | Must default to dark and restore immediately on load |
| 3 | Refactor shared shell and layout | `Components/Layout/MainLayout.razor`, supporting components, `wwwroot/app.css` | Add collapsible sidebar, compact top header, environment indicator |
| 4 | Refresh signed-out/auth surfaces | `Home.razor` unauthenticated state, `AccessDenied.razor`, auth-related pages/components | Hero-style single-column without signed-in shell |
| 5 | Refresh home overview | `Components/Pages/Home.razor`, shared summary components | Combined status card plus compact alerts/activity summaries using Radzen layout primitives |
| 6 | Refresh status page into accordion panels | `Components/Pages/Status.razor`, shared section components | Multiple sections can remain open; lower-priority sections collapsed by default |
| 7 | Refresh configuration page into accordion panels | `Components/Pages/Configuration.razor`, supporting form/group components | Broad Radzen form/layout adoption while preserving in-progress edits |
| 8 | Keep theme switching in the shared header only | Shared theme controls, header components | Avoid duplicated theme controls inside configuration content |
| 9 | Consolidate duplicated UI patterns and styles | Shared components, CSS tokens/utilities | Reduce duplication and improve maintainability |
| 10 | Refactor AppHost composition structure | `src/TNC.Trading.Platform.AppHost/AppHost.cs` and extracted support files if needed | Broad cleanup for readability, maintainability, and testing ergonomics without adding resources |
| 11 | Validate UI/AppHost behavior and update docs/wiki if needed | Relevant test projects and `docs/wiki/` pages | Required before marking the plan complete |

### 5.4 Error Handling

| Scenario | Expected behavior | Instrumentation |
| -------- | ----------------- | --------------- |
| Status/configuration load failure | Show readable inline error state without breaking shell layout | Existing logs plus user-visible error text |
| Theme preference missing | Default to dark theme | Optional debug log only if useful; no user-facing error |
| Theme preference read/write failure | Keep current theme in memory, fall back safely, do not block operator workflow | Warning log in browser/server if implemented |
| Unauthorized access to protected page | Existing redirect/access-denied flow remains in place | Existing auth audit flow remains active |
| Configuration save failure | Show inline status message without discarding in-memory edits | Existing HTTP error handling plus UI status message |
| Narrower-width layout overflow | Sidebar remains usable and content remains readable without hard breakage | Manual validation rather than runtime instrumentation |
| Missing environment indicator data | Header omits or de-emphasizes unavailable environment details without failing layout | No special instrumentation required |
| AppHost cleanup regression | Preserve existing local resource composition and startup semantics; fail fast if wiring becomes invalid | Existing Aspire startup output and local validation steps |

### 5.5 Configuration

| Setting | Purpose | Default | Location |
| ------ | ------- | ------- | -------- |
| UI theme preference key | Stores per-browser theme selection | `dark` when absent | Browser storage |
| Default theme mode | Initial UI theme for signed-in and signed-out surfaces | `dark` | Web UI/theme state service |
| Sidebar initial state | Determines first-load/session shell behavior | Expanded | Web UI shell state |
| Radzen theme family | Selects allowed base visual theme | `Software` | Web UI app shell/head registration |
| Existing auth settings | Continue to drive signed-in/signed-out behavior | Existing values | Existing app configuration/AppHost environment |
| Existing API base address | Continue to drive Web-to-API communication | Existing value | `Program.cs` HTTP client registration |
| Existing AppHost resource settings | Continue to control SQL Server, Keycloak, and related wiring | Existing values | `src/TNC.Trading.Platform.AppHost/appsettings*.json` and environment configuration |

## 6. Security Design

Describe how the solution meets `SRx` requirements.

- **AuthN/AuthZ**: Continue using the existing Keycloak/Test authentication setup and current route/role/scope protections. UI refactoring does not weaken protected surfaces or alter authorization semantics.
- **Secrets**: Preserve current write-only secret handling for IG credentials. New UI elements must not render secret values, and theme preference persistence stores only non-sensitive UI state.
- **Data protection**: Existing HTTPS/authentication flow remains unchanged. No new sensitive data paths are added in this package.
- **Threat model notes**:
  - prevent accidental exposure of secret presence/credential values beyond the current allowed “Present/Missing” pattern
  - do not expose privileged routes or actions through shell changes
  - avoid introducing client-side storage for anything other than non-sensitive theme preference data
  - keep signed-out surfaces free of privileged navigation
  - keep AppHost cleanup limited to structural and orchestration improvements rather than security behavior changes

## 7. Observability

| Signal | What | Where | Notes |
| ------ | ---- | ----- | ----- |
| Logs | UI load/save failures, optional theme persistence failures, auth redirects already present | Existing Web app logging sinks | No secrets or sensitive payloads |
| Logs | AppHost startup/composition failures during cleanup validation | Existing Aspire/AppHost output | Used to confirm no orchestration regressions |
| Metrics | None newly required for this package | Existing platform telemetry if available | UI package is primarily presentation-focused |
| Traces | Existing request traces for page/API interactions | Existing Aspire/OpenTelemetry path if enabled | No new tracing dependency assumed |
| Operator-visible status | Header environment indicator, overview status card, inline error/success states | Web UI | These are user-facing signals, not backend telemetry replacements |

## 8. Testing Strategy

| Test type | Coverage | Location | Notes |
| --------- | -------- | -------- | ----- |
| Unit | Theme state service, shell state defaults, and UI mapping helpers extracted into C# classes | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` | Prefer unit tests where UI behavior is factored into testable services/models |
| Integration | Existing Web/API integration behavior that could regress from configuration/status page refactor | `test/TNC.Trading.Platform.Web/...` or existing integration projects as applicable | Only where real service boundaries are involved |
| Functional | Shared shell, home overview, status accordion behavior, configuration accordion behavior, and authentication presentation states | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` | Use requirement-traceable comments and stable feature-area organization |
| E2E | High-value UI navigation and signed-in flows through the refreshed shell | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` | Use Playwright guidance for UI-driven coverage |
| Local composition validation | Broad AppHost cleanup with unchanged SQL/Keycloak-backed path | AppHost-supported local validation steps in the delivery plan | Confirms no extra resources and no startup path regression |
| Manual | Shared shell, dark default, theme switching, remembered preference, signed-in/signed-out states, and narrower widths | Delivery-plan validation checklist | Required by TR4 and central to acceptance |

## 9. Rollout Plan

| Phase | Action | Success criteria | Rollback |
| ----- | ------ | ---------------- | -------- |
| 1 | Add Radzen and theme infrastructure behind the existing shell | Build succeeds and existing pages still render | Revert package registration/theme wiring |
| 2 | Deliver shared shell refresh | Navigation, auth info, and header work without route/order regressions | Revert layout/components/CSS changes |
| 3 | Deliver prioritized page refreshes | Home, status, and configuration meet acceptance criteria and remain functionally correct | Revert page-specific changes individually |
| 4 | Apply AppHost cleanup and validate local/testing path | AppHost remains operable with the existing SQL and Keycloak-backed path only | Revert AppHost structural cleanup |
| 5 | Validate UI/AppHost states; update docs/wiki | Manual and automated validation complete; docs updated if needed | Hold completion until documentation and validation align |

## 10. Open Questions

- None currently identified.

## 11. Appendix (optional)

- `requirements.md`
- `../business-requirements.md`
- `../systems-analysis.md`
- `../../.github/copilot-instructions.md`
- `../../.github/instructions/work-packages.instructions.md`
- `../../.github/instructions/docs.instructions.md`
- `../../.github/instructions/dotnet-stack.instructions.md`
- `../../.github/instructions/radzen-blazor.instructions.md`
- Current implementation baseline:
  - `src/TNC.Trading.Platform.Web/Program.cs`
  - `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`
  - `src/TNC.Trading.Platform.Web/Components/Pages/Home.razor`
  - `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`
  - `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor`
  - `src/TNC.Trading.Platform.Web/Components/Pages/AccessDenied.razor`
  - `src/TNC.Trading.Platform.Web/wwwroot/app.css`
  - `src/TNC.Trading.Platform.AppHost/AppHost.cs`
