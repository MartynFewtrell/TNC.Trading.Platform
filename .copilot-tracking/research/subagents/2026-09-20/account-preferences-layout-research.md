<!-- markdownlint-disable -->
---
title: Account Preferences Layout Research
description: Research findings for the trailing stops account preferences layout adjustment.
author: GitHub Copilot
ms.date: 2026-09-20
ms.topic: reference
---

## Research Scope

* Locate the account preferences UI corresponding to trailing stops.
* Inspect applicable Blazor, Radzen, configuration, nearby style, and test evidence.
* Recommend the smallest accessible and responsive layout adjustment.

## Status

Complete. This research did not modify product, style, or test files.

## Located Surface

The requested screen is the authenticated operator route `/account-preferences`.
The page header and the confirmed-value editor both use the visible name
"Trailing stops".

* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:2
	declares the route.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:15
	renders the page header.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:41-54
	renders the configuration panel, radio choices, and Save action.

## Evidence

The current editor has a fieldset and an action group as siblings inside
`.account-preferences-control-row`. The native `InputRadioGroup` emits the
two labelled Boolean radio controls in source order: Enabled then Disabled.
The Save button follows them in the DOM and is already in an explicitly named
`role="group"`.

* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:43-52
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:105-116

The scoped CSS makes the outer control row a wrapping, vertically centred flex
row. It makes the radio-group child a wrapping flex row, so it can place
Disabled to the right of Enabled when sufficient inline space exists. The
action group has block margins and its button has a 44px minimum block size.

* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:57-87
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:89-105
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:143-146

Both the header and configuration are independent `.panel` elements. The
shared panel rule gives each 1.25rem padding, so the meaningful alignment
reference is the configuration panel's content edge, shared by its heading,
fieldset, and Save action.

* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:15
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:41-42
* src/TNC.Trading.Platform.Web/wwwroot/app.css:241-254

Existing E2E coverage deliberately locks the current horizontal desktop
geometry: it expects Save to begin 24px after the fieldset and share its
vertical centre. It also verifies no horizontal overflow at 360px and native
radio ArrowRight behavior. Unit coverage asserts the fieldset, labels, and
radio count without asserting layout.

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:87-118
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:122-130
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:139-151
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:115-135

## Applicable Guidance

The Radzen guidance prefers accessible labels, keyboard navigation, semantic
headings, and supported component or CSS parameters over fragile selectors.
The configuration guidance requires operator-managed settings to remain in the
Blazor UI. This change is presentation-only and does not change persistence,
the preference contract, or secret handling. The Playwright guidance calls for
web-first assertions and accessibility-first locators.

* .github/instructions/radzen-blazor.instructions.md:25-49
* .github/instructions/configuration.instructions.md:19-37
* .github/instructions/playwright.instructions.md:19-43

## Recommended Implementation

Make a CSS-only change in
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css.
Change `.account-preferences-control-row` from the current horizontal wrapping
flex row to a start-aligned vertical flex stack:

```css
.account-preferences-control-row {
		display: flex;
		flex-direction: column;
		align-items: flex-start;
		gap: 0;
		margin: 1.25rem 0;
}
```

Keep the existing fieldset, radio group, action-group markup, and
`.account-preferences-choice > div` rule. With the fieldset no longer sharing
its inline row with Save, its radio-group flex container has the panel's
available inline space. Disabled remains immediately to the right of Enabled
at normal and 360px viewports. The existing action-group top margin places
Save below the choices, aligned to the same panel content edge as the heading
above it.

Update the desktop geometry E2E test at
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:87-118.
Replace its horizontal gap and shared-centre assertions with checks that:

* Enabled and Disabled are on the same row, with Disabled to the right.
* Save starts below the radio choices and has the same left edge as the fieldset.
* The document has no horizontal overflow at 1280px.

Retain the narrow-width overflow test and ArrowRight keyboard test unchanged.
No unit-test markup change is necessary because the fieldset, labels, action
name, button test ID, and DOM order remain unchanged.

## Accessibility And Responsive Considerations

* Preserve the fieldset and legend. They convey the radio-group label and its
	grouping semantics.
* Preserve source and tab order: Enabled, Disabled, then Save. The proposed
	CSS matches the visual reading order, avoiding an order-only visual change.
* Preserve the existing label `:focus-within` outline, radio accent colour,
	44px targets, button focus-visible outline, and named Preference actions
	group.
* Keep `flex-wrap` on `.account-preferences-choice > div`. On unusually narrow
	widths or enlarged text, choices may wrap rather than overflow; Save stays
	below the group.
* Do not give the fieldset or button a fixed width. The existing `min-inline-size: 0`
	and wrapping behavior support narrow screens.

## Alternatives

1. Use `display: grid` with `justify-items: start` on the existing control row.
	 This achieves the same vertical result but adds no benefit over a flex
	 column for two ordered blocks.
2. Add a new inner wrapper around the fieldset and action group, then style it
	 as a vertical stack. Choose this only if a future requirement needs a
	 separately named editor region or a different desktop layout. It is more
	 markup than this presentation-only request needs.
3. Use CSS `order` to move Save below the choices while retaining the row.
	 Do not choose this approach because visual and keyboard order can diverge
	 and the behaviour under text scaling is harder to reason about.

## Validation

After implementation, run the focused AccountPreferences E2E test class and
the Web unit tests. Exercise a 1280px viewport, 360px viewport, keyboard
ArrowRight selection, and normal browser zoom or enlarged text. Confirm the
existing no-horizontal-overflow checks, focus indicators, named action group,
and Save confirmation focus return remain intact.
