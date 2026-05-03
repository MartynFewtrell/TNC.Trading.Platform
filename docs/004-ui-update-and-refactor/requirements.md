# UI Update and Refactor Requirements

This document captures the requirements for work package `004-ui-update-and-refactor`. It defines what should be improved in the Web UI and what project refactoring outcomes are expected, while staying aligned with the project-level business requirements.

## 1. Summary

- **Work item**: UI update and refactor
- **Work folder**: `./docs/004-ui-update-and-refactor/`
- **Business requirements**: `../business-requirements.md`
- **Owner**: TNC Trading
- **Date**: 2026-05-02
- **Status**: draft
- **Outputs**:
  - `technical-specification.md`
  - `plans/001-delivery-plan.md`

### 1.1 Links

| Document | Path |
| --- | --- |
| Business requirements | `../business-requirements.md` |
| Requirements | `requirements.md` |
| Technical specification | `technical-specification.md` |
| Initial delivery plan | `plans/001-delivery-plan.md` |

## 2. Context

### 2.1 Background

The platform now has an authenticated operator Web UI, but the next work package is intended to improve how the UI looks and how easy it is to use in the Web project, while also carrying out refactoring work across the project where it supports better maintainability. This aligns with the candidate work package `004-ui-update-and-refactor` in `../systems-analysis.md`, which calls for improved navigation, presentation consistency, and maintainability without changing the core platform capability scope.

## 3. Scope

### 3.1 In scope

- Improve the look and usability of the Web project UI while preserving the core platform capability scope.

#### Shared shell and navigation

- Improve the shared layout, navigation, and reusable UI components first.
- Preserve the current routes, page names, and navigation order exactly while improving the presentation and maintainability of the in-scope operator UI.
- Allow the left navigation to collapse on desktop and laptop layouts to give more room to page content.
- Default the left navigation to an expanded state on first load and on each new session.
- Preserve icons for primary destinations when the left navigation is collapsed.
- Introduce a compact utility-style top-level header area that keeps the left navigation in place, shows the site title on the left, and shows login information on the right.
- Include a small active-environment indicator in the signed-in header when that information is available.
- Keep the top-level header non-sticky so it scrolls away with the page content rather than remaining fixed while the operator scrolls.

#### Page priorities and general refresh approach

- Prioritize the landing/home, status, and configuration pages immediately after the shared shell work.
- Apply a light visual refresh to the prioritized pages after the shared shell update, improving spacing, typography, and styling while keeping their current layouts mostly intact except where a moderate targeted refresh is explicitly allowed below.
- Limit non-prioritized pages in this package to inheriting the shared shell and common styling improvements without targeted page-specific redesign.

#### Landing or home page

- Position the landing or home page as a lightweight operator overview page after the shared shell refresh, giving it a clearer role as the main entry point without changing its route or page identity.
- Prioritize an operational status summary as the primary content on the landing or home overview page, presented as a single combined summary card.
- Keep the landing or home overview heading area concise, without a supporting subtitle or additional introduction text.
- Keep the home overview status summary visually simplified, leaving detailed timestamps and freshness emphasis to the status page rather than the overview card.
- Emphasize active or unresolved alerts and notable events immediately after the primary operational status summary on the landing or home overview page, using a compact summary list with severity or status indicators.
- Let the compact alerts list on the home overview show however many active items fit naturally in the layout rather than imposing a small fixed count limit with a see-more link.
- Keep items in the home overview's compact alerts list as summary-only indicators rather than clickable links to deeper pages.
- Do not include home-page quick actions in this package; operators should rely on the main navigation for movement and task access.
- Include recent activity or history on the landing or home overview page when it can reuse existing available data without expanding this package's scope.

#### Status page

- Allow the status page to receive a moderate targeted refresh that improves grouping, visual hierarchy, and readability while preserving its existing route, purpose, and core information.
- Use a small number of distinct sections or panels rather than a single long page treatment on the status page.
- Collapse some lower-priority status sections by default to reduce visual length.
- Use accordion-style behaviour for the status page rather than a free-form layout, while still allowing multiple sections to remain open at the same time.
- Keep collapsed status-page section headers as simple section titles only, without adding short summary text into the collapsed header.

