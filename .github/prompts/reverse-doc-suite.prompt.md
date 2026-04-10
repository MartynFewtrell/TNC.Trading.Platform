---
agent: 'agent' # one of: agent | ask | Plan
description: 'Reverse-engineer the repository into a complete /docs markdown documentation suite enabling a clean-room reimplementation with equivalent functionality.'

# Strongly recommended
name: reverse-doc-suite
model: 'gpt-5.4'

# Optional
# tools: ['search', 'search/readFile', 'runCommands', 'web/fetch']
# tags: [documentation, reverse-engineering, architecture, dotnet]
---

# Reverse Engineer Application into a Documentation Suite

## Purpose

You are a software archaeologist and technical writer. Analyze the existing repository end-to-end (product code, tests, infrastructure, CI/CD, and configuration) and produce a **full suite of Markdown documents** under `/docs` that explains:

- What the application does (user-facing behavior and workflows)
- How it works (architecture, components, data flow, integration points)
- How to build, run, test, deploy, and operate it
- Enough functional + technical detail for an engineer to build a new application with equivalent functionality

## When to use

- You inherited an application with insufficient documentation and need comprehensive internal docs.
- You need to replicate application functionality in a new implementation and want requirements + technical behavior reconstructed from the code.

## Inputs

### Required

- The current repository workspace (all files under `.github/`, `src/`, `test/`, `infra/`, and `docs/`)
- Ability to read source code, configuration, and build/CI files in this repo

### Optional

- Ability to run builds/tests locally (if supported by the environment) to validate assumptions
- Access to runtime logs/config samples already committed in the repo

## Configuration variables (optional)

${DOCS_ROOT="docs"} <!-- output directory for generated documentation -->
${OVERWRITE_DOCS="true"} <!-- true|false: overwrite existing docs files with same name -->
${INCLUDE_MERMAID="true"} <!-- true|false: include mermaid diagrams where helpful -->
${RUN_BUILD_AND_TESTS="auto"} <!-- auto|always|never: run build/tests if feasible -->
${EVIDENCE_LEVEL="high"} <!-- high|medium: how aggressively to cite file/line evidence -->

## Constraints

- MUST: Do **not** ask clarifying questions; assume all necessary information exists in the repository.
- MUST: Store all generated documentation in `${DOCS_ROOT}/` as Markdown (`.md`) files.
- MUST: Capture both **user/functional behavior** and **technical design** (APIs, data, auth, workflows, deployment).
- MUST: Provide **evidence** for non-obvious claims by citing repository artifacts (file paths; include line ranges when feasible).
- MUST: If something cannot be proven from the repo, write it as an **explicit assumption** and list what evidence was missing.
- MUST NOT: Modify production application behavior. Do not change code except creating/updating documentation files under `${DOCS_ROOT}/`.
- SHOULD: Prefer concrete, testable statements over generic descriptions (e.g., list endpoints, commands, schemas, message contracts).
- Output MUST be: (1) a created/updated set of `${DOCS_ROOT}/**/*.md` files, and (2) a short final summary of what was produced and what remains uncertain.

## Process

1. Inventory the repository
2. Reconstruct functional requirements from code
3. Map architecture and boundaries
4. Document data and integrations
5. Document build/run/deploy/operate
6. Validate and cross-link

## Output format

Create/update the following files under `${DOCS_ROOT}/` (add more if the repo demands it, but do not omit these unless truly not applicable):

```markdown
# ${DOCS_ROOT}/index.md

## Summary
- What the system is
- Who uses it
- What it integrates with

## Doc set map
- [Application overview](./application-overview.md)
- [User workflows](./user-workflows.md)
- [Architecture](./architecture.md)
- [Components & services](./components-and-services.md)
- [Data model](./data-model.md)
- [APIs](./api-reference.md)
- [Authentication & authorization](./auth.md)
- [Integrations](./integrations.md)
- [Configuration](./configuration.md)
- [Local development](./local-development.md)
- [Deployment & infrastructure](./deployment.md)
- [Operations & observability](./operations.md)
- [Testing strategy](./testing.md)
- [Assumptions & gaps](./assumptions-and-gaps.md)
- [Appendix: code map](./appendix-code-map.md)
```

Each document MUST follow this internal structure (adapt headings as needed, but keep the intent):

```markdown
# <Title>

## Purpose
What this document covers.

## Key points (TL;DR)
- Bullet list of the most important facts.

## Details
Concrete, evidence-backed explanation.

## Evidence (repo references)
- `path/to/file.ext` (lines a–b): why it matters
- `path/to/another.file`: why it matters

## Open questions / assumptions (if any)
- Assumption: ...
  - Missing evidence: ...
  - Risk/impact: ...
```

Minimum required document topics:

- `application-overview.md`: what it does, core capabilities, major user value
- `user-workflows.md`: primary workflows and state transitions (use sequences if helpful)
- `architecture.md`: high-level architecture + mermaid diagrams (context/component)
- `components-and-services.md`: responsibilities per project/module/service
- `data-model.md`: persistence approach, key entities, relationships, migration strategy
- `api-reference.md`: endpoints/routes, inputs/outputs, auth requirements, error semantics
- `auth.md`: identity provider assumptions, token/claims usage, authorization model
- `integrations.md`: external systems/APIs, message buses, schedules, file exchanges
- `configuration.md`: config sources, env vars, appsettings structure, secrets
- `local-development.md`: prerequisites and exact steps/commands if discoverable
- `deployment.md`: environments, infra definitions, container/runtime, CI/CD outline
- `operations.md`: logging/metrics/tracing, health checks, alerts, runbook procedures
- `testing.md`: test pyramid used, how to run tests, key suites and conventions
- `assumptions-and-gaps.md`: everything uncertain + what to verify next
- `appendix-code-map.md`: curated map of “start here” files and directories

In your final response after writing files, include:

```markdown
# Documentation Suite Generated

## Summary
- Docs created/updated: <count>
- Primary entry points identified: <list>
- Biggest assumptions: <list>

## Files
- `docs/index.md`
- ... (all produced docs)

## Next steps
- Concrete verification tasks (build/test/deploy checks) based on remaining uncertainty
```

## Examples (optional)

### Example request

Reverse engineer this codebase and generate a full documentation suite under `/docs`.

### Example response (optional)

A created/updated set of `/docs/*.md` files plus a short final summary listing what was produced.
