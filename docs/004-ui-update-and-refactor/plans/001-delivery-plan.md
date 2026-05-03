# UI Update and Refactor Delivery Plan

## Summary

- **Source**: See `../requirements.md` for canonical work metadata (work item, owner, dates, links) and scope. See `../../business-requirements.md` for project-level business context.
- **Status**: draft
- **Inputs**:
  - `../../business-requirements.md`
  - `../requirements.md`
  - `../technical-specification.md`

## Description of work

Deliver the `004-ui-update-and-refactor` work package as incremental, testable slices that improve the authenticated operator Web UI, preserve the existing protected routes and backend integrations, introduce shared theme and shell infrastructure, refresh the prioritized home, status, configuration, and authentication-related surfaces, and apply the scoped AppHost cleanup required to keep the SQL Server and Keycloak-backed local/testing path simple and maintainable. The plan is bounded to usability, presentation consistency, maintainability, validation, and required `docs/wiki/` updates; it does not add new trading capabilities or change the project-level business goals.

## Delivery approach

- **Delivery model**: single PR
- **Branching**: Deliver the work on `004-ui-update-and-refactor` as one reviewable PR, using the work items in this plan as internal implementation and review checkpoints before merge.
- **Dependencies**: Existing Web UI in `src/TNC.Trading.Platform.Web`; existing AppHost composition in `src/TNC.Trading.Platform.AppHost`; existing Platform API integrations through `PlatformApiClient`; existing SQL Server and Keycloak-backed local/test path.
- **Key risks**:
  - UI scope drift beyond the prioritized shared shell and target pages; mitigate by keeping non-prioritized pages limited to shared-shell inheritance only.
  - Regression in protected operator workflows during refactoring; mitigate with baseline/pre-completion build and test gates plus focused manual flow validation.
  - AppHost cleanup destabilizing the local/testing path; mitigate by limiting changes to structure and wiring clarity while validating against the existing SQL Server and Keycloak resources only.

## Delivery Plan

### Execution gates (required)

Before starting *any* work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

The final plan includes five incremental work items.