#### Configuration page

- Allow the configuration page to receive a moderate targeted refresh that improves grouping, visual hierarchy, form clarity, and readability while preserving its existing route, purpose, and core configuration workflows.
- Use a small number of distinct sections or panels rather than a single long page treatment on the configuration page so its overall look and feel aligns with the status page.
- Collapse some lower-priority configuration sections by default to reduce visual length.
- Use accordion-style behaviour for the configuration page rather than a tabbed or segmented switcher layout, while still allowing multiple sections to remain open at the same time.
- Keep collapsed configuration-page section headers as simple section titles only, without adding short summary text into the collapsed header.
- Preserve in-progress configuration edits when the operator moves between configuration sections rather than warning before leaving the current section.

#### Theme behaviour

- Include a basic light and dark theme switching capability for the refreshed operator UI without expanding this package into a broader theming platform.
- Expose the theme switcher in both the top-level header and the configuration page so the operator can access it quickly and also find it in configuration.
- Keep theme selection on the configuration page as a smaller option within the broader configuration content, placed within the section where it most logically belongs rather than being surfaced near the top for quick access.
- Apply theme changes immediately when the operator switches theme, without requiring a separate save or apply action.
- Remember the selected light or dark theme preference for the signed-in operator across sessions on a per-browser basis so the refreshed UI restores the operator's last chosen theme in that browser.
- Default the refreshed UI to the dark theme when no saved operator preference exists yet.
- Limit theme switching controls to signed-in operator surfaces only.

#### Signed-out and authentication surfaces

- Style signed-out and authentication surfaces with the same refined visual language as the signed-in UI, but without introducing the full signed-in shell or left-side navigation.
- Use a more spacious single-column hero-style layout for signed-out and authentication surfaces rather than a simple centered card layout.
- Keep signed-out and authentication surfaces focused almost entirely on the sign-in action, without adding supporting descriptive text beyond what is necessary for the flow.
- Keep signed-out and authentication surfaces text-and-form only, without adding a separate visual accent panel or illustration area.
- Use a narrower content column with more surrounding whitespace for the sign-in form on signed-out and authentication surfaces rather than a broader content width.
- Vertically center the narrower sign-in form within its column on signed-out and authentication surfaces rather than aligning it higher in the layout.
- Before sign-in, keep signed-out and authentication surfaces on the same dark-default visual presentation as the signed-in UI rather than using a separate fixed theme.

#### Responsive behaviour, refactoring, and validation

- Target desktop and laptop operator usage first, while keeping the refreshed UI basically usable at narrower widths without requiring a dedicated mobile-first redesign.
- Standardize on the existing UI and component approach for this package rather than replacing or substantially reshaping the styling/component foundation.
- Consolidate duplicated UI patterns into a clearer shared component set across the wider Web project where duplication already exists and the consolidation materially supports maintainability.
- Require focused validation beyond build and existing automated checks, covering the shared shell, prioritized operator flows, authentication presentation states, and basic narrower-width usability for the refreshed UI.
- Allow broader cross-project refactoring where it materially improves maintainability, provided the core platform capability scope and intended behavior are preserved.

### 3.2 Out of scope

- Adding new trading capabilities outside the existing platform capability scope.
- Changing the project-level business goals for the platform.
- Replacing the Web project with a different application type.

## 4. Functional Requirements

Use `FR1`, `FR2`, ... for functional requirements.

