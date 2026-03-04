---
description: 'Enforces Vertical Slice Architecture with CQRS-style request/response contracts so features stay cohesive, controllers remain thin, and behavior is easy to test and evolve.'
applyTo: 'src/**/*.cs, test/**/*.cs'
---

# Vertical Slice + CQRS-Style Request/Response Guidelines

## Overview

These guidelines define the required structure for implementing features as vertical slices in this repository.
Each feature is implemented end-to-end around a single operation using explicit request/response contracts, optional validation, and a single handler.

## Scope

Applies to: `src/**/*.cs, test/**/*.cs`

- Applies to feature implementation code under `Features/`.
- Applies to HTTP endpoint/controller code that dispatches feature requests.
- Applies to unit tests for feature handlers and validators.

## Instructions

### MUST

- Organize product code by feature under a `Features/<FeatureName>/` folder (vertical slice), not by technical type (no primary `Controllers/`, `Services/`, `Repositories/` organization).

- For every feature/operation, create the following files in the feature folder:
  - `<FeatureName>Request.cs`
  - `<FeatureName>Response.cs`
  - `<FeatureName>Handler.cs`
  - `<FeatureName>Validator.cs` when validation is required

- Create exactly one request type and one response type per operation.
- Ensure the handler processes exactly one request type and returns exactly one response type.

- Keep controllers/endpoints thin:
  - Bind input to the `*Request`
  - Call the `*Handler`
  - Translate handler results to HTTP responses

- Keep request/response DTOs transport-only:
  - Map DTOs to internal/domain/persistence models inside the handler (or dedicated mapping helpers in the same feature folder)
  - Use DTOs to decouple domain/persistence models from transport models

- Put business logic in the handler and/or domain model/services.

- Add validation when any external input can be malformed or violate invariants (HTTP input, message bus, UI form input), especially for strings, identifiers, amounts, dates, enums, and collections.

- Unit test handlers and validators directly; do not rely on controller tests as the primary test mechanism.

### SHOULD

- Use `<Verb><Noun>` feature names with imperative verbs:
  - Commands (state change): `Create`, `Update`, `Cancel`, `Delete`, `Submit`, `Approve`
  - Queries (read): `Get`, `List`, `Search`, `Export`

- Name types consistently:
  - `<Verb><Noun>Request`
  - `<Verb><Noun>Response`
  - `<Verb><Noun>Handler`
  - `<Verb><Noun>Validator`

- Keep feature-only helper files inside the feature folder (e.g., `<FeatureName>Mapping.cs`, `<FeatureName>Errors.cs`).
- Prefer immutable DTOs (`record`/`record class`) when practical.
- Keep handlers focused and reviewable; if a handler grows large, extract domain services or feature-local helpers.

### MUST NOT

- MUST NOT put business rules, persistence code, or integration calls in controllers/endpoints.
- MUST NOT expose EF Core entities (or database models) as request/response DTOs.
- MUST NOT add business behavior, service dependencies, or persistence concerns to DTOs.
- MUST NOT implement multiple operations in a single handler via flags/branching.
- MUST NOT use validators to implement business decisions (validators validate input; they do not perform orchestration or state transitions).
- MUST NOT introduce "god services" or over-generalized shared abstractions that hide real behavior (for example, `BaseHandler`, `GenericService`, `CommonRequest`).

## Output and Validation (optional)

- Expected artifacts for a new feature:
  - `Features/<FeatureName>/<FeatureName>Request.cs`
  - `Features/<FeatureName>/<FeatureName>Response.cs`
  - `Features/<FeatureName>/<FeatureName>Handler.cs`
  - `Features/<FeatureName>/<FeatureName>Validator.cs` (when applicable)
  - Unit tests covering the handler (and validator when present)

- Validate success:
  - `dotnet build`
  - `dotnet test`

## References (optional)

## Examples (optional)

- Good example (folder layout):

  ```
  Features/
    CreateOrder/
      CreateOrderRequest.cs
      CreateOrderResponse.cs
      CreateOrderValidator.cs
      CreateOrderHandler.cs
  ```

- Good example (DTO templates):

  ```csharp
  public sealed record class CreateOrderRequest(string Symbol, decimal Quantity);
  public sealed record class CreateOrderResponse(Guid OrderId, string Status);
  ```

- Good example (thin endpoint/controller behavior):
  - Bind request
  - Validate (if applicable)
  - Dispatch to handler
  - Return response

- Bad example:
  - A controller that queries the database, calls multiple services, performs business rules, and builds response DTOs inline.

## Notes (optional)

- The handler/validator mechanism may be implemented with MediatR or an internal dispatch pattern; the required shape and responsibilities are the same.
