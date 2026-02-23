---
description: 'Standardize Playwright .NET test contributions (structure, locator strategy, assertions, and flake avoidance) so UI tests are readable, resilient, and repeatable.'
applyTo: 'test/**/*.cs, test/**/*.csproj'
---

# Playwright .NET testing guidelines

## Overview

These instructions define how UI tests are authored with Playwright for .NET in this repository. The goal is resilient, accessible, low-flake tests that are easy to review and diagnose.

## Scope

Applies to: `test/**/*.cs, test/**/*.csproj`

- Applies when creating or updating Playwright UI tests.
- These rules are additive to `/.github/instructions/folder-structure.instructions.md` (tests live under `test/`).

## Instructions

### MUST

- Keep Playwright tests under `test/`.
- Use one Playwright test framework per test project:
  - xUnit: `Microsoft.Playwright.Xunit`
- Prefer inheriting from `PageTest` for Playwright tests.
- Use Playwright web-first assertions (`Expect(...)`) for UI assertions so checks auto-retry.
- Use `Test.StepAsync()` to group interactions into meaningful steps.

- Prefer user-facing, accessibility-first locators:
  - `GetByRole`, `GetByLabel`, `GetByPlaceholder`, `GetByText`
  - Use `GetByTestId` only when a stable `data-testid` exists and role/label/text locators are not practical.

- Rely on Playwright auto-waiting.
  - Avoid explicit sleeps and time-based waits.
  - Prefer waiting via assertions (for example `ToHaveTextAsync`, `ToBeVisibleAsync`, `ToHaveURLAsync`).

### SHOULD

- Keep one test class focused on one feature/page area.
- Name test files using `<FeatureOrPage>Tests.cs`.
- Use `ToMatchAriaSnapshotAsync` to validate accessibility tree structure for components where markup is expected to be stable.
- Use `ToHaveURLAsync` after navigation-triggering actions.

### MUST NOT

- MUST NOT use hard-coded delays or time-based waits (for example `Thread.Sleep`, `Task.Delay`, `WaitForTimeoutAsync`).
- MUST NOT increase global/default timeouts as the primary way to address flakiness.
- MUST NOT prefer brittle selectors (deep CSS chains, XPath) when role/label/text-based locators are available.

## Output and Validation (optional)

- Expected artifacts:
  - Playwright test projects and tests under `test/`.
  - Test files named `<FeatureOrPage>Tests.cs`.

- Validate success:
  - `dotnet test`

## References (optional)

- `/.github/instructions/folder-structure.instructions.md`
- https://playwright.dev/dotnet/
