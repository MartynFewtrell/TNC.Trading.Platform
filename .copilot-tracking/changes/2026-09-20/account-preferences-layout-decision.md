<!-- markdownlint-disable-file -->
# Account Preferences Layout Decision

**Decision date:** 2026-09-20  
**Scope:** 1280 by 900 desktop viewport

## Approved Behavior

The Account Preferences choice controls may wrap at the 1280px desktop viewport. The accepted contract is that Enabled and Disabled remain visible, usable, and non-overlapping; Save remains visible below the choice fieldset; and the document has no horizontal overflow. A same-row or left-to-right X-position is not a product requirement.

## Evidence And Approval Basis

This decision records the existing Account Preferences UI contract for the failing-test resolution work: `AccountPreferences.razor.css` defines a vertical control row and a wrapping choice container, and the verification research found no requirement for same-row placement. The decision is approved for this work item because the requested resolution explicitly asks for responsive usability assertions consistent with that CSS contract.

## Test Consequence

`AccountPreferences_ShouldAlignChoiceAndSaveControls_WhenDesktopViewportIsWide` asserts visibility, non-overlap, Save placement, and no horizontal overflow. It does not assert that Disabled is to the right of Enabled or that both controls share a row.