---
title: Web UI Navigation Trace
summary: Evidence-backed trace of the operator workspace navigation definition, order, rendering path, and coverage.
author: GitHub Copilot Researcher Subagent
ms.date: 2026-08-17
ms.topic: reference
keywords:
  - blazor
  - navigation
  - operator workspace
  - research
---

## Research Scope

Investigate the authoritative definition and display order of navigation items in the TNC.Trading.Platform operator workspace. Inspect only the minimal relevant navigation/layout components, routed page targets, and applicable tests. Do not modify production code.

## Status

Complete. The authoritative navigation definition, route targets, responsive behavior, and relevant test coverage were inspected.

## Executive Findings

`src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` is the single authoritative definition of the operator workspace sidebar and its display order. It declares each `PlatformNavItem` directly in Razor markup; there is no separate navigation registry, data-driven collection, or sort operation in the traced UI path.

The sidebar is only rendered when the resolved operator context has at least one platform role. Its item blocks are evaluated and emitted in source order, so role membership determines inclusion but does not reorder included links.

## Observed Navigation Order

The following is the declaration and resulting top-to-bottom order for a user who has all platform roles:

1. Home: `/` - always present inside the platform-role sidebar
2. Status: `/status` - viewer role
3. IG login history: `/ig-login/history` - viewer role
4. Account details: `/account-details` - viewer role
5. Configuration: `/configuration` - operator role
6. Authentication administration: `/administration/authentication` - administrator role

The source does not make the role blocks mutually exclusive. A user with multiple roles receives the union of the applicable blocks in the fixed order above. Home remains first for every user who receives the sidebar.

## Evidence

### Definition and Rendering Flow

* `src/TNC.Trading.Platform.Web/Components/Routes.razor` lines 1-11 wraps application routing in `CascadingAuthenticationState` and uses `AuthorizeRouteView` with `Layout.MainLayout` as the default layout. Routed pages therefore render through the shared shell.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` lines 1-9 resolves `PlatformShellContextProvider` and renders the header and shell body.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` lines 10-29 is the authoritative sidebar declaration. The `HasAnyPlatformRole` guard controls the entire `<nav>`; Home is declared first; the viewer, operator, and administrator `if` blocks then emit their links in sequence.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` lines 45-48 loads the operator context during initialization. Lines 72-75 implement only the collapse-state toggle; they do not alter the item list or sequence.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformNavItem.razor` lines 1-5 renders each declaration as a Blazor `NavLink`, preserving the parent markup sequence. Lines 8-21 define its link, icon, text, and match parameters; the default `NavLinkMatch.Prefix` controls active-state matching, not order.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformOperatorContext.cs` lines 6-15 defines `IsViewer`, `IsOperator`, `IsAdministrator`, and `HasAnyPlatformRole`. The final value is an OR of the three role booleans.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformShellContextProvider.cs` lines 11-14 obtains and caches the current operator context. Lines 19-23 decline environment loading for users without any platform role; that behavior affects the environment badge only and does not define sidebar items.

### Route Targets and Access Boundaries

* `src/TNC.Trading.Platform.Web/Components/Pages/Home/Home.razor` lines 1-3 maps `/` and allows anonymous access. It is the target of the sidebar's first item, but the layout only displays that link to users with a platform role.
* `src/TNC.Trading.Platform.Web/Components/Pages/Status/Status.razor` lines 1-3 maps `/status` and requires the viewer policy.
* `src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/IgLoginHistory.razor` lines 1-3 maps `/ig-login/history` and requires the viewer policy.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` lines 1-3 maps `/account-details` and requires the viewer policy.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor` lines 1-3 maps `/configuration` and requires the operator policy.
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor` lines 1-3 maps `/administration/authentication` and requires the administrator policy.

The sidebar role guards mirror these target-page policy categories. Authorization remains enforced by `AuthorizeRouteView` and each target's `[Authorize]` attribute; the sidebar is a visibility affordance rather than the authorization boundary.

### Styling and Responsive Behavior

* `src/TNC.Trading.Platform.Web/wwwroot/app.css` lines 45-61 makes the desktop body a two-column grid with the sidebar before the main content. Lines 134-141 define sidebar positioning and appearance.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` lines 157-174 styles each link as a block-level flex row and changes only visual active/hover state.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` lines 176-184 hides the brand and link text in collapsed desktop mode; it does not remove, reorder, or otherwise change links. `MainLayout.razor` lines 72-75 supplies this state.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` lines 454-473 applies the `960px` responsive breakpoint. The body becomes one column, the sidebar remains before content in document/grid order, and collapsed labels/icons are restored. No CSS ordering rule (`order`, flex reverse, or grid-area reassignment) was found in the traced styles.

## Relevant Test Coverage

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs` lines 14-30 verifies that a signed-out user receives a full-width shell without the primary navigation.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs` lines 39-56 verifies a viewer sees the workspace label and selected navigation text (`Status` and `Account details`), plus the environment badge.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs` lines 65-77 verifies the header toggle applies the collapsed-shell class.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs` lines 142-147 maps the fixture identities to default viewer, operator, and administrator scopes. It can support role-variant layout tests, although the current `MainLayoutTests` only exercises the viewer and signed-out shell cases.

No inspected unit, functional, or end-to-end test asserts the complete navigation sequence, the presence of `IG login history` or `Configuration` in the layout, the administrator link, or role-union ordering. Page-specific tests exist for individual targets, but they do not test shell navigation ordering.

## Recommendation

Future ordering changes belong in `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`, specifically the `<nav class="platform-sidebar">` block at lines 10-29. Reorder the corresponding `PlatformNavItem` declarations or their role blocks there; do not change `PlatformNavItem.razor`, routes, or CSS to alter semantic menu order.

For a behavior change, add focused bUnit assertions in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs` that query `.platform-nav-item` and compare their `href` values or visible text in document order for each relevant role combination. That gives direct regression protection for the ordering contract while preserving route authorization as a separate concern.

## Evaluated Alternatives

* A separate navigation configuration, registry, or service was considered as a possible owner. The traced layout contains direct markup declarations, and no such owner was found in the minimal navigation path.
* Route declaration order was considered as a possible ordering mechanism. Each page supplies its route independently, while the sidebar explicitly names `Href` values in its own order; route order therefore does not control display order.
* CSS ordering was considered because the shell supports a collapsed sidebar and a narrow-screen layout. The inspected styles change grid structure and label visibility but include no rule that changes navigation item ordering.

## Gaps

* The current suite does not provide a complete ordered-list regression assertion across viewer, operator, administrator, and combined-role cases.
* The exact role combinations emitted by the production identity provider were outside the requested minimal UI trace. The UI contract is explicit for each boolean role flag, but real claim-to-role mapping was not inspected.
* No browser rendering test was run because this was research-only and the source plus bUnit tests were sufficient to establish authored DOM order and responsive CSS behavior.

## Follow-On Questions

* Should the documented display order be elevated into an explicit test contract for all supported role combinations before the next navigation change?
