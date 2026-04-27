---
description: 'Enforce safe, behavior-preserving refactoring practices so code becomes easier to maintain without introducing regressions.'
applyTo: 'src/**/*.cs, src/**/*.razor, src/**/*.razor.cs, test/**/*.cs'
---

# Refactoring guidance

## Overview

These instructions define how refactoring work should be performed in this repository. The goal is to improve readability, maintainability, testability, and structure without changing intended behavior unless the work item explicitly calls for a functional change.

## Scope

Applies to: `src/**/*.cs, src/**/*.razor, src/**/*.razor.cs, test/**/*.cs`

- Applies when restructuring existing production or test code under `src/` and `test/`.
- These rules are additive to `/.github/copilot-instructions.md`, `/.github/instructions/csharp.instructions.md`, `/.github/instructions/tests.instructions.md`, and any feature-specific instruction files.
- For Blazor code, apply these rules to component markup, code-behind, and related view-model logic together.

## Instructions

### MUST

- Preserve observable behavior unless the request explicitly includes a behavior change.
  - Treat routes, request and response shapes, rendered UI outcomes, validation rules, configuration keys, dependency injection registrations, service lifetimes, and persistence contracts as observable behavior.
- Apply established design principles when refactoring.
  - Use Separation of Concerns to keep UI, application, domain, infrastructure, and test responsibilities distinct.
  - Use the Single Responsibility Principle to ensure a type, component, or method has one clear reason to change.
  - Use Dependency Inversion and Explicit Dependencies to depend on abstractions and constructor-injected collaborators instead of hidden ambient state or concrete infrastructure details.
  - Use DRY to remove true duplication by centralizing shared behavior in one authoritative implementation.
- Prefer compiler-assisted or IDE-assisted refactorings for structural changes.
  - Use safe rename for symbols instead of manual search-and-replace.
  - Use extract method, extract local function, extract interface, move type to matching file, sync namespace and folder name, and move declaration near reference when those operations match the intended change.
- Review rename and move previews before applying changes when the refactoring spans multiple files, projects, or public-facing symbols.
- Keep each refactoring step small enough that build failures and regressions can be localized to a narrow change.
- Add or update automated tests when a refactor touches behavior-critical logic or when existing coverage is missing for the refactored path.
  - Regression-focused tests MUST verify behavior, not implementation details.
- Preserve public contracts unless the request explicitly authorizes changing them.
- Preserve nullable annotations, asynchronous behavior, cancellation flow, and exception semantics unless the request explicitly authorizes changing them.
- When extracting shared logic, introduce the narrowest abstraction that removes current duplication or clarifies an existing responsibility.
- When moving or splitting C# types, keep one top-level type per file and ensure the file name, type name, folder, and namespace remain aligned.
- Introduce seams for testability through explicit dependencies and dependency injection rather than through hidden ambient state, service location, or direct instantiation of infrastructure-heavy collaborators.
- Validate the result after refactoring by running a build and the relevant automated tests for the affected area.

### SHOULD

- Prefer extract method or extract local function when reducing duplication or breaking up long methods.
- Prefer moving declarations closer to usage when doing so narrows scope and improves readability.
- Prefer cohesive component or service boundaries over large utility classes.
- Prefer refactoring toward simpler control flow, clearer naming, and smaller method surfaces.
- Prefer abstractions only when they represent a real boundary, variation point, or test seam.
- Prefer explicit constructor dependencies over static access, service locators, or hidden framework lookups.
- Prefer small, well-factored services and components; if a class accumulates many dependencies, treat it as a signal to reassess its responsibilities.
- Prefer deduplicating business rules, validation logic, mapping logic, and UI state transitions only when the duplicated behavior is truly the same concept.
- Prefer removing dead code only after confirming it has no active references and the affected tests still pass.
- In Blazor components, prefer extracting reusable UI into a separate component only when the markup or interaction pattern is genuinely shared or the current component has more than one clear responsibility.

### MUST NOT

- MUST NOT change behavior accidentally as part of a "refactor-only" change.
- MUST NOT use manual global find-and-replace for symbol renames when a safe rename operation can update references.
- MUST NOT mix unrelated cleanup into the same refactoring change when it makes review or regression diagnosis harder.
- MUST NOT introduce speculative abstractions, base classes, or interfaces without a present code-level need such as duplication, substitution, or a test seam.
- MUST NOT force unrelated responsibilities into the same type, component, or service.
- MUST NOT remove duplication by coupling concepts that only look similar but represent different business meanings.
- MUST NOT hide dependencies behind statics, ambient context, or service locator access when an explicit dependency can be declared.
- MUST NOT weaken tests by removing assertions, narrowing coverage, or deleting regression cases without an explicit replacement.
- MUST NOT rely on code coverage percentage alone as proof that a refactor is safe.

## Output and Validation (optional)

- Validate success with:
  - `dotnet build`
  - Relevant `dotnet test` coverage for the touched projects or behaviors
- Confirm that renamed or moved types still compile cleanly and that references were updated consistently.
- If the refactor intentionally changes externally visible behavior, update the affected wiki or operator/developer documentation before considering the work complete.

## References (optional)

- `https://learn.microsoft.com/visualstudio/ide/refactoring-in-visual-studio?view=vs-2022`
- `https://learn.microsoft.com/visualstudio/ide/reference/refactoring-extract-inline?view=vs-2022`
- `https://learn.microsoft.com/visualstudio/ide/reference/refactoring-rename-move?view=vs-2022`
- `https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles`
- `https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/guidelines`
- `https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices`
- `/.github/copilot-instructions.md`
- `/.github/instructions/csharp.instructions.md`
- `/.github/instructions/tests.instructions.md`