| ID | Requirement | Rationale | Acceptance criteria | Notes/Constraints |
| --- | ----------- | --------- | ------------------- | ---------------- |
| FR1 | The Web project UI must present a more usable operator experience for the existing platform features. | The work package exists to improve the look and usability of the operator UI. | The delivered UI improves the operator experience for the shared UI shell and the in-scope operator pages without removing the existing ability to access supported platform features. | Shared layout, navigation, and reusable UI components are prioritized first, followed by the landing/home, status, and configuration pages. Page-level refresh on the prioritized pages should remain light, focusing on spacing, typography, and styling while keeping existing layouts mostly intact, except that the status page may receive a moderate targeted refresh to improve grouping, visual hierarchy, and readability using a small number of distinct sections or panels with lower-priority sections collapsed by default, accordion-style behavior that allows multiple sections to remain open, and simple title-only collapsed headers, and the configuration page may receive a moderate targeted refresh to improve grouping, visual hierarchy, form clarity, and readability while preserving core workflows using a small number of distinct sections or panels that align visually with the status page, lower-priority sections collapsed by default, accordion-style behavior that allows multiple sections to remain open, simple title-only collapsed headers, and preserved in-progress edits when moving between sections. The landing/home page should serve as a clearer lightweight operator overview entry point after the refresh, with operational status summary content given top priority in its hierarchy as a single combined summary card, no supporting subtitle or additional introduction text in the heading area, a visually simplified presentation that leaves detailed timestamps and freshness emphasis to the status page rather than the overview card, active or unresolved alerts/notable events emphasized immediately after it as a compact summary list with severity or status indicators that shows however many active items fit naturally in the layout and remains summary-only rather than linking deeper into the application, no quick actions shown on the home page, and recent activity/history included when it can reuse existing available data without expanding scope. |
| FR2 | The Web project UI must use a more consistent navigation and presentation approach across the in-scope operator experience. | Consistency improves usability and reduces operator friction. | The shared UI shell and the in-scope operator pages use a consistent approach to layout, navigation, and visual presentation, and the result is documented in the technical specification and delivery plan. | The initial page priority after the shared shell work is landing/home, status, and configuration. Current routes, page names, and navigation order must be preserved exactly. Non-prioritized pages should receive shared shell and common styling improvements only unless a targeted exception is later documented. On desktop and laptop layouts, the left navigation may support collapsing to give more room to page content provided navigation remains clear and accessible, it should default to expanded on first load and on each new session, and the collapsed state should preserve icons for primary destinations instead of reducing to a fully minimal rail. |
| FR3 | The project must be refactored where needed to support cleaner UI structure and better maintainability across the relevant project areas without changing the intended capability scope. | The work package explicitly includes refactoring across the project. | Refactoring changes improve maintainability in the targeted areas, may span multiple projects when materially justified, and do not introduce new core platform capabilities beyond this package's scope. | Broader cross-project refactoring is allowed where it materially improves maintainability, but intended behavior and scope must be preserved. The intended non-Web refactoring scope includes the AppHost project where that supports Aspire best practice and simpler testing. The package should standardize on the existing UI/component approach rather than replacing the current styling/component foundation, and should consolidate duplicated UI patterns into a clearer shared component set where duplication already exists. |
| FR5 | The AppHost-supported local and testing path for this work package must remain simple and rely only on the existing containerized SQL and Keycloak resources as the required supporting resources. | The requested refactoring scope includes simplifying testing and keeping local orchestration aligned with the existing containerized test-support model. | The delivered changes keep the targeted AppHost-supported local/testing path limited to the existing containerized SQL and Keycloak resources and do not require additional supporting resources for that path. | Applies to the testing and local orchestration path touched by this work package. |
| FR4 | The shared operator UI shell must include a compact utility-style top-level header area with the site title aligned to the left and authentication information aligned to the right, while the existing left navigation remains in place. | A clearer persistent header improves orientation and preserves access to navigation and sign-in state without competing too heavily with page content. | The delivered shared UI shell includes a compact visible top-level header with the site title on the left and, on the right, either the logged-in operator display name with a log-out option plus a small active-environment indicator when available, or a log-in option when signed out, and the left navigation remains available after the change. | The header should remain utility-focused rather than acting as a strong brand banner. The active-environment indicator should stay small and supportive rather than becoming the primary header focus. The header should scroll away with page content rather than remaining fixed on screen. |
| FR6 | The refreshed operator UI must provide a basic light and dark theme switching capability. | An explicit theme choice can improve usability while staying within the package's UI refresh scope. | The delivered UI allows the operator to switch between a light theme and a dark theme across the shared shell and refreshed pages without breaking the intended operator workflows. | This package should provide basic theme switching only, not a broader multi-theme platform. The theme switcher should be available from both the top-level header and the configuration page for signed-in operators, theme changes should apply immediately when selected, and the configuration-page theme control should remain a smaller option within broader configuration content placed in the section where it most logically belongs rather than in a dedicated appearance section or surfaced near the top for quick access. |
| FR7 | The refreshed operator UI must remember the operator's chosen light or dark theme preference across sessions. | Persisting the chosen theme reduces repeated setup friction for the operator. | When the signed-in operator returns to the refreshed UI in the same browser, the previously selected light or dark theme is restored without requiring the operator to choose it again each time. | Preference persistence should stay within the scope of basic theme switching rather than expanding into a broader personalization framework, and it should be remembered per browser rather than as a cross-device account preference. When no saved operator preference exists yet, the UI should default to the dark theme. |
| FR8 | Signed-out and authentication surfaces must align visually with the refreshed operator UI without adopting the full signed-in shell. | Visual consistency should extend to sign-in-related surfaces without blurring the distinction between signed-out and signed-in operator contexts. | The delivered signed-out and authentication surfaces use the same refined visual language as the refreshed operator UI while remaining free of the signed-in left navigation and full operator shell structure. | Theme switching controls remain unavailable before sign-in. These surfaces should use a more spacious single-column hero-style layout rather than a simple centered card layout, stay focused almost entirely on the sign-in action rather than adding supporting descriptive text beyond what is necessary for the flow, remain text-and-form only without a separate visual accent panel or illustration area, use a narrower content column with more surrounding whitespace for the sign-in form rather than a broader content width, vertically center that form within its column, and follow the same dark-default visual presentation as the signed-in UI before sign-in. |

