---
title: External Clean Architecture Guidance Research
description: Authoritative guidance for incrementally moving an ASP.NET Core and .NET Aspire solution toward Clean Architecture
ms.date: 2026-07-26
ms.topic: concept
---

## Research scope

Status: Complete

Research questions:

* What current authoritative guidance defines dependency direction and the roles of Domain, Application, adapters, Infrastructure, presentation, and composition roots?
* Where should CQRS vertical slices, DTOs and contracts, EF Core concerns, and external API clients live?
* How should an existing ASP.NET Core and .NET Aspire solution migrate incrementally while keeping API, AppHost, ServiceDefaults, and Web mostly stable?
* Which project structures are viable, what are their tradeoffs, and which structure should be the default?
* Which automated fitness tests can preserve intended boundaries during migration?

## Provisional hypothesis and discriminating check

The provisional default is to add a framework-independent Domain project, retain Application as the use-case and inner-port owner, retain Infrastructure as the outbound-adapter owner, and keep API/Web plus AppHost as presentation and composition boundaries. This hypothesis is rejected if authoritative guidance requires one project per conceptual Clean Architecture ring or places use-case-owned abstractions in Infrastructure.

## Evidence and discoveries

### Dependency direction and responsibility boundaries

Microsoft's current Clean Architecture guidance puts business logic and the
application model at the center. Compile-time dependencies point inward:
Infrastructure implements interfaces owned by the Application Core, and UI
code works through those interfaces. The guidance permits a UI-to-Infrastructure
project reference for dependency injection, but says concrete Infrastructure
types should be referenced only in the composition root (`Program.cs` or the
equivalent). This is source dependency inversion, not a prohibition on runtime
calls from an inner use case to an outer adapter.

Evidence context:

* [Common web application architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
	says dependencies flow toward the innermost circle and the Application Core
	has no dependencies on other application layers. It places entities,
	aggregates, domain services, interfaces, specifications, events, and simple
	infrastructure-neutral DTOs in that core. It places `DbContext`, migrations,
	repositories, and infrastructure-specific services in Infrastructure. The
	page was updated on 2026-07-08, although its e-book source carries an older
	publication date.
* The same page calls `Program.cs` the application's "composition root." It
	explicitly accepts a UI project reference to Infrastructure for wiring while
	limiting use of concrete Infrastructure types to that root.
* [Designing a DDD-oriented microservice](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice)
	distinguishes a domain model that owns business concepts, state, rules, and
	invariants from a thin application layer that coordinates tasks and delegates
	business decisions to domain objects. Domain entities should be POCOs without
	direct EF Core or other infrastructure-framework dependencies.
* That DDD page also warns that persistence ignorance does not mean ignoring
	physical storage constraints. Mapping and storage characteristics must still
	inform a feasible model without becoming source dependencies in Domain.

### Ports, adapters, Infrastructure, and external APIs

An inner consumer should own a narrow capability-oriented port. Infrastructure
owns the adapter and translation for persistence, email, identity, files,
clocks backed by external systems, and remote APIs. Avoid ports shaped as
`HttpClient`, `DbContext`, provider SDK requests, or transport responses because
those types move an outer mechanism into an inner contract.

Evidence context:

* Microsoft's Clean Architecture guidance identifies Application Core
	interfaces for data access, file-system access, and network calls, with
	implementations in Infrastructure.
* [HTTP requests with IHttpClientFactory](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-10.0)
	says typed clients provide one place to configure and encapsulate interaction
	with a backend. The current article is dated 2026-04-27 and was updated on
	2026-07-22. This supports a typed client as an outbound adapter, not as the
	inward port itself.
* [Use the IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)
	documents named, typed, and generated clients, handler pooling, DNS renewal,
	logging, and outgoing middleware. It warns against capturing typed clients in
	singletons and against storing request-scoped data in pooled handlers. An
	adapter should translate provider DTOs and exceptions into inward-owned data
	and outcomes before returning to Application.

### Domain and Application separation

Domain and Application are different responsibilities even when initially held
in one assembly. Domain owns durable business vocabulary, entities, value
objects, aggregates, invariants, domain services, and domain events. Application
owns use-case orchestration, commands and queries, handlers, transaction intent,
authorization decisions specific to a use case, validation at the use-case
boundary, and ports needed by those use cases.

The Microsoft sources show both a combined "Application Core" model and a
separate Domain/Application model. They therefore support project separation as
an enforcement choice, not a defining Clean Architecture rule. A separate
Domain project is most valuable when meaningful business rules can be tested
without ASP.NET Core, EF Core, remote SDKs, or hosting packages. A simple CRUD
area can remain simpler until that pressure exists.

### CQRS vertical slices and boundary contracts

Use CQRS first as behavioral separation: one task-oriented command or
side-effect-free query, one handler, and an operation-specific result. It does
not require separate stores, messaging, event sourcing, or a mediator library.
Feature folders can keep each request, response, validator, handler, and mapping
together while project references enforce inward dependency direction.

Evidence context:

* [CQRS pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
	defines commands as business tasks and queries as non-mutating operations that
	return presentation-suitable DTOs without domain logic. Its foundational
	option uses distinct read/write logic over one database. The page is dated
	2025-02-20 and was updated on 2026-06-03.
* [Applying CQRS and CQS approaches](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/eshoponcontainers-cqrs-ddd-microservice)
	uses the same database for commands and queries, applies DDD constraints to
	transactional updates, and allows simpler direct projections for reads. It
	explicitly says not to force CQRS and DDD patterns everywhere.
* Query/read DTOs belong with the use case whose consumer-facing shape they
	represent. HTTP binding models and HTTP response details belong in API; UI
	view models belong in Web; provider wire DTOs belong in Infrastructure.
	Shared public integration contracts merit a dedicated Contracts assembly only
	when an independently versioned producer/consumer boundary actually exists.

### EF Core ownership

Keep `DbContext`, provider packages, entity configurations, migrations,
repository implementations, and SQL-specific query implementations in
Infrastructure. Domain entities may be persisted by EF Core while remaining
POCOs; Fluent API configurations in Infrastructure avoid persistence attributes
in Domain. Application can own narrow persistence ports where substitution or
policy isolation is valuable, but a generic repository over every table is not
required.

Evidence context:

* [Designing the infrastructure persistence layer](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
	places repository implementations and EF Core `DbContext` in Infrastructure,
	recommends aggregate-oriented update boundaries, and states that custom
	repositories "shouldn't be mandatory." EF Core already implements repository
	and unit-of-work behavior.
* [Creating and configuring an EF Core model](https://learn.microsoft.com/en-us/ef/core/modeling/)
	describes Fluent API as the most powerful configuration mechanism and supports
	separate `IEntityTypeConfiguration<T>` classes. The page was updated on
	2026-06-01.
* [DbContext lifetime, configuration, and initialization](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
	defines a context as a short-lived, non-thread-safe unit of work. Request scope
	is a good ASP.NET Core default; Blazor and multiple-unit-of-work scenarios can
	use `AddDbContextFactory`. The page was updated on 2026-01-21.
* [EF Core migrations overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
	treats migrations and the model snapshot as source-controlled schema evolution
	artifacts. Keeping them beside the Infrastructure `DbContext` preserves
	design-time tooling ownership.

### Presentation and Aspire composition

API and Web should remain inbound adapters: bind external input, authenticate,
translate to an Application request, dispatch a use case, and translate its
result. Their `Program.cs` files are process-level composition roots and may call
Infrastructure registration extensions. AppHost is a distinct distributed
application composition root; it declares executable services, resources,
references, configuration flow, health dependencies, and startup order. It
does not absorb in-process dependency injection or business behavior.

Evidence context:

* [What is the AppHost?](https://aspire.dev/get-started/app-host/) calls AppHost
	the code-first place to declare services and relationships. `WithReference`
	injects connection or endpoint details, while `WaitFor` controls startup based
	on health. Its best practices say to keep AppHost minimal and dependencies
	explicit.
* Microsoft Learn's former AppHost overview now redirects to the Aspire page,
	making `aspire.dev` the current canonical source.
* [C# Service Defaults](https://aspire.dev/get-started/csharp-service-defaults/)
	is the current target for the former ServiceDefaults page. ServiceDefaults is
	for common service discovery, resilience, health-check, and OpenTelemetry
	setup, not domain services or feature orchestration.

### Existing solution fit

The current production graph is close to the recommended boundary shape:

* `src/TNC.Trading.Platform.Application` has no production project reference
* `src/TNC.Trading.Platform.Infrastructure` references Application
* `src/TNC.Trading.Platform.Api` references Application, Infrastructure, and
	ServiceDefaults
* `src/TNC.Trading.Platform.Web` references Application and ServiceDefaults
* `src/TNC.Trading.Platform.AppHost` references the executable API and Web
	projects

The main structural gaps visible from project metadata are the absence of a
Domain project and EF Core/Azure mechanism packages in API as well as
Infrastructure. Package presence alone does not prove misuse, but it identifies
the first dependency and source scan for migration. AppHost and ServiceDefaults
already have stable responsibilities and should require little or no structural
change.

## Project-structure alternatives

### Alternative 1: Combined Application Core

```text
TNC.Trading.Platform.Application
	Domain model, use cases, inner ports, operation contracts
TNC.Trading.Platform.Infrastructure -> Application
	EF Core, external clients, adapter translations
TNC.Trading.Platform.Api -> Application, Infrastructure, ServiceDefaults
TNC.Trading.Platform.Web -> Application, ServiceDefaults
TNC.Trading.Platform.AppHost -> Api, Web
```

This is the smallest migration. It follows Microsoft's combined Application
Core example and keeps every existing project. Use folders and namespace rules
to distinguish `Domain`, `Features`, and `Ports` inside Application.

Advantages:

* No new production project and minimal reference churn
* Easy feature-by-feature movement out of API and Infrastructure
* Appropriate while the domain model is small or boundaries remain uncertain

Costs and risks:

* The compiler cannot prevent Application use cases from depending on domain
	implementation details that should be more stable
* Domain purity requires namespace/package fitness tests and review discipline
* Application can become a broad "everything important" assembly

Best fit: a small or early-stage application whose business invariants are not
yet stable enough to justify a physical Domain boundary.

### Alternative 2: Domain, Application, and Infrastructure core

```text
TNC.Trading.Platform.Domain
	Entities, value objects, aggregates, domain rules/events
TNC.Trading.Platform.Application -> Domain
	Feature slices, commands/queries, handlers, inner ports, use-case results
TNC.Trading.Platform.Infrastructure -> Application, Domain
	EF Core, persistence adapters, external API adapters, provider DTOs
TNC.Trading.Platform.Api -> Application, Infrastructure, ServiceDefaults
TNC.Trading.Platform.Web -> Application, ServiceDefaults
TNC.Trading.Platform.AppHost -> Api, Web
```

This adds one production class library while preserving API, Infrastructure,
Web, AppHost, and ServiceDefaults. API's Infrastructure reference remains a
composition-root exception. Infrastructure normally references Application to
implement ports and may reference Domain for EF mappings; Application references
Domain. Domain references no production project.

Advantages:

* Compile-time enforcement protects framework-independent business policy
* Fits the existing graph and Microsoft's separate Domain/Application guidance
* Keeps vertical slices in Application instead of scattering one use case
	across horizontal projects
* Supports fast Domain and Application unit tests without hosts or containers

Costs and risks:

* Moving types too early can create pass-through abstractions or circular
	ownership debates
* An anemic Domain project containing only data records provides little value
* Public type moves require controlled namespace and consumer updates

Best fit: the current solution, where state transitions, schedule rules,
authentication retry behavior, and configuration invariants provide plausible
domain policy while the executable boundaries should remain stable.

### Alternative 3: Feature or bounded-context modules

```text
Modules/
	Authentication.Domain
	Authentication.Application -> Authentication.Domain
	Authentication.Infrastructure -> Authentication.Application
	Trading.Domain
	Trading.Application -> Trading.Domain
	Trading.Infrastructure -> Trading.Application
TNC.Trading.Platform.Api -> module Application/Infrastructure assemblies
TNC.Trading.Platform.Web -> module Application assemblies
TNC.Trading.Platform.AppHost -> Api, Web
```

This treats each cohesive business capability as a module with its own inward
dependency rule. The stable hosts compose modules, and ServiceDefaults remains
cross-cutting host setup. A lower-project-count variant uses one assembly per
module with `Domain`, `Application`, and `Infrastructure` namespaces enforced by
architecture tests.

Advantages:

* Strong business-capability cohesion and independent evolution
* Reduces the chance that one global Domain or Application project becomes a
	shared-model coupling point
* Creates future extraction seams without requiring microservices now

Costs and risks:

* Highest project, registration, test, and ownership overhead
* Premature module boundaries can cause duplicate abstractions and chatty
	cross-module calls
* Shared database and transaction boundaries need explicit ownership
* Unsuitable as a big-bang reorganization while capability boundaries are still
	being learned

Best fit: a later stage, after repeated changes demonstrate stable bounded
contexts and teams need independent ownership or deployment options.

### Comparison

| Criterion                     | Combined core | Separate Domain | Feature modules |
|-------------------------------|---------------|-----------------|-----------------|
| Change to stable hosts        | Very low      | Low             | Low to medium   |
| Compile-time domain isolation | Low           | High            | High            |
| Initial migration cost        | Lowest        | Moderate        | Highest         |
| Vertical-slice cohesion       | High          | High            | High            |
| Boundary flexibility          | High          | Medium          | Lower initially |
| Long-term module autonomy     | Low           | Medium          | High            |
| Recommended now               | Viable        | Default         | Defer           |

## Recommended default

Use Alternative 2: add `TNC.Trading.Platform.Domain`, retain Application as the
CQRS-style use-case boundary, and retain Infrastructure as the outbound-adapter
boundary. Do not add separate Contracts, Persistence, Integrations, or Shared
Kernel projects until a concrete independent consumer, deployment boundary, or
ownership pressure appears.

Recommended source dependency graph:

```text
Domain <- Application <- Infrastructure
						 ^                ^
						 |                |
					API/Web ---- composition only

AppHost -> API/Web executable resources
API/Web -> ServiceDefaults host defaults
```

The diagram is conceptual. API references Application for use cases and may
reference Infrastructure to call registration extensions. Infrastructure can
reference both Application and Domain. Web should use Application-facing
requests/results or API contracts and should not gain a persistence dependency.
AppHost composes processes and resources; API/Web `Program.cs` composes each
process's services.

Responsibility placement:

| Concern                         | Default owner                                               |
|---------------------------------|-------------------------------------------------------------|
| Business invariants             | Domain                                                      |
| Use-case orchestration          | Application feature slice                                   |
| Commands and queries            | Application feature slice                                   |
| Use-case result records         | Application feature slice                                   |
| Inward ports                    | Application, or Domain when domain policy consumes one       |
| HTTP request/response models    | API adapter                                                  |
| Blazor view models              | Web adapter                                                  |
| External provider wire models   | Infrastructure adapter                                       |
| Public integration contracts    | Contracts project only when independently shared             |
| EF Core context/configurations  | Infrastructure                                               |
| EF Core migrations              | Infrastructure beside the owning context                     |
| Remote API typed clients        | Infrastructure                                               |
| DI registrations                | Infrastructure extensions called by API composition          |
| Process composition             | API/Web `Program.cs`                                         |
| Distributed composition         | AppHost                                                      |
| Telemetry/resilience defaults   | ServiceDefaults                                              |

This recommendation keeps the repository's existing feature folders and
request/response/handler shape. Clean Architecture controls dependency direction;
vertical slices control change locality. They are complementary.

## Migration sequence and fitness tests

### Incremental sequence

1. Capture behavior before moving code. Keep existing API, Web, integration,
	 and use-case tests green; add characterization tests only for a selected
	 feature whose behavior is not already protected.
2. Declare the intended project roles and extend the existing architecture test
	 suite before moving many types. Preserve its current acyclic and resolvable
	 graph checks.
3. Add the Domain project with no framework or provider packages. Move one
	 coherent policy cluster, such as schedule or state-transition rules, together
	 with its value objects and tests. Do not move request/response records merely
	 because they contain business-looking fields.
4. Keep each operation in Application as a vertical slice. Let the handler
	 coordinate Domain behavior and inner ports. Keep simple queries simple and
	 project directly to operation results through an Application-owned query port
	 when a rich aggregate adds no value.
5. Normalize one outbound boundary at a time. Rename ports by business purpose,
	 place provider request/response DTOs and serialization in Infrastructure, and
	 map to inward-owned records. The current Application `Infrastructure/Ig`
	 folder is a strong candidate because it contains IG-shaped contracts even
	 though Application has no HTTP or Azure package dependency.
6. Move API database initialization behind an Infrastructure registration or
	 initialization boundary. The current API `Program.cs` directly resolves
	 `PlatformDbContext` and calls `EnsureCreatedAsync`; that keeps an EF mechanism
	 in presentation startup. Preserve startup behavior while removing EF Core and
	 provider package references from API when no API source needs them.
7. Keep `PlatformDbContext`, Fluent API mappings, migrations, EF repositories,
	 and SQL projections in Infrastructure. Introduce a repository only where an
	 aggregate or testable use-case boundary benefits; do not wrap every `DbSet`
	 mechanically.
8. Repeat by feature. Route existing endpoints and pages through the migrated
	 handler while unmigrated code coexists. Treat temporary mappers as an
	 in-process anti-corruption layer and remove them after all callers move.
9. Tighten fitness rules only after a role becomes stable. Remove obsolete
	 packages and references after the corresponding source scan and tests pass.
10. Reassess feature modules after change history reveals bounded contexts. Do
		not split services, databases, or deployments merely to complete a diagram.

This is an in-process application of the same risk-control principle documented
by Microsoft's [Strangler Fig pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig): replace functionality incrementally, keep
old and new paths working during transition, validate, and deliberately remove
the old path. The article is dated 2026-05-29 and updated on 2026-06-03. No
network proxy or microservice split is required for the principle to be useful.

Use an adapter when old and new semantics differ. Microsoft's
[Anti-Corruption Layer pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer), dated 2026-05-28, explicitly says the pattern can
apply to legacy integration inside a monolith. Keep that adapter focused on
translation, validation, and sanitization rather than business orchestration.

### Fitness tests

Add tests in the existing
`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests`
project. Prefer explicit checks over introducing a package until reflection or
namespace rules become cumbersome.

* Project graph: Domain references no production project; Application references
	Domain but not Infrastructure, API, Web, AppHost, or ServiceDefaults;
	Infrastructure does not reference presentation or AppHost; Web does not
	reference Infrastructure; only AppHost references executable projects.
* Package boundaries: Domain must not reference ASP.NET Core, EF Core, Azure,
	Aspire, HTTP-client, UI, or provider SDK packages. Application must not
	reference EF Core, Azure provider SDKs, Aspire hosting, or UI packages.
* Source/type boundaries: Domain and Application types must not mention
	`DbContext`, `DbSet`, `HttpContext`, `HttpClient`, `IResult`, Razor/component
	types, provider SDK types, or Infrastructure namespaces.
* Composition exception: API may reference Infrastructure, but usages should be
	constrained to `Program.cs` and dedicated registration/startup code. A source
	scan is more discriminating than a project-reference rule for this exception.
* Contract isolation: API DTOs must not be EF entities; provider DTOs must not
	cross Infrastructure; Domain entities must not be returned directly from
	endpoints or bound directly to UI forms.
* Feature shape: each operation keeps one request, response, and handler, with a
	validator when required. Queries remain side-effect free.
* Behavioral independence: Domain and Application unit tests run without
	starting Aspire, a web host, SQL Server, Keycloak, or an external API.
* Adapter integration: run focused tests against real EF provider behavior and
	stub HTTP handlers or provider sandboxes. Keep fewer closed-box AppHost tests
	for wiring, service discovery, health, and end-to-end behavior.
* Migration safety: add a temporary test that compares old and new outputs for a
	migrated query or policy where dual execution is deterministic; delete it with
	the retired path.

Fitness tests should encode stable dependency decisions, not freeze project
counts, filenames, or historical inventories. The existing graph validator's
choice to avoid fixed project names and layer roles is correct for the current
state; add role rules when the Domain boundary is introduced and agreed.

## Pitfalls

* Treating a canonical four-circle diagram as a required four-project template.
	Robert C. Martin says the circles are schematic and may vary; the Dependency
	Rule is the invariant.
* Creating interfaces for every class. Add a port at an actual external boundary
	or substitution point, and define it for the inner consumer's purpose.
* Putting provider-shaped interfaces in Application. An interface decorated for
	Refit or returning an IG/Azure wire response is still an outer dependency even
	if its C# declaration sits inward.
* Sharing one DTO everywhere. HTTP DTOs, use-case requests/results, UI view
	models, persistence entities, and provider wire models change for different
	reasons and should be translated at their adapters.
* Returning Domain entities from queries or APIs. Microsoft warns against
	propagating domain entities into presentation; read projections should expose
	purpose-built DTOs.
* Forcing all reads through aggregate repositories. Simplified CQRS permits
	direct read projections; aggregates and repositories primarily protect
	transactional invariants on writes.
* Wrapping EF Core in generic repositories without a use-case benefit. Microsoft
	notes that custom repositories are optional and `DbContext` already supplies
	repository/unit-of-work behavior.
* Ignoring persistence constraints in pursuit of "pure" Domain. Persistence
	ignorance avoids source coupling, not physical reality or mapping design.
* Sharing `DbContext` across threads or a Blazor circuit. It is not thread-safe;
	use one short unit of work and `IDbContextFactory` where DI scope does not match
	that lifetime.
* Putting application models or shared business helpers in ServiceDefaults.
	Aspire explicitly cautions that this project is for its host-default extension
	functionality, not general shared code.
* Moving in-process DI composition into AppHost. AppHost composes distributed
	resources; each executable still owns its service container and middleware.
* Equating Clean Architecture with microservices. Microsoft notes that logical
	layers can deploy as one unit and that unnecessary service decomposition adds
	communication and operational complexity.
* Applying full CQRS, separate stores, messaging, or event sourcing to simple
	CRUD. Current Azure guidance lists complexity and eventual consistency costs
	and says CQRS may be unsuitable for simple domains.
* Allowing transition adapters to become permanent orchestration layers. Track
	their retirement condition and keep business decisions in Domain/Application.
* Capturing typed HTTP clients in singletons or request-scoped state in pooled
	handlers. Current .NET guidance warns that both defeat lifetime and isolation
	assumptions.

## References

Primary current guidance:

* [Common web application architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
* [Designing a DDD-oriented microservice](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice)
* [Designing the infrastructure persistence layer](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
* [Applying CQRS and CQS approaches](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/eshoponcontainers-cqrs-ddd-microservice)
* [CQRS pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
* [DbContext lifetime, configuration, and initialization](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
* [Creating and configuring an EF Core model](https://learn.microsoft.com/en-us/ef/core/modeling/)
* [EF Core migrations overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
* [HTTP requests with IHttpClientFactory](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-10.0)
* [Use the IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)
* [What is the AppHost?](https://aspire.dev/get-started/app-host/)
* [C# Service Defaults](https://aspire.dev/get-started/csharp-service-defaults/)
* [Strangler Fig pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig)
* [Anti-Corruption Layer pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer)

Original principle sources:

* Robert C. Martin, [The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html), 2012. The article states that source dependencies point inward, adapters convert external and internal forms, boundary data is simple and inward-convenient, and the number of circles is not prescribed.
* Alistair Cockburn, [Hexagonal Architecture](https://alistair.cockburn.us/hexagonal-architecture/), original 2005 article. It defines purposeful ports and technology-specific adapters so application behavior can run independently of UI, databases, and other external devices.
* Jeffrey Palermo, [The Onion Architecture, part 1](https://jeffreypalermo.com/2008/07/the-onion-architecture-part-1/), 2008. It places the Domain Model at the center and externalizes data access implementations while retaining core-owned contracts.

Source-quality note: several Microsoft .NET architecture pages are maintained
e-book chapters whose original publication metadata is older than their page
update date. They remain official and consistent with the newer 2025-2026 Azure,
ASP.NET Core, EF Core, and Aspire guidance, but current product-specific pages
should govern API and runtime details. Microsoft Learn now redirects its Aspire
fundamentals pages to `aspire.dev`, which is the canonical Aspire source used
here.

## Unresolved gaps and clarifying questions

No research blocker remains for the requested architectural recommendation.
Implementation-specific decisions still require codebase analysis outside this
research-only task:

* Which current Application records contain durable domain behavior versus
	operation projections or persistence snapshots?
* Whether API's direct database initialization is intentional only for local
	development, and whether migrations will replace `EnsureCreatedAsync`
* Whether Web invokes Application in process for deliberate reasons or should
	consume API contracts exclusively at its deployment boundary
* Whether IG provider DTOs are observable public contracts or can move behind an
	Infrastructure mapping boundary without compatibility shims
* Which capability boundaries, if any, are stable enough to justify the feature
	module alternative later

## Recommended next research

* [ ] Inventory Application types by responsibility: Domain policy, use case,
	inner port, query projection, provider contract, or persistence snapshot
* [ ] Trace one write slice and one query slice end to end through API,
	Application, Infrastructure, and Web to estimate translation churn
* [ ] Review database lifecycle and deployment requirements before choosing
	`EnsureCreated`, migrations, bundles, or an external migration job
* [ ] Review Web's runtime topology and contract ownership before deciding
	whether Application results are valid Web dependencies
* [ ] Prototype the proposed role-aware project graph and forbidden-dependency
	fitness tests in a separate implementation task
* [ ] Identify the first Domain extraction candidate by business-rule density,
	test coverage, and low adapter coupling
