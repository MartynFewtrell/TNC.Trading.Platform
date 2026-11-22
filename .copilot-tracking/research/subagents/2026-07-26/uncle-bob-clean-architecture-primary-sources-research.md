<!-- markdownlint-disable-file -->
# Robert C. Martin's Clean Architecture: Primary-Source Research

## Research status

Complete for the requested conceptual questions. Research started and completed
2026-07-26. Publicly accessible talks/transcripts remain a documented evidence
gap; no claims depend on them.

## Scope and questions

This research investigates Clean Architecture specifically as Robert C. Martin presents it. It prioritizes Martin's own published material, including the 2012 Clean Architecture article, relevant Clean Coder posts about architecture, components, and boundaries, accessible official talks or transcripts, and publicly verifiable descriptions of the 2017 book.

Questions:

* What are the essential principles and the Dependency Rule?
* What roles do entities, use cases, interface adapters, and frameworks and drivers play?
* What may cross architectural boundaries, and in what form?
* How does dependency inversion enforce source-code dependency direction?
* What is the role of composition and the Main component?
* How should boundary crossings and data structures be designed?
* How does Clean Architecture affect testing?
* What distinguishes policy from detail?
* Which component principles matter to Clean Architecture?
* How does runtime control flow differ from source-code dependency direction?
* Which practices are optional conventions rather than actual Clean Architecture requirements?
* What common interpretations are unsupported or misleading?
* What implications can be translated into enforceable GitHub Copilot instructions for a .NET repository?

## Source method

Primary sources are preferred. Secondary sources are included only to corroborate bibliographic facts, published-book descriptions, or points unavailable in accessible primary material. Direct quotations are short and attributed; all other source content is paraphrased. Statements marked "Interpretation" synthesize implications rather than report Martin's explicit wording.

## Findings

### Essential principles

Martin presents Clean Architecture as a synthesis of Hexagonal Architecture,
Onion Architecture, Screaming Architecture, DCI, and BCE. Their shared objective
is separation of concerns through boundaries between business rules and
interfaces. The intended properties are independence from frameworks, UI,
database, and external agencies, plus the ability to test business rules without
those external elements.

The architecture is organized by level of policy. Mechanisms and volatile
details belong farther out; general and stable policy belongs farther in. The
four-circle drawing is not a mandated project template. Martin explicitly calls
the circles "schematic" and permits more or fewer boundaries. The invariant is
the Dependency Rule.

### Dependency Rule and dependency inversion

The Dependency Rule governs source-code dependencies: they point inward toward
higher-level policy. Inner code must not name functions, classes, variables,
framework-generated formats, or other software entities declared in an outer
circle. This is stricter and more precise than saying that requests execute from
outside to inside.

Runtime control can cross a boundary in either direction. In Martin's example,
a controller invokes a use case and the use case invokes a presenter. The
presenter call points outward at runtime, but its source dependency still points
inward because the use-case layer owns an output-port interface and the outer
presenter implements it. Martin uses dynamic polymorphism and the Dependency
Inversion Principle to make source dependency oppose control flow where needed.

"A Little Architecture" reinforces the distinction: high-level business policy
can call a low-level database implementation at runtime while the database
implementation depends at compile time on a gateway interface owned by the
business rules. Martin also applies interface segregation: each business rule
defines only the data-access capability it needs, rather than inheriting a broad
database API.

### The four named areas

* Entities encapsulate the most general, high-level business rules. They may be
	objects with methods or data structures plus functions. Enterprise reuse is
	possible but not required; in a single application they are its most general
	business objects. External operational changes should not affect them.
* Use cases contain application-specific business rules. They orchestrate data
	to and from entities and direct entities toward the application's goals.
	Changes to a use case affect this layer, while UI, database, and framework
	changes should not.
* Interface adapters convert between forms convenient to use cases/entities and
	forms convenient to external agencies. Martin places MVC controllers,
	presenters, and views here, along with persistence and external-service
	adapters. SQL belongs outside the business-policy circles.
* Frameworks and drivers contain web frameworks, databases, tools, and glue.
	Martin treats these as details and expects relatively little authored logic in
	the outermost area.

### Boundary crossings and data

Boundary data should use isolated, simple structures in the form most convenient
to the inner circle. Martin allows basic structs, DTOs, function arguments,
maps, or objects. He rejects passing database row objects inward because that
imports an outer framework's format. His 2012 wording also says not to "pass
Entities" across the illustrated boundary. Interpretation: this is a warning
against exposing inner domain objects as generic transport records across an
architectural boundary, not a claim that entities can never be supplied to an
inner use case by another inner policy component. The operative test is whether
the crossed data introduces a forbidden dependency or couples policy to an
outer representation.