## 5. Non-Functional Requirements

Use `NF1`, `NF2`, ... for non-functional requirements.

| ID | Category | Requirement | Measure/Target | Acceptance criteria |
| --- | -------- | ----------- | -------------- | ------------------- |
| NF1 | Maintainability/Supportability | The updated UI and supporting project structure must be easier to understand and change than the current implementation. | Reduced structural complexity in the targeted areas, including relevant cross-project boundaries where materially justified, with implementation guidance captured in the technical specification. | The technical specification and delivery plan identify the intended structural improvements, and the delivered work reflects those improvements in the targeted scope while preserving intended behavior. The work should reuse and standardize the existing UI/component approach instead of introducing a replacement foundation, while consolidating duplicated UI patterns into a clearer shared component set where worthwhile. |
| NF2 | Reliability/Availability | Existing supported operator workflows must continue to work after the UI update and refactoring changes. | No regression in the supported in-scope operator flows. | Validation demonstrates that the updated UI still supports the intended operator flows for the affected areas. |
| NF3 | Usability/Accessibility | The updated UI must improve usability for the project owner when operating the platform. | Clearer navigation and more coherent presentation across the targeted UI surfaces, including the shared header and navigation shell. | The delivered UI provides a visibly more coherent operator experience for the targeted surfaces and does not make existing operator tasks harder to complete. |
| NF4 | Observability | The work must preserve the platform's current observable runtime and operator-facing status information for the affected areas. | Existing status and operator feedback remain available after the changes. | The updated implementation continues to expose the operator-facing information needed for the affected workflows. |
| NF5 | Testability/Supportability | The work package must simplify the targeted local and testing composition so the affected testing path does not depend on additional supporting resources beyond the existing containerized SQL and Keycloak resources. | No additional supporting resources are required for the targeted AppHost-supported local/testing path. | Validation confirms the targeted local/testing path touched by this package remains operable using the existing containerized SQL and Keycloak resources only. |
| NF6 | Usability/Visual Consistency | The refreshed UI must use a clean, minimal professional dashboard style that supports readability over decorative complexity. | Restrained color usage, clear spacing, improved typography, and readable presentation across the shared shell and prioritized pages. | The delivered UI applies a visually consistent, restrained, readability-first style across the shared shell and prioritized pages, with prioritized page changes remaining a light visual refresh rather than a substantial layout redesign. |
| NF7 | Responsive Behavior | The refreshed operator UI must be optimized for desktop and laptop usage while remaining basically usable at narrower widths. | Desktop and laptop layouts are the primary target; narrower widths remain usable for essential operator tasks without requiring a mobile-first redesign. | Validation confirms the refreshed shared shell and prioritized pages remain usable on typical desktop/laptop sizes and do not break at narrower widths used during operator access. |
| NF8 | Information Clarity | The refreshed status page must present operational information with clearer grouping and hierarchy than the current implementation. | Improved scannability and readability for the status surface while preserving the existing information purpose. | The refreshed status page makes the existing operational information easier to scan and understand without changing its core role or removing important status content, using a small number of distinct sections or panels instead of relying on a single long continuous page treatment, with lower-priority sections collapsed by default, accordion-style behavior that still allows multiple sections to remain open, and simple title-only collapsed headers. |
| NF9 | Form Clarity | The refreshed configuration page must present configuration options with clearer grouping and form readability than the current implementation. | Improved scannability and input clarity for the configuration surface while preserving the existing configuration purpose and workflows. | The refreshed configuration page makes the existing configuration workflows easier to read and complete without changing its core role or removing important configuration capability, using a small number of distinct sections or panels instead of relying on a single long continuous page treatment, aligning its overall visual treatment with the status page while remaining clearly editable, collapsing lower-priority sections by default, using accordion-style behavior that still allows multiple sections to remain open, preserving in-progress edits when the operator moves between sections, and keeping collapsed headers as simple title-only labels. |
| NF10 | Theme Consistency | The refreshed UI must apply the selected light or dark theme consistently across the shared shell and refreshed pages. | Theme styling remains coherent and usable in both supported theme modes, with dark theme as the default when no saved preference exists. | The delivered UI presents a consistent, readable operator experience in both light and dark themes across the shared shell and refreshed pages, and starts in dark theme for operators who have no saved theme preference yet. |
| NF11 | Preference Continuity | The refreshed UI must restore the operator's previously selected theme choice consistently across sessions. | The chosen theme remains stable for the operator in the same browser between sessions unless explicitly changed. | Validation confirms that an operator's selected light or dark theme is restored on later visits in the same browser instead of reverting unexpectedly. |
| NF12 | Authentication Surface Consistency | Signed-out and authentication surfaces must feel visually consistent with the refreshed operator UI while remaining clearly separate from signed-in operator surfaces. | Shared typography, spacing, and visual language across signed-out and signed-in surfaces without duplicating the full operator shell. | The delivered authentication-related surfaces look intentionally aligned with the refreshed UI, remain clearly distinct from the signed-in navigation shell, and use the same dark-default visual presentation before sign-in. |

