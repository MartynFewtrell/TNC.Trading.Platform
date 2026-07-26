---
description: 'Guides cohesive vertical slices, CQRS-style operation contracts, and portable Clean Architecture responsibility and dependency direction.'
applyTo: 'src/**/*.cs, src/**/*.csproj, test/**/*.cs, test/**/*.csproj'
---

# Vertical Slice, CQRS-Style, and Clean Architecture Guidelines

## Overview

These guidelines combine three complementary concerns:

- Vertical Slice Architecture keeps one operation's behavior cohesive.
- CQRS-style contracts keep command and query inputs and outputs explicit.
- Clean Architecture keeps policy independent from external details through
  responsibility ownership and inward source dependencies.

Responsibilities may be separated by files, folders, assemblies, services, or
another justified boundary. They do not need canonical names, a fixed number of
projects, or one project per responsibility.

## Scope

Applies to: `src/**/*.cs, src/**/*.csproj, test/**/*.cs, test/**/*.csproj`

- Applies to feature implementation code and its project boundaries.
- Applies to inbound boundary code that dispatches feature requests.
- Applies to unit tests for feature handlers, validators, and policy.

## Clean Architecture Responsibilities

Use these as conceptual roles, not mandatory names or project boundaries:

- **Policy** contains enterprise or domain rules where present. It should be
  stable, framework-independent, and usable without external mechanisms.
- **Use cases** contain application-specific policy and coordinate one user or
  system goal. They may call policy and inner-owned ports.
- **Inbound adapters** receive HTTP, messaging, command-line, UI, or other
  external input, translate it into simple inward-owned data, invoke a use case,
  and translate the result outward.
- **Outbound adapters** implement ports owned by the inner consumer and
  translate between inward data and persistence, identity, notification,
  provider, or external-service representations.
- **Frameworks and drivers** contain external mechanisms and their details.
- **Composition roots** assemble the object graph and select concrete adapters
  for registration, configuration, startup, and hosting.

The Dependency Rule is about source dependencies: source references point
inward toward more general and stable policy. Runtime calls may travel outward
through dependency inversion, but that runtime direction must not reverse the
source dependency. A source dependency is not justified by a runtime call path
alone.

## Instructions

### MUST

#### Clean Architecture boundaries

- Keep policy and use-case code independent of transport, UI, persistence,
  framework, provider, and hosting types.
- Make boundary data simple and owned inward: use records, basic structures,
  arguments, or maps suited to the inner consumer rather than external request,
  response, database, UI, or SDK types.
- Translate external representations at adapters. Do not pass framework
  contexts, persistence entities, provider responses, or UI component models
  inward as policy or use-case boundary types.
- When an inward use case needs an outward runtime service, define the narrow
  port at the consuming inner boundary. Let the outward adapter implement that
  port; do not make the use case reference the concrete adapter.
- Keep concrete adapter references in composition roots limited to registration,
  configuration, adapter selection, startup orchestration, and hosting. This
  exception does not permit handlers, endpoints, or workflows to own business
  decisions or call concrete external details directly.
- Keep business decisions in policy or use-case code, not in inbound adapters,
  outbound adapters, frameworks, or composition roots.
- Test policy and use-case behavior independently of web hosts, databases,
  containers, UI, brokers, and external services whenever the behavior permits.
- Preserve valid existing boundaries during incremental work. When a
  responsibility is misplaced, move or translate one boundary at a time and
  avoid speculative projects, pass-through abstractions, or marker types.

- Organize product code by feature under a `Features/<FeatureName>/` folder
  (vertical slice), not by technical type as the primary organization.

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
  - Map DTOs to inward-owned policy or use-case data inside the handler (or
    dedicated mapping helpers in the same feature folder)
  - Use DTOs to decouple policy and external models from transport models

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

- Classify a responsibility and its boundary before adding a dependency. Ask
  whether the dependency is policy, a use case, an inbound adapter, an outbound
  adapter, a framework or driver, or composition. If stable assembly boundaries
  have not yet been declared, apply these relationships locally and document
  the incremental movement rather than inventing a topology.

### MUST NOT

- MUST NOT reverse the source dependency direction merely because runtime calls
  move outward.
- MUST NOT make policy or use cases depend on external representations,
  framework details, concrete adapters, or hosting concerns.
- MUST NOT require every class to have an interface or every adapter to have a
  new abstraction when no boundary or substitution need exists.
- MUST NOT equate Clean Architecture with a particular project layout, number
  of layers, framework, deployment style, or integration pattern.
- MUST NOT add speculative projects, pass-through services, marker types, or
  broad abstractions solely to make a diagram appear complete.

- MUST NOT put business rules, persistence code, or integration calls in controllers/endpoints.
- MUST NOT expose persistence entities or database models as request/response DTOs.
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
- Validate project graph integrity separately from architectural source rules:
  references should resolve and production project references should be acyclic.
  Repository-specific boundary checks may enforce declared boundaries when the
  repository has stable roles, but a project graph alone cannot prove boundary
  translation, policy placement, inner-owned ports, or composition-only concrete
  adapter use.
- Review source dependencies and boundary types directly, and keep behavior
  tests focused on policy and use cases rather than mirroring volatile type
  structure.

## References (optional)

- Robert C. Martin, [The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- Robert C. Martin, [A Little Architecture](https://blog.cleancoder.com/uncle-bob/2016/01/04/ALittleArchitecture.html)
- InformIT, [The Clean Architecture Dependency Rule](https://www.informit.com/articles/article.aspx?p=2832399)

## Examples (optional)

- Source/runtime dependency inversion:
  - `use case -> inner-owned port <- database adapter`
  - Runtime flow may be `inbound adapter -> use case -> database adapter`,
    while source dependencies remain inward.
- Boundary translation:
  - `HTTP adapter -> request DTO -> use case`
  - The use case receives simple inward-owned data, not an HTTP context or
    framework result.
- Composition:
  - `composition root -> concrete adapter registration`
  - Concrete adapter selection belongs in composition and startup code, not in
    a request handler or policy rule.

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

- The handler/validator mechanism may use an internal dispatch pattern or a
  framework-neutral equivalent; the required shape and responsibilities remain
  the same.