| Work item | Description | Traceability (requirements) | Traceability (spec sections) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Establish UI foundation | Add Radzen-first UI infrastructure, shared theme state and browser persistence, shared shell primitives, and baseline styling tokens without changing page routes or protection boundaries. | FR1, FR2, FR3, FR4, FR6, FR7, NF1, NF2, NF3, NF6, NF10, NF11, SR1, SR3, SR4, IR1, TR1, TR2 | 3.1 (layers 1-2), 3.3, 4 (FR1, FR2, FR3, FR4, FR6, FR7, NF1, NF2, NF3, NF6, NF10, NF11, SR1, SR3, SR4, IR1, TR1, TR2), 5.3 steps 1-3, 5.4, 5.5, 6, 8, 9 phase 1-2 | Baseline repository build/test pass | `dotnet build`; `dotnet test`; manual verification of dark default, header rendering, sidebar expanded default, sidebar collapse behavior, preserved navigation order, and signed-in header state | Revert the shared shell, theme-service, and app-style changes in the PR to restore the existing shell and theme behavior | Review the shell in a signed-in session on desktop/laptop widths and confirm navigation order, routes, and auth state remain unchanged |
| Work Item 2: Refresh signed-out and overview surfaces | Refresh the signed-out/authentication presentation and the home overview surface using the shared shell/theme foundation and existing data flows. | FR1, FR2, FR4, FR6, FR7, FR8, NF2, NF3, NF4, NF6, NF7, NF10, NF11, NF12, SR1, SR2, SR4, IR1, TR1, TR2, TR4 | 3.1 (layers 3-4 for home/auth surfaces), 3.3, 4 (FR1, FR2, FR4, FR6, FR7, FR8, NF2, NF3, NF4, NF6, NF7, NF10, NF11, NF12, SR1, SR2, SR4, IR1, TR1, TR2, TR4), 5.1, 5.2, 5.3 steps 4-5, 5.4, 6, 8, 9 phase 2-3 | Work Item 1 | `dotnet build`; `dotnet test`; manual verification of signed-out hero layout, sign-in affordance, default dark presentation before sign-in, home overview summary card, compact alerts list, recent activity reuse, and narrower-width usability | Revert the home/auth surface PR to restore the prior presentation while retaining the shared shell foundation from Work Item 1 | Validate signed-out and signed-in entry flows, and confirm the home page remains the same route and identity while presenting the new overview content |
| Work Item 3: Refresh status and configuration flows | Rebuild the status and configuration pages into accordion-driven, maintainable surfaces, preserve edits across configuration section changes, and expose the secondary configuration-page theme switcher. | FR1, FR2, FR3, FR6, FR7, NF1, NF2, NF3, NF4, NF6, NF7, NF8, NF9, NF10, NF11, SR1, SR2, SR4, IR1, TR1, TR2, TR4 | 3.1 (layer 3 for prioritized pages), 3.3, 4 (FR1, FR2, FR3, FR6, FR7, NF1, NF2, NF3, NF4, NF6, NF7, NF8, NF9, NF10, NF11, SR1, SR2, SR4, IR1, TR1, TR2, TR4), 5.1, 5.2, 5.3 steps 6-9, 5.4, 6, 8, 9 phase 3 | Work Items 1-2 | `dotnet build`; `dotnet test`; manual verification of status accordion grouping, default collapsed lower-priority sections, multi-open behavior, simple title-only collapsed headers, configuration accordion behavior, preserved in-progress edits, immediate theme switching from configuration, and no secret exposure | Revert the status/configuration refresh PR to restore the prior page layouts while keeping previously merged shared infrastructure intact | Walk through the status and configuration flows with existing operator credentials and confirm current workflows, routes, and save behavior remain intact |
| Work Item 4: Apply AppHost cleanup and validation path refinements | Refactor AppHost structure for readability and maintainability while preserving the existing SQL Server and Keycloak-only local/testing composition and runtime expectations. | FR3, FR5, NF1, NF2, NF5, SR1, SR3, SR4, TR2, TR3, OR2 | 3.1 (layer 6), 3.3, 4 (FR3, FR5, NF1, NF2, NF5, SR1, SR3, SR4, TR2, TR3, OR2), 5.2, 5.3 step 10, 5.4, 5.5, 6, 7, 8, 9 phase 4 | Work Items 1-3 | `dotnet build`; `dotnet test`; manual/local validation that the AppHost-backed path still runs with the existing SQL Server and Keycloak resources only and no new required supporting resources | Revert the AppHost cleanup PR to restore the prior composition structure if startup or local/testing validation regresses | Run the standard local path with existing SQL Server and Keycloak containers and confirm no new resources or operator steps are required |
| Work Item 5: Complete validation and documentation | Execute focused regression and presentation validation across the refreshed UI and AppHost path, then update affected `docs/wiki/` pages and verify wiki links. | TR1, TR2, TR3, TR4, OR1, OR2, NF2, NF3, NF5, NF7, NF10, NF11, NF12, SR1, SR2, SR4 | 4 (TR1, TR2, TR3, TR4, OR1, OR2, NF2, NF3, NF5, NF7, NF10, NF11, NF12, SR1, SR2, SR4), 5.3 step 11, 7, 8, 9 phase 5 | Work Items 1-4 | `dotnet build`; `dotnet test`; focused manual validation of shared shell, prioritized pages, signed-in/signed-out states, theme switching, remembered preference behavior, narrower widths, AppHost path, and wiki link review | Revert or hold the final documentation/validation PR until discrepancies are resolved; if needed, back out the latest offending implementation PR before completion | Review the updated wiki guidance, re-run the documented local path, and confirm the documentation matches the delivered UI and validation approach |

### Work Item 1 details