## 6. Security Requirements

Use `SR1`, `SR2`, ... for security requirements.

| ID | Category | Requirement | Acceptance criteria |
| --- | -------- | ----------- | ------------------- |
| SR1 | Authentication/Authorization | The UI update and refactoring work must preserve the current authentication and authorization protections for the affected operator surfaces. | Protected operator routes and actions remain protected after the changes, with no reduction in the existing access-control behavior for the affected areas. |
| SR2 | Data Protection | The UI update and refactoring work must not expose secrets or sensitive platform data through new UI presentation changes. | The delivered UI does not reveal secret values or sensitive protected data that were not previously exposed to operators. |
| SR3 | Secrets/Key Management | The work must not introduce new checked-in secrets or secret-handling paths as part of the UI update or refactoring. | No new secrets are added to source-controlled files as part of this work package. |
| SR4 | Threats/Abuse Cases | Refactoring must not weaken existing safeguards on protected navigation, protected actions, or operator-only workflows. | Validation confirms that the affected operator workflows continue to enforce the intended protection boundaries after refactoring. |

## 7. Data Requirements (optional)

No additional data requirements are identified yet for this work package.

## 8. Interfaces and Integration Requirements (optional)

Use `IR1`, `IR2`, ... for integration requirements.

| ID | Requirement | System | Contract | Acceptance criteria | Notes |
| --- | ----------- | ------ | -------- | ------------------- | ----- |
| IR1 | The updated Web UI must continue to work with the platform's existing backend interfaces used by the affected operator workflows. | Platform API | Existing application HTTP/API interactions | The updated UI continues to complete the affected operator workflows using the existing backend interfaces unless an explicitly documented change is introduced in the technical specification. | Interface changes are not currently assumed. |

