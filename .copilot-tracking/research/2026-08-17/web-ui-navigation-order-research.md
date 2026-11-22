<!-- markdownlint-disable-file -->
# Task Research: Web UI Navigation Order

Research the TNC Trading Platform Web UI navigation, with emphasis on how items are defined, rendered, and ordered.

## Task Implementation Requests

* Explain how navigation works in the Web UI.
* Identify the order in which navigation-bar items appear.

## Scope and Success Criteria

* Scope: The Blazor Web UI navigation components, their layout, and any supporting configuration or route definitions that determine visible order.
* Assumptions: The supplied screenshot represents the current operator sidebar navigation.
* Success Criteria:
  * Identify the source file and mechanism that determine display order.
  * Document the observed item order and its relationship to routes/pages.
  * Recommend the precise location to change ordering in a future implementation task.

## Outline

1. Navigation ownership and rendering flow
2. Ordered items and role-dependent visibility
3. Route authorization and responsive behavior
4. Alternatives and recommended change location

## Research Executed

### File Analysis

* src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor
  * Lines 10-29 directly declare the operator-workspace sidebar links in Razor source order.
  * Lines 45-48 load the shell context, and lines 72-75 only toggle collapse state.
* src/TNC.Trading.Platform.Web/Components/Layout/PlatformNavItem.razor
  * Lines 1-5 render each declared item as a `NavLink`; this component does not sort items.
* src/TNC.Trading.Platform.Web/Components/Routes.razor
  * Lines 1-11 set `Layout.MainLayout` as the routed-page shell.
* src/TNC.Trading.Platform.Web/Authentication/PlatformOperatorContext.cs
  * Lines 6-15 define the viewer, operator, and administrator role flags used for conditional visibility.
* src/TNC.Trading.Platform.Web/wwwroot/app.css
  * Lines 45-61, 134-184, and 454-473 alter layout, link presentation, and collapse behavior without changing item order.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs
  * Lines 14-77 cover signed-out suppression, selected viewer content, and collapse state, but not the complete ordered menu.

### Code Search Results

* `PlatformNavItem`
  * The sidebar declarations are in the shared `MainLayout`; no navigation registry, collection, or sort operation controls the traced path.

## Project Conventions

* The Web project uses Blazor routing with a shared `MainLayout` shell.
* Authorization is enforced on route targets through page-level policies; sidebar role checks are visibility controls.
* Instructions followed: .github/copilot-instructions.md and .github/instructions/folders.instructions.md, plus the applicable Markdown guidance.

## Key Discoveries

### Navigation Ownership and Flow

The sidebar is a declarative part of `MainLayout.razor`, not a list inferred from page routes. Application routing resolves a page through `Routes.razor`, which supplies `MainLayout` as the default layout. `MainLayout` obtains the current `PlatformOperatorContext` and, when `HasAnyPlatformRole` is true, emits the `<nav class="platform-sidebar">` markup.

Each `PlatformNavItem` is a child component that renders the supplied values as a `NavLink`. Razor renders these sibling declarations in their authored DOM sequence. Consequently, the source sequence in `MainLayout.razor` is the authoritative navigation order.

```text
Routes.razor
  -> MainLayout.razor
    -> PlatformShellContextProvider obtains role flags
    -> role-guarded PlatformNavItem declarations render in source order
      -> PlatformNavItem.razor renders NavLink elements
```

### Observed Order

For a user with all three platform roles, the top-to-bottom sidebar order is:

1. Home (`/`)
2. Status (`/status`)
3. IG login history (`/ig-login/history`)
4. Account details (`/account-details`)
5. Configuration (`/configuration`)
6. Authentication administration (`/administration/authentication`)

Home appears first for every platform user. The three viewer items appear next, in their declared order. Configuration follows when the user is an operator, and Authentication administration follows when the user is an administrator. The role checks are independent, so a user assigned multiple roles receives the union of relevant links while retaining this fixed sequence.

### Route and Access Relationship

The display condition and authorization policy align, but they are not the same mechanism:

* `Home.razor` lines 1-3 maps `/` and permits anonymous access; its navigation link is still shown only inside the platform-role sidebar.
* `Status.razor`, `IgLoginHistory.razor`, and `AccountDetails.razor` lines 1-3 use the viewer policy.
* `Configuration.razor` lines 1-3 uses the operator policy.
* `Administration.razor` lines 1-3 uses the administrator policy.

This means hiding a link does not grant or revoke access. Changing the sidebar changes navigation visibility and sequence, while changing a page policy changes authorization.

### Responsive Behavior

The desktop shell uses a two-column grid with the sidebar before main content. At the `960px` breakpoint it becomes a single-column layout, and collapsed desktop mode hides labels. Neither mode uses CSS `order`, reversed flex direction, or reassigned grid areas; the navigation stays in authored document order.

## Technical Scenarios

### Reorder Operator Workspace Navigation

**Requirements:**

* Preserve the intended role visibility for each entry.
* Change the top-to-bottom navigation sequence predictably across desktop, collapsed, and narrow-screen layouts.
* Keep route authorization independent from menu visibility.

**Preferred Approach:**

Edit the `<nav class="platform-sidebar">` block in src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor (lines 10-29). Move the relevant `PlatformNavItem` declaration or role block to its desired position. This is the single authoritative display-order source and produces the correct DOM sequence without CSS or route changes.

```razor
@if (operatorContext.IsViewer)
{
    <PlatformNavItem Href="/status" Icon="monitor_heart" Text="Status" />
    <PlatformNavItem Href="/ig-login/history" Icon="history" Text="IG login history" />
    <PlatformNavItem Href="/account-details" Icon="account_balance" Text="Account details" />
}
```

Add focused bUnit assertions in test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs that select `.platform-nav-item` and compare `href` values in DOM order. Cover at least a viewer, an operator, an administrator, and any supported combined-role identity.

#### Considered Alternatives

* Change route declaration order: rejected because individual page routes are independent and sidebar `Href` values are explicitly declared in `MainLayout.razor`.
* Change `PlatformNavItem.razor`: rejected because it renders one supplied item and has no knowledge of sibling sequence.
* Use CSS reordering: rejected because visual and DOM order could diverge, reducing keyboard-navigation clarity; the existing styles do not reorder the links.
* Introduce a navigation registry or service: rejected for a simple reorder because no existing abstraction requires it and direct markup is already the established owner.

## Potential Next Research

* Confirm production identity claim-to-role combinations before defining a comprehensive combined-role regression matrix.
  * Reasoning: The UI has independent Boolean role flags, while the identity provider determines which combinations occur in practice.
  * Reference: src/TNC.Trading.Platform.Web/Authentication/PlatformOperatorContext.cs lines 6-15.

## Actionable Next Steps

1. Treat the current sequence as an explicit UI contract if it is intentional.
2. Add ordered-link assertions before or alongside the next navigation change.
3. Reorder only the sidebar declarations in `MainLayout.razor`; retain target-page authorization policies.

## Technical Scenarios

Pending delegated codebase investigation.