### Testing

The architecture makes business rules testable without UI, database, web server,
or another external element. Isolation from slow I/O also enables fast,
lightweight test environments. Martin's later testing posts support testing
through stable behavior-facing APIs rather than mirroring every production
class. "Test Contra-variance" explicitly says there is no rule requiring one
test class per production class; structurally mirrored tests impede refactoring.
The public Chapter 28 preview is stronger than treating tests as external
verification: it says tests are part of the system and participate in the
architecture. Their dependencies and APIs therefore need architectural design,
including protection from unnecessary coupling to volatile production details.

### Screaming Architecture and organization

"Screaming Architecture" says a repository's top-level organization should tell
readers what the application does, not merely which delivery framework it uses.
Use cases should be visibly central; the web, database, and frameworks are tools
and delivery mechanisms. Martin's practical test is whether the use cases can be
tested without the framework. "Does Organization Matter?" adds that organization
must evolve with system scale, so this is a direction for making policy visible,
not a universal folder taxonomy.

### Details and policy

Database, web server, UI, and frameworks are implementation details, not the
system's highest-level architectural decisions. "A Little Architecture" argues
that important architectural decisions preserve the ability to defer those
details until more information exists. Calling a tool does not require business
policy to source-depend on that tool.

Martin's public book preview distinguishes the application data model from the
database product: application data structure can be architecturally significant,
while the database software remains a utility detail. "Framework Bound" likewise
does not reject frameworks. It says to keep them behind architectural boundaries
and out of high-level policy.

### Composition and the Main component

The public Chapter 26 preview calls `Main` the component that creates,
coordinates, and oversees the others. It is the system's initial entry point and
"the ultimate detail," with no application component depending on it. It creates
factories, strategies, and global facilities, performs dependency injection, and
then transfers control to high-level abstractions.

Interpretation for .NET: `Program.cs`, the composition root, and host-specific
registration are outermost details. They may reference application abstractions
and concrete adapters to assemble the object graph. Domain and use-case projects
must not reference the host merely because the host initiates runtime execution.
A DI container is optional machinery at the edge, not the mechanism that defines
Clean Architecture.

### Component principles

Martin's book groups six principles into component cohesion and component
coupling. They matter when circles or feature boundaries are realized as
assemblies, packages, libraries, or independently released units. They do not
replace the Dependency Rule.

The public Chapter 13 preview names three cohesion principles:

* REP, the Reuse/Release Equivalence Principle: the unit of reuse should be a
	released and versioned unit. A reusable component needs a coherent release
	process and compatibility story.
* CCP, the Common Closure Principle: group classes that change for the same
	reasons and at the same times; separate classes that change for different
	reasons. At component scale this is the Single Responsibility Principle's
	change-axis concern.
* CRP, the Common Reuse Principle: do not force consumers to depend on classes
	they do not use. Classes reused together belong together; otherwise split the
	component. At component scale this parallels interface segregation.

The public Chapter 14 preview names component coupling and directly exposes ADP:

* ADP, the Acyclic Dependencies Principle: "Allow no cycles in the component
	dependency graph."
* SDP, the Stable Dependencies Principle: dependencies should point toward more
	stable components, where stability means resistance to being forced to change
	by dependents rather than immobility.
* SAP, the Stable Abstractions Principle: a component's abstractness should be
	proportional to its stability. Highly stable components need abstraction so
	they can be extended without destabilizing dependents; volatile components can
	remain concrete.

Source note: REP's short definition and ADP's sentence are directly visible in
the public previews. The CCP, CRP, SDP, and SAP summaries above are labeled
interpretive paraphrases of Martin's established definitions because the
corresponding full chapter sections were not publicly exposed in this session.
They should be checked against a lawfully held copy before exact wording is used
in normative repository instructions.

### Runtime flow versus source dependency

Runtime flow and source dependency are different graphs. A typical request can
execute controller to use case to presenter and can invoke a persistence adapter.
The source graph can still point inward at every boundary when inner policy owns
the ports that outer adapters implement. Runtime ordering therefore cannot be
used to infer allowed project references.

### Required rules versus optional conventions

Martin explicitly requires the Dependency Rule for the architecture he depicts.
The following are consequences or useful design goals supported by his sources:
test business rules without external machinery; isolate frameworks and I/O;
translate outer formats at boundaries; keep policy visible; defer detail choices.

The following are not universal Clean Architecture requirements:

* Exactly four circles or exactly four .NET projects. Martin calls the circles
	schematic and permits additional boundaries.
* Folder or project names literally called Entities, UseCases,
	InterfaceAdapters, and FrameworksAndDrivers.
* One class per use case, CQRS, MediatR, repositories, DDD aggregates, or a
	specific request/handler/result convention. These may be compatible local
	choices but are not stated by the primary sources as requirements.
* Object-oriented entities. Martin allows objects with methods or data
	structures plus functions.
* A database-free product. The database may be used heavily at runtime while
	remaining an outer implementation detail.
* Microservices, separate processes, or independent network deployment. Martin
	says Clean Architecture is indifferent among DLLs/JARs, threads, processes,
	and services when components remain ignorant of deployment mechanics.
* A DI framework. Dependency inversion is a source-dependency design property;
	container-based injection is one composition technique at `Main`.
* Every boundary crossing must use an interface. An interface or dynamic
	polymorphism is needed where control must flow against the allowed source
	dependency; a same-direction call does not gain value from an invented port.
* One unit-test class per production class. Martin explicitly rejects this as a
	rule and warns that mirrored test structure creates fragility.

### Publicly verifiable book description

Pearson and InformIT identify *Clean Architecture: A Craftsman's Guide to
Software Structure and Design* by Robert C. Martin as a first-edition,
432-page Pearson/Addison-Wesley title. InformIT gives publication date
2017-09-10, print ISBN 978-0-13-449416-6, and copyright 2018; Pearson gives
2017-09-12 for its listed edition/update. O'Reilly lists September 2017 and
ISBN 9780134494272 for its digital edition. The publisher description says the
book addresses high-level application structures, core architecture/design
principles, the architect's role, failure modes, and professional practices.

Public previews verified chapters on Component Cohesion, Component Coupling,
The Main Component, The Test Boundary, The Database Is a Detail, The Web Is a
Detail, and Frameworks Are Details. InformIT publishes a substantial Chapter 22
excerpt, "The Clean Architecture Dependency Rule," dated 2017-11-18. Its core
language closely matches Martin's 2012 article and confirms that the Dependency
Rule, four areas, schematic circles, boundary inversion, and simple boundary
data remained in the published book.

## Source register

### Evidence-strength guide

* Direct primary evidence: Martin's Clean Coder articles and text visible in the
	public publisher-authorized chapter previews or excerpt.
* Publisher metadata: Pearson, InformIT, and O'Reilly catalog facts and chapter
	titles; authoritative for publication details, not substitutes for unavailable
	chapter text.
* Interpretation: .NET mappings, enforceable Copilot rules, and explicitly
	labeled synthesis. These are reasoned consequences, not quotations from Martin.
* Unverified: talk content and exact wording hidden behind preview or sign-in
	barriers. No conclusion in this document depends on it.

### Primary sources reviewed