- [ ] Work Item 1: Establish UI foundation
  - [ ] Build and test baseline established
  - [ ] Task 1: Add shared UI infrastructure and theme registration
    - [ ] Step 1: Register Radzen UI services, assets, and shared startup wiring while keeping `Program.cs` focused on orchestration
    - [ ] Step 2: Add the shared theme state and browser persistence support with dark mode as the default when no preference exists
    - [ ] Step 3: Add shared CSS tokens and theme-aware styling aligned to the Radzen Software family
  - [ ] Task 2: Refactor the signed-in shell into reusable layout primitives
    - [ ] Step 1: Refresh `MainLayout.razor` with the compact utility-style header and preserved left navigation
    - [ ] Step 2: Add reusable header, environment badge, navigation item, and theme toggle components or equivalents
    - [ ] Step 3: Implement session-default expanded navigation with desktop/laptop collapse support that preserves icons
  - [ ] Task 3: Validate shared shell behavior and regression boundaries
    - [ ] Step 1: Verify routes, page names, navigation order, and auth protection remain unchanged
    - [ ] Step 2: Verify dark-default rendering, signed-in header state, and narrower-width shell usability
  - [ ] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [ ] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/Program.cs`: Register shared UI infrastructure and keep startup orchestration concise
    - `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`: Introduce the refreshed signed-in shell
    - `src/TNC.Trading.Platform.Web/wwwroot/app.css`: Add theme-aware shared styling tokens and shell styles
    - New or updated shared Web UI support files under `src/TNC.Trading.Platform.Web`: Add theme state, shared components, and related shell support files as needed
  - **Work Item Dependencies**: Must be delivered first because later page refreshes depend on the shared shell, theme infrastructure, and reusable component patterns.
  - **User Instructions**: Review the updated shell in a signed-in browser session, test the collapse toggle, and confirm the header shows the expected site title, auth state, and environment indicator behavior.

### Work Item 2 details

- [ ] Work Item 2: Refresh signed-out and overview surfaces
  - [ ] Build and test baseline established
  - [ ] Task 1: Refresh signed-out and authentication-related presentation
    - [ ] Step 1: Apply the refined dark-default visual language to sign-in and access-related surfaces without the signed-in shell
    - [ ] Step 2: Implement the narrower hero-style single-column layout and sign-in-focused presentation
  - [ ] Task 2: Refresh the home overview page
    - [ ] Step 1: Reframe the home page as the concise operator overview without changing its route or identity
    - [ ] Step 2: Add the combined operational status summary card
    - [ ] Step 3: Add compact, non-clickable alert and notable-event summaries plus reusable recent activity content when available
  - [ ] Task 3: Validate overview and auth presentation states
    - [ ] Step 1: Verify signed-out and signed-in header/auth states behave as intended
    - [ ] Step 2: Verify home overview content hierarchy and narrower-width usability
  - [ ] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [ ] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/Components/Pages/Home.razor`: Refresh home overview and signed-out presentation
    - `src/TNC.Trading.Platform.Web/Components/Pages/AccessDenied.razor`: Align access-denied presentation with the refreshed auth language
    - New or updated shared Web UI files under `src/TNC.Trading.Platform.Web`: Add overview and authentication presentation components as needed
    - Affected pages under `docs/wiki/`: Update operator-facing guidance for the refreshed overview and authentication surfaces
  - **Work Item Dependencies**: Depends on Work Item 1 shared infrastructure and shell patterns.
  - **User Instructions**: Validate the signed-out presentation and the signed-in home overview in the browser, confirming the new surfaces remain focused and non-disruptive.

### Work Item 3 details

