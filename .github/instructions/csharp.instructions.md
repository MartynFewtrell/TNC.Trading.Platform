---
description: 'Standardize C# contributions (formatting, naming, nullability, error handling, and test expectations) so code is consistent, maintainable, and production-ready.'
applyTo: '**/*.cs'
---

# C# development guidelines

## Overview

These instructions define baseline rules for writing and changing C# code in this repository. The goal is consistent style, safe nullability, predictable error handling, and changes that remain testable.

## Scope

Applies to: `**/*.cs`

- These rules apply to all C# files under `src/` and `test/`.
- For test code, also follow `/.github/instructions/tests.instructions.md` and (when applicable) `/.github/instructions/playwright.instructions.md`.
- For auth-related work, also follow `/.github/instructions/auth.instructions.md`.

## Instructions

### MUST

- Follow formatting rules defined in `.editorconfig`.
- Use the C# language version already configured by the repository.
  - Do not change `LangVersion` (or rely on preview language features) as part of feature work unless the work item explicitly requires it.

- Follow naming conventions:
  - Use PascalCase for types, methods, and public members.
  - Use camelCase for local variables.
  - Prefix interface names with `I`.

- Write null-safe code:
  - Respect nullable reference types annotations.
  - Prefer non-nullable types and validate inputs at boundaries.
  - Prefer `is null` / `is not null` pattern checks.

- Handle errors intentionally:
  - Do not swallow exceptions.
  - When catching, add context and either rethrow or translate into a clear, typed outcome.
  - For HTTP APIs, prefer returning standardized errors (Problem Details / RFC 9457) at the boundary.

- Keep authentication implementation aligned with repo standards:
  - If changing auth flows, OIDC configuration, token validation, or identity provider integration, follow `/.github/instructions/auth.instructions.md`.

- Add or update tests when changing behavior:
  - Changes to business logic or external behavior MUST include appropriate unit/integration/functional test updates.
  - Test code MUST NOT add `Arrange/Act/Assert` comments.

- Document public APIs:
  - Public APIs MUST have XML doc comments.
  - Comments MUST explain intent and constraints; do not restate what the code already makes obvious.

### SHOULD

- Prefer file-scoped namespaces when the project already uses them.
- Prefer pattern matching and switch expressions when they reduce complexity.
- Prefer `nameof(...)` over string literals when referencing members.
- Prefer `async`/`await` end-to-end for I/O and avoid blocking calls.

### MUST NOT

- MUST NOT introduce hard-coded secrets or credentials in code.
- MUST NOT use `async void` except for event handlers.
- MUST NOT block on asynchronous code (for example `.Result`, `.Wait()`) in request-handling paths.

## Output and Validation (optional)

- Validate success:
  - `dotnet build`
  - `dotnet test`

## References (optional)

- `/.github/instructions/dotnet-stack.instructions.md`
- `/.github/instructions/auth.instructions.md`
- `/.github/instructions/tests.instructions.md`
- `/.github/instructions/playwright.instructions.md`
