# Understanding the HVE Process

This document captures my understanding of the Hypervelocity Engineering (HVE) process as implemented in microsoft/hve-core, with a focus on how HVE structures complex engineering work, how agent handoffs operate, and how refactoring is handled within that system.

## What HVE Is

Hypervelocity Engineering (HVE) is a structured workflow system for GitHub Copilot that combines prompts, agents, instructions, and skills into a repeatable engineering process.

At a high level, HVE separates concerns so that:

- prompts define the user-facing workflow entry point
- agents orchestrate execution
- instructions apply standards automatically based on file context
- skills provide executable utilities beyond conversational guidance

This separation is important because it keeps user intent, execution logic, coding standards, and specialized tooling distinct.

## The Core Delegation Model

HVE uses a layered delegation model:

1. A user invokes a prompt or directly selects an agent.
2. The prompt routes the request to a specific agent.
3. The agent decides how to execute the task.
4. The agent applies matching instructions automatically based on the files involved.
5. The agent invokes skills or subagents when a specialized operation is needed.

This means HVE is not just a prompt library. It is a workflow architecture that treats AI assistance as an engineered system with explicit responsibilities and boundaries.

## The Main Artifact Types

### Prompts

Prompts are workflow entry points. They answer the question: what does the user want to accomplish?

Prompts usually:

- capture user intent
- define the entry workflow
- route to a target agent through frontmatter

In practice, prompts are short and declarative. They do not contain the full implementation method.

### Agents

Agents are the core workflow orchestrators. They answer the question: how should this task be executed?

Agents can:

- define behavioral rules
- control tool usage
- declare subagent dependencies
- expose user-facing handoff buttons
- maintain multi-step workflow state

This is where most of the real process logic lives.

### Instructions

Instructions are passive standards. They apply automatically to matching files and shape how code or documentation should be produced.

They answer the question: what standards apply here?

### Skills

Skills are active utilities. They provide documented executable capabilities, usually backed by scripts or reusable operational guidance.

They answer the question: what specialized utility is needed?

## The RPI Workflow

The most important HVE engineering workflow is RPI:

1. Research
2. Plan
3. Implement
4. Review

This workflow exists because HVE assumes that complex engineering work fails when discovery, design, implementation, and validation are mixed together in a single unconstrained interaction.

The key idea is that each phase has a different optimization target:

- Research optimizes for verified truth
- Plan optimizes for execution structure
- Implement optimizes for bounded change and validation
- Review optimizes for conformance, completeness, and quality

HVE also stresses that context should usually be cleared between major phases so each agent works under the right constraints rather than inheriting mixed objectives from prior chat history.

## The Main RPI Agents

### RPI Agent

RPI Agent is the autonomous orchestrator. It can complete simpler work directly and switch to a heavier document-backed workflow when complexity increases.

Its lifecycle is:

1. Research
2. Plan
3. Implement
4. Review
5. Discover

The extra Discover phase is important. HVE treats follow-on work, refactoring opportunities, and remaining gaps as first-class outputs rather than leaving them implicit.

### Task Researcher

Task Researcher is research-only. It does not plan or implement. Its job is to create one authoritative research document grounded in evidence from the codebase and external references when needed.

### Task Planner

Task Planner converts research into execution artifacts. It produces:

- an implementation plan
- an implementation details file
- a planning log

The planner is not supposed to write production code. Its role is to define phases, dependencies, validation steps, and traceability.

### Task Implementor

Task Implementor executes the plan. It reads the planning artifacts, works phase by phase, tracks progress, and keeps a changes log.

This agent is where planned refactoring or maintainability improvements typically get applied.

### Task Reviewer

Task Reviewer validates that implementation matches the plan and that the resulting code is sound. It combines specification conformance review with broader quality validation.

## How Handoffs Work

HVE uses two different handoff mechanisms.

### User-Facing Handoffs

These are explicit workflow transitions exposed in agent frontmatter.

Examples include:

- Research to Plan
- Plan to Implement
- Implement to Review
- Review back to Research
- Review back to Plan
- Review back to Implement

These handoffs are meant to make phase transitions easy and visible in the UI.

### Internal Subagent Delegation

This is the more important mechanism from an implementation perspective.

Top-level agents can delegate bounded work to subagents. This is not the same as a user-facing handoff. It is internal orchestration.

The parent agent stays responsible for:

- deciding when delegation is needed
- framing the subtask
- updating tracking artifacts
- reconciling results
- deciding what happens next

The subagent stays responsible for one narrow job.

This is the core engineering pattern in HVE.

## The Key Refactoring Pattern in HVE

HVE does not appear to define a single standalone refactoring agent. Instead, refactoring is implemented as a capability distributed across the RPI system.

That happens in several ways.

### Refactoring as Planned Implementation Work

Refactoring is commonly treated as implementation work that is:

- researched first
- turned into explicit plan steps
- executed in bounded phases
- validated after each phase

This prevents large speculative rewrites.

### Refactoring as Review-Driven Rework

The review phase can identify maintainability issues, structural mismatches, or incomplete work. When that happens, HVE loops back into implementation, planning, or research as needed.

