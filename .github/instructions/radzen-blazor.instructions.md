---
description: 'Define enforceable repository guidance for using Radzen Blazor free resources and components consistently, accessibly, and maintainably across Blazor UI work.'
applyTo: '**/*.razor, **/*.cs, **/*.css'
---

# Radzen Blazor free resource and component usage guidelines

## Overview

These instructions define how contributors should use Radzen Blazor free, open-source resources in this repository's Blazor application work. The goal is consistent component adoption, correct setup, accessible forms and data views, predictable theming, and maintainable UI integrations aligned with Blazor guidance.

In this repository, Radzen Blazor free components are the preferred default choice for Blazor UI development when a shared component library is needed.

## Scope

Applies to: `**/*.razor, **/*.cs, **/*.css`

- These rules apply when adding, replacing, or modifying Radzen Blazor free components, related setup code, and supporting styles.
- These rules cover only free Radzen resources: Radzen Blazor Components, free themes, demos, API docs, GitHub source, getting started guidance, help content, roadmaps, blog guidance, and community forum content when used as implementation references.
- These rules do not apply to premium-only Radzen Studio, Radzen Blazor for Visual Studio, premium themes, or paid support workflows unless a task explicitly requires them.

## Instructions

### MUST

- Use only free, open-source Radzen Blazor Components for shared UI controls unless a task explicitly authorizes a different library.
- Treat Radzen Blazor free components as the preferred default component library for Blazor UI development in this repository.
- Install and reference Radzen using the official setup pattern:
  - add the `Radzen.Blazor` package
  - add `@using Radzen` and `@using Radzen.Blazor` in shared imports
  - register `builder.Services.AddRadzenComponents();`
  - include `<RadzenTheme Theme="..." />` in the app head
  - include the official `Radzen.Blazor.js` script from `_content/Radzen.Blazor/`
- Ensure any interactive Radzen component runs in an interactive Blazor render mode. Use the application's configured render mode or set `@rendermode` explicitly when required.
- When dialogs, notifications, tooltips, or context menus are used, configure and host the required Radzen infrastructure in the shared app shell instead of duplicating setup inside pages.
- Prefer Radzen components for common Blazor UI needs before building custom equivalents:
  - forms and validated inputs
  - data grids, lists, filtering, sorting, and paging
  - dialogs, notifications, tabs, accordions, steps, menus, and responsive layout
  - charts, scheduler, tree, timeline, and utility components when the feature genuinely needs them
- Use Radzen demos, API docs, getting-started guidance, and the GitHub repository as the primary implementation references before inventing custom patterns.
- Keep forms aligned with Blazor validation patterns using `EditForm`, `EditContext`, `DataAnnotationsValidator`, validation messages, and typed models.
- Preserve accessibility for all Radzen usage:
  - provide meaningful labels, placeholder text only as supplemental guidance, and visible validation feedback
  - preserve keyboard navigation and focus order
  - keep dialog titles, button text, and status text explicit
  - use semantic headings and surrounding page structure
- Configure data-heavy components to avoid excessive rendering cost:
  - use paging, virtualization, or incremental loading for large collections
  - avoid binding grids to unbounded result sets
  - avoid unnecessary rerenders in repeated component trees
- Prefer built-in free themes only. Treat Material, Standard, Default, Humanistic, and Software theme families as the allowed baseline unless the task explicitly approves premium themes.
- Centralize theme selection and global Radzen styling. Put shared visual decisions in app-level configuration or shared styles rather than page-specific overrides.
- Use component parameters, templates, and CSS variables before resorting to markup-dependent CSS overrides.
- Use `@key` in reusable templated or collection-rendering components when preserving item identity or focus matters.
- Validate Radzen-based behavior changes with relevant tests when the change affects user interaction, navigation, forms, or visible state.

### SHOULD

- Prefer Radzen DataGrid for CRUD-style tabular experiences that need sorting, filtering, paging, templating, or export-oriented workflows.
- Prefer simpler Radzen inputs and layout primitives before introducing advanced composite components.
- Prefer templated Radzen components only when reuse or customization clearly outweighs simpler Razor markup.
- Prefer repository-wide theme consistency over per-page branding experiments.
- Prefer official demos and API examples that match the exact component and scenario being implemented.
- Check Radzen roadmaps and changelog-style sources before creating workaround code for missing features.
- Use the community forum and help resources to confirm intended usage patterns when official demos or API docs are ambiguous.
- Keep validation models in C# files instead of declaring them inside `.razor` files when building complex or reusable validated forms.

### MUST NOT

- MUST NOT mix multiple competing component libraries for the same interaction pattern in the same feature without a documented reason.
- MUST NOT introduce premium-only Radzen themes or tooling as an implicit dependency of normal development.
- MUST NOT bypass Blazor validation with ad hoc JavaScript for standard form scenarios.
- MUST NOT depend on fragile CSS selectors that target generated markup or internal theme structure when supported parameters, templates, or CSS variables exist.
- MUST NOT use complex Radzen components where plain HTML or built-in Blazor components are sufficient.
- MUST NOT copy demo code blindly; adapt it to repository architecture, naming, authorization, validation, and testing standards.
- MUST NOT load large datasets into grids or visual components without paging, filtering, or virtualization when the user only sees a subset at a time.

## Output and Validation (optional)

- Add or update Radzen usage only in files that match the declared scope.
- Validate success by confirming:
  - Radzen package and app-level setup remain consistent
  - interactive components render in an interactive mode
  - forms still validate correctly
  - keyboard navigation and visible labeling remain intact
  - solution build and relevant UI tests pass when behavior changes

## References (optional)

- https://www.radzen.com/
- https://blazor.radzen.com/get-started
- https://blazor.radzen.com/
- https://blazor.radzen.com/docs/api
- https://github.com/radzenhq/radzen-blazor
- https://forum.radzen.com/
- https://www.radzen.com/help
- https://www.radzen.com/roadmaps
- https://learn.microsoft.com/aspnet/core/blazor/performance/rendering
- https://learn.microsoft.com/aspnet/core/blazor/forms/validation
- https://learn.microsoft.com/aspnet/core/blazor/components/templated-components

## Notes (optional)

- External sources used:
  - https://www.radzen.com/
  - https://blazor.radzen.com/get-started
  - https://raw.githubusercontent.com/radzenhq/radzen-blazor/master/README.md
  - https://learn.microsoft.com/aspnet/core/blazor/performance/rendering
  - https://learn.microsoft.com/aspnet/core/blazor/forms/validation
  - https://learn.microsoft.com/aspnet/core/blazor/components/templated-components