## 9. Testing Requirements

Use `TR1`, `TR2`, ... for testing requirements.

| ID | Requirement | Acceptance criteria | Notes |
| --- | ----------- | ------------------- | ----- |
| TR1 | The work package must include validation of the affected operator UI flows after the UI update and refactoring changes. | Automated or manual validation demonstrates that the affected in-scope UI flows continue to work as intended after the changes. | Exact test coverage will be refined in the technical specification. Validation should include focused checks of the shared shell and prioritized operator flows. |
| TR2 | The work package must validate that refactoring changes do not introduce regressions in the targeted scope. | Validation demonstrates that the targeted scope continues to behave as intended after refactoring. | The delivery plan should define the appropriate build, test, and any focused UI validation steps. |
| TR3 | The work package must validate the targeted AppHost-supported local/testing path affected by the refactoring changes. | Validation demonstrates that the targeted local/testing path continues to work using the existing containerized SQL and Keycloak resources only, without requiring additional supporting resources for that path. | Exact validation steps will be refined in the technical specification and delivery plan. |
| TR4 | The work package must include focused manual validation of the refreshed operator UI presentation states beyond build and existing automated checks. | Validation demonstrates the refreshed shared shell, prioritized pages, signed-in and signed-out header states, default dark theme behavior, basic light and dark theme states, immediate theme switching behavior, remembered theme preference behavior, and basic narrower-width usability behave as intended after the changes. | Intended as targeted manual validation rather than a full additional automation suite unless later specified. |

## 10. Operational Requirements (optional)

Use `OR1`, `OR2`, ... for operational requirements.

| ID | Requirement | Acceptance criteria | Notes |
| --- | ----------- | ------------------- | ----- |
| OR1 | The work package must update operator-facing and local-development documentation if the delivered UI or project structure changes require it. | Relevant documentation is updated before the work package is considered complete when the implemented solution changes operator guidance, runtime understanding, or local-development expectations. | Applies to affected `docs/wiki/` pages as needed. |
| OR2 | The work package must keep the affected AppHost local orchestration guidance aligned with the delivered testing and local runtime approach. | Documentation and operator/developer guidance remain accurate for the targeted AppHost-supported local/testing path after the changes. | Applies when the delivered work changes AppHost composition, local orchestration expectations, or the targeted testing path. |

## 11. Assumptions, Risks, and Dependencies

### 11.1 Assumptions

- The existing Web project remains the platform's operator UI for this work package.
- The work package is intended to improve the current UI and supporting code rather than introduce a new product capability area.
- Existing authentication and authorization behavior remains part of the baseline that must be preserved unless explicitly changed later.
- The AppHost project is in scope only where it supports Aspire-aligned orchestration improvements and simplification of the targeted testing path.

### 11.2 Risks

- **R1: Scope drift risk**: UI improvement work could expand into unrelated feature development.
  - **Mitigation**: Keep the work package focused on usability, presentation consistency, and maintainability for the targeted areas.
- **R2: Regression risk**: Broader refactoring across the project could break existing operator workflows or shared behavior relied on by the Web project.
  - **Mitigation**: Define focused validation for the affected UI flows and related shared behavior, and preserve existing protected behavior.
- **R3: Incomplete targeting risk**: Without clear page and workflow priorities, the package could improve low-value areas first.
  - **Mitigation**: Start with shared layout, navigation, and reusable components, then prioritize the landing/home, status, and configuration pages before lower-priority surfaces.

### 11.3 Dependencies

- Existing Web project UI in `src/TNC.Trading.Platform.Web`.
- Existing authenticated operator workflows delivered by prior work packages.
- Existing AppHost-based local orchestration in `src/TNC.Trading.Platform.AppHost`.
- Project-level business requirements in `../business-requirements.md`.
- Project-level systems analysis in `../systems-analysis.md`.

## 12. Open Questions

No open questions are currently identified for this work package.

## 13. Appendix (optional)

- Related systems analysis candidate: `../systems-analysis.md` work package `004-ui-update-and-refactor`.