This makes refactoring iterative and evidence-based rather than open-ended.

### Refactoring as a Validation Category

Implementation Validator has an explicit refactoring-oriented quality lens. It looks for issues such as:

- long methods
- deep nesting
- parameter bloat
- god classes
- shotgun surgery
- primitive obsession
- weak testability
- structural complexity

This means refactoring is not only something HVE performs. It is also something HVE checks for systematically.

## The Subagents That Matter Most

### Researcher Subagent

Researcher Subagent answers tightly scoped research questions and writes a dedicated research artifact. It stops once each question has evidence.

This is used when the parent agent needs focused discovery without broadening the whole conversation.

### Phase Implementor

Phase Implementor executes one bounded implementation phase only.

It receives:

- the phase identifier
- the relevant plan steps
- the details section
- research context
- applicable instruction files
- validation commands

It performs the work for that phase and returns a structured completion report.

This is one of the most important patterns in HVE because it lets the parent agent parallelize or isolate implementation safely.

### Plan Validator

Plan Validator compares the implementation plan and details against the research document.

Its job is to detect:

- missing research coverage
- plan deviations from research
- broken or ambiguous references

It updates only the discrepancy section of the planning log.

### RPI Validator

RPI Validator checks whether a completed implementation phase actually matches the plan and changes log.

It performs both:

- plan item to change matching
- change entry to plan item matching

This is important because it catches both missing work and scope creep.

### Implementation Validator

Implementation Validator assesses broader code quality. It covers:

- architecture
- design principles
- DRY concerns
- API and library usage
- refactoring opportunities
- error handling
- test coverage
- security

This is how HVE separates implementation correctness from implementation quality.

## Why the Tracking Files Matter

HVE stores durable workflow state in files under .copilot-tracking rather than relying on chat memory.

Typical artifacts include:

- research documents
- plan files
- details files
- planning logs
- changes logs
- review logs

This has several benefits:

- work survives context resets
- handoffs between agents are explicit
- implementation status is inspectable
- validation findings are durable
- team handoff becomes easier

This file-based workflow is one of the strongest design choices in HVE.

## The Parent and Subagent Boundary

One of the clearest implementation patterns in HVE is the boundary between orchestrators and executors.

The parent agent is responsible for:

- deciding what phase comes next
- choosing whether to delegate
- selecting the right subagent
- supplying bounded inputs
- updating durable artifacts
- interpreting results

The subagent is responsible for:

- doing one narrow job
- writing its own output artifact
- returning only a concise executive summary
- avoiding further orchestration

This is effectively a command-and-control model for AI workflow composition.

## Why This Matters for Refactoring Work

For refactoring and mitigation-plan execution, HVE suggests several useful principles.

### Keep Refactoring Bounded

Refactoring should be phase-scoped and tied to explicit success criteria. HVE avoids broad, speculative rewrites.

### Research Before Structural Change

Before changing structure, identify existing codebase patterns, architectural boundaries, and applicable standards.

### Track Progress in Durable Artifacts

Plans, discrepancies, changes, and review findings should be written down rather than left in chat history.

### Validate Twice

HVE validates both:

- whether the planned change was actually implemented
- whether the resulting code is high quality and structurally sound

### Use Iteration Loops Explicitly

When review reveals gaps, the workflow returns to:

- Research for missing context
- Plan for scope or sequencing fixes
- Implement for code changes

That loop is deliberate rather than exceptional.

## My Summary of the HVE Process

My current understanding is that HVE is an engineering operating model for Copilot-driven work, not just a set of prompts.

Its core characteristics are:

- strict separation of workflow phases
- explicit orchestration by specialized agents
- narrow delegation to subagents
- durable tracking artifacts on disk
- standards applied automatically through instructions
- iterative validation and rework loops
- deliberate discovery of next work, including refactoring

For refactoring specifically, HVE does not rely on a single magic refactoring agent. Instead, it treats refactoring as disciplined implementation work within a broader Research, Plan, Implement, Review system.

That makes the process more controllable, more auditable, and better suited to real multi-file engineering work than a one-shot refactor prompt.

## References

- HVE Core repository: https://github.com/microsoft/hve-core
- HVE Core README: https://raw.githubusercontent.com/microsoft/hve-core/main/README.md
- HVE agents catalog: https://raw.githubusercontent.com/microsoft/hve-core/main/.github/CUSTOM-AGENTS.md
- HVE RPI overview: https://raw.githubusercontent.com/microsoft/hve-core/main/docs/rpi/README.md
- HVE RPI workflow walkthrough: https://raw.githubusercontent.com/microsoft/hve-core/main/docs/rpi/using-together.md
- HVE task implementor guide: https://raw.githubusercontent.com/microsoft/hve-core/main/docs/rpi/task-implementor.md
- HVE AI artifacts architecture: https://raw.githubusercontent.com/microsoft/hve-core/main/docs/architecture/ai-artifacts.md
- HVE contributing guide for agents: https://raw.githubusercontent.com/microsoft/hve-core/main/docs/contributing/custom-agents.md