- [ ] Work Item 3: Refresh status and configuration flows
  - [ ] Build and test baseline established
  - [ ] Task 1: Refresh the status page into grouped accordion sections
    - [ ] Step 1: Reorganize status content into a small number of readable sections or panels
    - [ ] Step 2: Default lower-priority sections to collapsed while allowing multiple sections to remain open
    - [ ] Step 3: Keep collapsed headers title-only and preserve the page’s current route and purpose
  - [ ] Task 2: Refresh the configuration page into grouped accordion sections
    - [ ] Step 1: Reorganize configuration content into a small number of editable sections aligned visually with status
    - [ ] Step 2: Preserve in-progress edits when switching between configuration sections
    - [ ] Step 3: Add the secondary in-context theme switcher with immediate apply behavior
  - [ ] Task 3: Consolidate reusable page patterns and guardrails
    - [ ] Step 1: Extract duplicated cards, sections, badges, empty/loading/error states, and related helpers where worthwhile
    - [ ] Step 2: Verify write-only secret handling and protected operator workflows remain unchanged
  - [ ] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [ ] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`: Refresh the status surface with accordion-driven grouping
    - `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor`: Refresh the configuration surface and preserve in-progress edits
    - New or updated shared Web UI files under `src/TNC.Trading.Platform.Web`: Add shared section, badge, and form-support components or helpers as needed
    - Affected pages under `docs/wiki/`: Update operator guidance for status, configuration, and theme-switching behavior
  - **Work Item Dependencies**: Depends on Work Items 1-2 for shared infrastructure and visual language consistency.
  - **User Instructions**: Validate status and configuration workflows with existing credentials, including section switching, save behavior, theme switching, and narrower-width usability.

### Work Item 4 details

- [ ] Work Item 4: Apply AppHost cleanup and validation path refinements
  - [ ] Build and test baseline established
  - [ ] Task 1: Refactor AppHost composition structure
    - [ ] Step 1: Simplify resource and project wiring for readability and maintainability
    - [ ] Step 2: Extract helper structure only where it improves clarity without changing supported resources or behavior
  - [ ] Task 2: Validate the targeted local/testing path
    - [ ] Step 1: Confirm the AppHost-backed path still relies only on the existing SQL Server and Keycloak resources
    - [ ] Step 2: Confirm no additional operator or local-development steps are introduced
  - [ ] Task 3: Align local-development documentation if affected
    - [ ] Step 1: Update the relevant `docs/wiki/` pages when AppHost structure or validation guidance changes
  - [ ] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [ ] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: Apply the broad but behavior-preserving composition cleanup
    - New or updated AppHost support files under `src/TNC.Trading.Platform.AppHost`: Add helper files only if needed to clarify AppHost structure
    - Affected pages under `docs/wiki/`: Update local-development and validation guidance if AppHost expectations change
  - **Work Item Dependencies**: Depends on the earlier UI work items so the final AppHost validation covers the refreshed UI and local path together.
  - **User Instructions**: Run the standard AppHost local path using the existing SQL Server and Keycloak containers and confirm the refreshed application starts without new supporting resources.

### Work Item 5 details

- [ ] Work Item 5: Complete validation and documentation
  - [ ] Build and test baseline established
  - [ ] Task 1: Execute focused regression and presentation validation
    - [ ] Step 1: Re-run the full build and test suite
    - [ ] Step 2: Verify shared shell, prioritized pages, auth states, theme switching, remembered preference, and narrower-width usability
    - [ ] Step 3: Verify the targeted AppHost-supported local/testing path remains operable with SQL Server and Keycloak only
  - [ ] Task 2: Complete documentation and link validation
    - [ ] Step 1: Update affected `docs/wiki/` pages for behavior, operator guidance, local development, and testing approach changes
    - [ ] Step 2: Verify affected wiki navigation and cross-links still resolve after the updates
  - [ ] Task 3: Close out the work package
    - [ ] Step 1: Confirm traceability coverage across implemented work items and validations
    - [ ] Step 2: Prepare the work package for review with rollback guidance preserved per increment
  - [ ] Relevant `docs/wiki/` pages updated to reflect the implemented changes
  - [ ] Build and test validation

  - **Files**:
    - Affected pages under `docs/wiki/`: Update implementation, operator, local-development, and testing guidance pages
    - `docs/004-ui-update-and-refactor/plans/001-delivery-plan.md`: Keep the final delivery plan aligned with the delivered work if sequencing or validation notes need refinement
  - **Work Item Dependencies**: Depends on completion of Work Items 1-4.
  - **User Instructions**: Review the updated wiki guidance, repeat the documented manual validation checklist, and confirm the final documentation reflects the delivered application behavior.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test`
- **Integration tests**: `dotnet test`
- **Manual checks**:
  - Validate the shared shell, preserved navigation order, header auth states, and environment indicator behavior.
  - Validate signed-out/authentication presentation, home overview hierarchy, status and configuration accordion behavior, and protected route behavior.
  - Validate dark-default rendering, immediate theme switching from the header and configuration page, remembered theme preference in the same browser, and basic narrower-width usability.
  - Validate the AppHost-supported local/testing path with the existing SQL Server and Keycloak resources only.
- **Security checks**:
  - Review protected route and operator-only workflow behavior for regressions.
  - Review configuration and refreshed UI surfaces to confirm no secret values or new secret-handling paths are exposed.
  - Review browser persistence changes to confirm only non-sensitive theme preference data is stored client-side.

## Acceptance checklist

- [ ] Work item aligns with `../business-requirements.md`.
- [ ] All referenced `FRx` requirements are implemented and validated.
- [ ] All referenced `NFx` requirements have measurements or checks.
- [ ] All referenced `SRx` security requirements are implemented and validated.
- [ ] Relevant `docs/wiki/` pages are updated to reflect the delivered implementation.
- [ ] Affected wiki links resolve after documentation updates.
- [ ] Rollback/backout plan documented for each work item.

## Notes

- This draft uses a single-PR delivery while retaining multiple internal work items so implementation, validation, and rollback reasoning stay incremental inside the same change set.
- If delivery risk increases during implementation, the work items can still be split into follow-up PRs without changing the traceability and validation structure.