* Robert C. Martin, [The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html), 2012-08-13. Primary source for the synthesis, architectural properties, Dependency Rule, four areas, schematic circles, boundary crossing, boundary data, and testability.
* Robert C. Martin, [A Little Architecture](https://blog.cleancoder.com/uncle-bob/2016/01/04/ALittleArchitecture.html), 2016-01-04. Primary source for policy versus detail, runtime versus compile-time dependency, caller-owned interfaces, interface segregation, database gateways, testing, and deferring detail decisions.
* Robert C. Martin, [Test Contra-variance](https://blog.cleancoder.com/uncle-bob/2017/10/03/TestContravariance.html), 2017-10-03. Primary source for independently designed tests, behavioral APIs, coupling, and the rejection of one-test-class-per-production-class as a rule.
* Robert C. Martin, [Symmetry Breaking](https://blog.cleancoder.com/uncle-bob/2017/03/07/SymmetryBreaking.html), 2017-03-07. Supporting primary source for tests depending on production code, test granularity, decoupling, and production components separated by abstraction layers.
* Robert C. Martin, [Screaming Architecture](https://blog.cleancoder.com/uncle-bob/2011/09/30/Screaming-Architecture.html), 2011-09-30. Primary source for use-case-visible organization, frameworks as tools, delivery as detail, deferred decisions, and framework-free use-case tests.
* Robert C. Martin, [NO DB](https://blog.cleancoder.com/uncle-bob/2012/05/15/NODB.html), 2012-05-15. Primary source for use cases at the center and databases/frameworks as deferrable details.
* Robert C. Martin, [Framework Bound](https://blog.cleancoder.com/uncle-bob/2014/05/11/FrameworkBound.html), 2014-05-11. Primary source for framework coupling risks and keeping frameworks behind boundaries away from high-level policy.
* Robert C. Martin, [The Open Closed Principle](https://blog.cleancoder.com/uncle-bob/2014/05/12/TheOpenClosedPrinciple.html), 2014-05-12. Supporting primary source for plugin direction: plugins depend on the system, while the core does not know plugins.
* Robert C. Martin, [Microservices and Jars](https://blog.cleancoder.com/uncle-bob/2014/09/19/MicroServicesAndJars.html), 2014-09-19. Primary source distinguishing independently deployable components from mandatory service boundaries.
* Robert C. Martin, [Clean Micro-service Architecture](https://blog.cleancoder.com/uncle-bob/2014/10/01/CleanMicroserviceArchitecture.html), 2014-10-01. Primary source stating that sync/async messaging and DLL, thread, process, or microservice deployment are details, not Clean Architecture requirements.
* Robert C. Martin, [Does Organization Matter?](https://blog.cleancoder.com/uncle-bob/2015/04/15/DoesOrganizationMatter.html), 2015-04-15. Supporting primary source for organization changing with scale rather than one permanent layout.

### Publisher and public book sources

* Pearson, [Clean Architecture: A Craftsman's Guide to Software Structure and Design, 1st edition](https://www.pearson.com/en-us/subject-catalog/p/clean-architecture-a-craftsmans-guide-to-software-structure-and-design/P200000009528), publisher page, listing 2017-09-12 and ISBN 9780134494326 for the eTextbook update.
* InformIT, [Clean Architecture: A Craftsman's Guide to Software Structure and Design](https://www.informit.com/store/clean-architecture-a-craftsmans-guide-to-software-structure-9780134494166), publisher storefront, published 2017-09-10, first edition, 432 pages, print ISBN 978-0-13-449416-6.
* InformIT, [The Clean Architecture Dependency Rule](https://www.informit.com/articles/article.aspx?p=2832399), 2017-11-18. Authorized public excerpt from Chapter 22.
* O'Reilly/Pearson, [Clean Architecture digital edition](https://www.oreilly.com/library/view/clean-architecture-a/9780134494272/), September 2017, 432 pages, ISBN 9780134494272. Public chapter previews used for Chapters 13, 14, 26, 28, 30, 31, and 32.

### Talks and transcripts investigated

Three likely Clean Architecture talk URLs on YouTube were tested, but YouTube
redirected the research client to Google sign-in. Titles, uploaders, publication
dates, and transcripts could not be verified reliably, so they are not cited as
evidence. No accessible official transcript was found within the bounded research
session. This is an evidence-access gap, not evidence that no such talk exists.

### Short quotations retained as evidence

* "source code dependencies can only point inwards" (The Clean Architecture)
* "the circles are schematic" (The Clean Architecture)
* "At runtime this is true. But at compile time we want the dependencies inverted." (A Little Architecture)
* "There is no such rule." Martin's answer to one test class per production class (Test Contra-variance)
* "Allow no cycles in the component dependency graph." (public Chapter 14 preview)
* "The granule of reuse is the granule of release." (public Chapter 13 preview)

## Implications for enforceable GitHub Copilot instructions

The strongest instructions are mechanically checkable and distinguish Martin's
requirements from repository conventions.

### Recommended normative rules for a .NET repository

* MUST keep domain/entity and application/use-case projects free of references
	to ASP.NET Core, Entity Framework Core, database providers, UI frameworks,
	transport SDKs, and infrastructure projects.
* MUST make project references point toward higher-level policy. Infrastructure,
	web/API, UI, workers, and hosts may reference application-owned abstractions;
	application policy must not reference their implementations.
* MUST define an outbound port in the inner policy boundary when a use case must
	invoke an outer adapter. Place the interface with the use case that needs it,
	and implement it in the outer adapter project.
* MUST translate framework, transport, and persistence types at the adapter
	boundary. Do not pass `HttpContext`, MVC results, EF entities/change trackers,
	database rows, provider exceptions, UI component models, or SDK response types
	into domain or use-case code.
* MUST use boundary request/response structures owned by the inner side and keep
	them simple. Their shape should serve policy, not mirror an external schema by
	default.
* MUST keep composition, concrete adapter selection, host configuration, and DI
	registration in `Program.cs`, an AppHost, or another outer composition root.
	No inner project may reference the composition root.
* MUST keep controllers/endpoints/UI event handlers thin: adapt input, invoke a
	use case, and adapt output. Business decisions belong in entities or use cases.
* MUST prevent component/project dependency cycles. Add an architecture test or
	CI dependency-graph check where the toolchain supports it.
* MUST test entities and use cases without starting a web host, database,
	container, message broker, or UI. Use separate adapter/integration tests for
	framework wiring and external behavior.
* MUST NOT infer source-reference direction from runtime call order. Review both
	graphs explicitly when a call crosses a boundary.
* MUST NOT prescribe exactly four projects, a specific mediator/CQRS library,
	repositories for every entity, microservices, or one-test-class-per-class as
	Clean Architecture requirements.

### Recommended review checks

* Inspect `.csproj` `ProjectReference` edges and reject inward-to-outward
	references.
* Search domain/application projects for imports from ASP.NET Core, EF Core,
	provider, UI, and infrastructure namespaces.
* Check public use-case signatures for outer/framework-owned types.
* Check that gateway interfaces express use-case needs rather than exposing a
	generic database or ORM API.
* Check that concrete adapter construction occurs only at composition roots.
* Check that policy tests run in memory and that adapter tests cover mappings at
	both sides of every significant boundary.
* Check that top-level structure exposes business capabilities or use cases,
	while framework-specific folders remain peripheral.

### Interpretation and local tailoring

Project boundaries are a useful .NET enforcement mechanism because the compiler
can reject forbidden references, but Martin does not require one project per
circle. A small system may enforce boundaries with namespaces and tests; a larger
system may use assemblies, packages, or services. GitHub Copilot instructions
should name the repository's chosen mechanism separately from the underlying
Dependency Rule.

## Common misinterpretations

* Clean Architecture is not the four-ring folder diagram. The diagram is
	schematic; inward source dependency is the controlling rule.
* "Dependencies point inward" does not mean runtime calls only move inward.
	Outward runtime calls are compatible when inner-owned ports invert source
	dependencies.
* Dependency inversion is not synonymous with constructor injection or a DI
	container. It controls ownership and source references.
* "Database is a detail" does not mean data modeling is unimportant or that no
	database is allowed. It means business policy must not be organized around or
	coupled to a database product and its representations.
* Framework independence does not prohibit frameworks. It confines framework
	coupling to replaceable peripheral code.
* DTOs are not automatically clean. A DTO generated by an outer framework or
	shaped around a database row still violates the rule if inner policy depends
	on it.
* Interfaces everywhere do not create Clean Architecture. Ports are valuable at
	significant boundaries and where dependency direction must be inverted;
	speculative interfaces can add indirection without policy protection.
* A shared "Contracts" project is not automatically neutral. If inner policy
	depends on contracts shaped by HTTP, messaging, persistence, or UI concerns,
	the dependency still points outward semantically.
* Microservices do not guarantee clean boundaries, and a single process does not
	imply a monolith. Martin treats deployment as a detail.
* Use-case-centered organization does not mandate a specific vertical-slice,
	CQRS, handler, or mediator framework. Those are optional implementation
	conventions that must still obey boundary ownership.
* Clean Architecture does not require an anemic domain model. Martin permits
	entities as behavior-rich objects or as data structures plus functions.
* Testing independence does not mean all tests must mock every collaborator.
	The key architectural claim is that policy can be tested without external
	mechanisms; adapter and end-to-end tests still have distinct value.

## Unresolved gaps and clarifying questions

* Official talk metadata and transcripts could not be verified because the
	tested YouTube pages redirected to sign-in. A browser session with transcript
	access or a conference-hosted archive is needed.
* Full text for the book's component-principle sections was not publicly exposed
	in this session. The named principles and two directly previewed definitions
	are verified; exact CCP, CRP, SDP, and SAP wording should be checked against a
	lawfully accessed copy before quoting.
* Pearson lists 2017-09-12 while InformIT lists 2017-09-10. These appear to be
	catalog or format-specific dates. Both are recorded rather than silently
	selecting one.
* No clarifying question blocks the requested repository implications. A later
	implementation task would need the repository's intended project-boundary
	granularity and approved architecture-test tooling.

## Recommended next research

* [ ] Verify an official conference-hosted Clean Architecture recording and its
	transcript, recording title, event, speaker, and publication date.
* [ ] Check Chapters 13, 14, 26, and 28 against a lawfully held book copy for
	exact component-principle, Main, and test-boundary wording.
* [ ] Compare Martin's 1990s Object Mentor component-principle papers with the
	2017 book to identify terminology changes; archived PDFs were discoverable but
	not text-extractable through the research client.
* [ ] For repository adoption, inventory actual `.csproj` edges and framework
	imports, then convert the normative rules into project-specific instructions
	and architecture tests. This implementation work is outside the research-only
	scope.
