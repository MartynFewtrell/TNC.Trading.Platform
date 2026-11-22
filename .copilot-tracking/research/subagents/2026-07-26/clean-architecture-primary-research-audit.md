---
title: Clean Architecture Primary Research Audit
description: Audit of the completed Clean Architecture Copilot instructions research against its four supporting subagent artifacts and repository rules
author: GitHub Copilot
ms.date: 2026-07-26
ms.topic: reference
---

## Research questions

* Are all major Uncle Bob claims factually supported by the cited primary-source research?
* Are repository paths and line ranges exact?
* Is the selected recommendation logically consistent and supported by a complete alternatives analysis?
* Is the recommendation compatible with existing architecture, folder, and token-discipline rules?
* Does the primary research distinguish verified evidence from interpretation?
* Is the research implementation-ready, complete against the user requirements, and free of stale placeholders?

## Scope

Primary artifact:

* .copilot-tracking/research/2026-07-26/clean-architecture-copilot-instructions-research.md

Supporting artifacts:

* .copilot-tracking/research/subagents/2026-07-26/uncle-bob-clean-architecture-primary-sources-research.md
* .copilot-tracking/research/subagents/2026-07-26/repository-clean-architecture-alignment-research.md
* .copilot-tracking/research/subagents/2026-07-26/clean-architecture-instruction-strategy-research.md
* .copilot-tracking/research/subagents/2026-07-26/clean-architecture-alternatives-and-enforcement-research.md

## Findings

### Medium: Eight local citations have out-of-bounds end lines

All 42 parsed local citations point to existing files, but eight citations use
end lines beyond the current end of file. The underlying claims remain supported;
the defect is citation exactness. This fails the explicit success criterion that
repository findings cite exact paths and line ranges.

In the primary artifact, make these exact replacements:

* Replace both occurrences of
	`src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:7-48` with
	`src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:7-46`.
* Replace
	`src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:1-62`
	with
	`src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:1-60`.
* Replace
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:1-114`
	with
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:1-113`.
* Replace
	`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35`
	with
	`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-33`.
* Replace
	`src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:1-29`
	with
	`src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:1-28`.
* Replace
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:1-14`
	with
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:1-13`.
* Replace
	`src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:1-56`
	with
	`src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:1-54`.

### Medium: Implementation readiness is overstated

The selected direction is logically consistent and the instruction wording is
ready to adapt, but the primary artifact does not select an executable
enforcement surface or a verified activation syntax. It requires a future
prototype comparison between a focused test and an MSBuild target, leaves the CI
entry point uninspected, and leaves the proposed multi-pattern `applyTo` syntax
unverified. Calling the combined recommendation "implementation-ready" therefore
overstates the completed decision.

At the start of `## Implementation-Ready Instruction Blueprint`, replace the
heading and opening ownership text with:

```markdown
## Implementation Blueprint Pending Two Delivery Decisions

The normative instruction content and closed dependency matrix are ready for
implementation. Before editing repository instructions or adding enforcement,
verify a supported multi-pattern `applyTo` form and select the permanent XML
allow-list execution surface by comparing a focused test with an MSBuild target.

### Proposed Ownership and Scope

Update `.github/instructions/architecture.instructions.md` rather than creating
a new file. Proposed frontmatter intent, subject to activation verification:
```

In the executive decision, replace the enforcement paragraph with:

```markdown
Pair the instruction with a dependency-light XML check that parses production
`.csproj` files through a structured XML API. The check must fail on a forbidden
direct reference, an unknown production project, or a cycle. The enforcement
mechanism remains a delivery decision: prototype a focused test and an MSBuild
target, then retain the form with the clearest local and CI failure output.
```

These replacements preserve the selected approach while distinguishing a
research-complete direction from an implementation decision that still needs an
empirical check.

### Low: One heading blurs requirements and supporting design goals

`### Required or Directly Supported` combines Martin's controlling requirement
with consequences, design goals, and a separate component principle. The body
and source artifact generally label interpretation correctly, but this heading
makes the evidence class less clear than the source artifact's explicit
evidence-strength guide.

Replace the `### Required or Directly Supported` subsection with:

```markdown
### Required by the Dependency Rule

* Inward source-code dependency toward policy
* Separation of business policy from replaceable outer details
* Dependency inversion when runtime control must cross outward against the
	permitted source-dependency direction
* No outer representation or software entity named by inner policy

### Directly Supported Properties and Component Guidance

* Framework-independent policy as a design goal
* Policy tests that do not require external machinery
* Simple, isolated boundary data shaped for inner policy
* Composition at the outermost `Main` detail
* Acyclic component dependencies when boundaries are realized as components
```

No critical or high-severity issue was found. No major Uncle Bob claim lacks
support in the primary-source subagent artifact. No requested ownership or
enforcement alternative is materially omitted. No stale placeholder token was
found.

## Evidence

* The primary-source artifact explicitly separates direct primary evidence,
	publisher metadata, interpretation, and unverified material. It supports the
	Dependency Rule, schematic circles, named areas, boundary data, dependency
	inversion, `Main`, testing, optional conventions, and component principles.
* The repository-alignment artifact supports the six-project graph, composition
	roots, adapter mappings, framework leakage, policy placement, test coupling,
	broad friend access, and absence of executable architecture enforcement.
* The instruction-strategy artifact compares standalone, augmentation, and
	layer/file-scoped ownership. It selects augmentation and explains compatibility
	with Vertical Slice Architecture, CQRS, folder rules, and token discipline.
* The alternatives artifact supplies the closed dependency matrix; compares an
	XML check, NetArchTest, ArchUnitNET, and no check; and selects the XML check as
	the first enforcement step.
* The current production project files still match the graph recorded by the
	primary artifact.
* `.github/instructions/architecture.instructions.md:1-87` is the existing owner
	for Vertical Slice and CQRS-style rules.
* `.github/instructions/folders.instructions.md:14-46` rejects duplicate roots
	and unjustified canonical project structures.
* `.github/instructions/token-discipline.instructions.md:38-60` prefers an
	existing owner and requires compatibility checks.
* `.github/instructions/refactoring.instructions.md:22-68` supports dependency
	inversion and narrow, non-speculative abstractions.
* A mechanical audit found 42 local citations, with no missing paths and eight
	out-of-bounds ranges.

The recommendation is logically consistent with the evidence: it preserves the
current acyclic graph, treats API/AppHost references as bounded composition
exceptions, avoids requiring a Domain project, and uses an XML check for the
specific invariant represented in `.csproj` files. The matrix also remains
compatible with current folder and token rules.

## Clarifying questions

None identified.

## Completion assessment

Audit status: complete with corrections required.

Issue count:

* Critical: 0
* High: 0
* Medium: 2
* Low: 1

The primary research completion criteria are not yet fully met. Factual support,
alternatives coverage, recommendation logic, repository-rule compatibility,
user-requirement coverage, and placeholder cleanup pass. Exact citation ranges
and unqualified implementation-readiness wording require correction before the
primary artifact can be accepted as complete.

Recommended next research not completed in this audit:

* [ ] Verify the installed host's supported multi-pattern `applyTo` syntax.
* [ ] Prototype the focused-test and MSBuild-target forms of the XML allow-list.
* [ ] Inspect the CI entry point before selecting the permanent enforcement
	surface.