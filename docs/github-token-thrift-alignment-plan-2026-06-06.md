# GitHub token-thrift alignment plan

This document translates the findings from the `.github` token-thrift review into a concrete implementation plan for the repository's Copilot assets under `.github/` and related guidance under `docs/`.

## Summary

- **Source input**: [Token-thrift review](./github-token-thrift-review-2026-06-06.md)
- **Objective**: align the repository's agent, instruction, prompt, and skill assets more closely with the reviewed token-thrift principles without weakening the repository's existing planning, review, and documentation standards
- **Scope**: `.github/copilot-instructions.md`, `.github/agents/`, `.github/instructions/`, `.github/prompts/`, `.github/skills/`, and any new stable-learning assets that belong under `.github/`
- **Out of scope**: runtime application code, production architecture changes, and unrelated documentation cleanup outside the token-thrift objective

## Planning goals

The plan is intended to address four categories of observations from the review.

1. Close the direct gaps called out in the review.
2. Tighten the partially aligned operating rules so they become explicit repository policy.
3. Resolve internal tensions where current assets send mixed signals.
4. Preserve the repository's existing strengths in planning, delegation, and durable documentation.

## Non-goals

This plan does not aim to:

- remove work-package planning or review artifacts already established in the repository
- replace durable markdown deliverables with chat-only output
- collapse all prompts, agents, and skills into a single artifact type
- optimize for token thrift at the expense of reviewability, auditability, or repository maintainability

## Delivery approach

- **Delivery model**: single documentation and customization alignment effort, delivered in small reviewable batches
- **Change strategy**: introduce the minimum rule set needed to encode token-thrift behavior directly, then update the existing assets that currently contradict or omit those rules
- **Decision principle**: where token thrift conflicts with durable engineering documentation, prefer concise durable artifacts over verbose chat narration
- **Primary risk**: over-correcting toward minimalism and weakening repository-specific planning and review standards
- **Primary mitigation**: encode thrift rules as bounded operating guidance rather than as absolute bans on documentation, plans, or evidence files

## Workstreams

### Workstream 1: Add token-discipline operating rules

Create a compact instruction file under `.github/instructions/` that converts the review's missing thrift controls into explicit repository policy.

Planned changes:

- add rules for capped search and read behavior, including targeted search first, line-range reads, and result limiting
- add a preferred search order that starts with semantic or code-graph-aware search when available, then falls back to text search
- add a retry budget rule that requires escalation or re-planning after repeated failed attempts on the same slice
- add a diff-first editing rule so repository customization assets prefer patch-style updates over full rewrites
- add a response-shape rule that discourages narrative preambles and low-value summaries when a terse completion response is sufficient

Expected outcome:

- principle gaps for output capping, search-order discipline, retry budget, and diff-only editing become directly encoded

### Workstream 2: Add stable-learning assets

Introduce stable repository guidance artifacts that capture enduring operating patterns instead of relying on implicit behavior.

Planned changes:

- add `.github/AGENTS.md` to document the repository's agent operating model, preferred delegation boundaries, and asset hierarchy
- add `.github/lessons.md` to capture short durable learnings that should survive across sessions
- update `.github/copilot-instructions.md` so these files are part of the expected operating context and are referenced alongside skills and instruction files

Expected outcome:

- the repository gains explicit support for the review's stable-learning expectation without introducing a new top-level documentation root

### Workstream 3: Tighten session and execution discipline

Convert the review's partial alignment areas into direct operating rules.

Planned changes:

- add guidance for one-task-per-session discipline, including when to suggest a fresh chat because the topic has materially changed
- add an explicit rule to parallelize independent reads, searches, and validations when safe
- add a stable-first context-loading rule so static repo guidance is loaded before volatile execution state where practical
- add a repository reporting norm that code references should use file-and-line evidence when the output format supports it

Expected outcome:

- the current implicit discipline becomes explicit, repeatable, and easier to evaluate during future reviews

### Workstream 4: Resolve prompt, skill, and summary tensions

Reduce ambiguity in the current asset model so the repository expresses one coherent extension strategy.

Planned changes:

- decide whether `.github/prompts/` remains a first-class asset surface or is maintained as transitional support for legacy flows
- update `.github/copilot-instructions.md` and `.github/prompts/help.md` so they describe the same preferred extension path
- review agent output contracts that currently require broad `Summary` sections and replace them with more concise completion formats where possible
- preserve durable report-file requirements where the repository genuinely needs evidence, but remove default chat-summary requirements that add little value

Expected outcome:

- the repository keeps its documentation-heavy review model where needed while removing mixed signals about preferred artifact types and response verbosity

### Workstream 5: Introduce model-tier guidance

Add explicit guidance for model selection so routine work is not automatically treated the same as complex analysis or delivery orchestration.

Planned changes:

- define a lightweight routine-work tier for focused review, formatting, narrow edits, and simple generation tasks
- define a higher-capability tier for multi-step planning, refactoring analysis, cross-file reasoning, and complex delivery orchestration
- update relevant agent definitions only where the current single-tier approach is misaligned with actual task complexity

Expected outcome:

- the repository addresses the review's model-tier gap while avoiding unnecessary churn across every agent definition

## Sequenced plan

| Phase | Focus | Deliverables | Dependencies |
| --- | --- | --- | --- |
| 1 | Define core thrift rules | new token-discipline instruction file; updates to `.github/copilot-instructions.md` | None |
| 2 | Add durable learning assets | `.github/AGENTS.md`; `.github/lessons.md`; cross-references from repo guidance | Phase 1 |
| 3 | Align execution behavior | targeted updates to agent and instruction files for session discipline, parallelism, static-first loading, and file-line evidence | Phases 1-2 |
| 4 | Resolve asset-surface tensions | aligned guidance across prompts, skills, and agent response shapes | Phases 1-3 |
| 5 | Introduce model-tier policy | targeted model guidance updates in agent definitions and repo instructions | Phases 1-4 |

## Validation strategy

Each phase should be validated before the next phase is considered complete.

Validation activities:

- verify all new or changed documentation and customization files remain in the correct repository folders
- confirm instruction files, prompt files, skills, and agent assets do not contradict the newly added thrift rules
- review updated guidance for duplicate or overlapping rules that would create ambiguity
- confirm relative links between `.github` and `docs` assets resolve correctly after edits
- perform a focused review of agent response contracts to ensure concise-output guidance does not remove required durable reporting behaviors

## Acceptance criteria

The plan is complete when all of the following are true.

- the repository contains an explicit token-discipline rule set under `.github/instructions/`
- `.github/AGENTS.md` and `.github/lessons.md` exist and are referenced from repository guidance
- repository instructions explicitly address output capping, search order, retry budgets, diff-first editing, and concise response shaping
- session discipline, safe parallelism, stable-first context loading, and file-line evidence are encoded as direct operating rules rather than implied behavior
- `.github/copilot-instructions.md` and `.github/prompts/help.md` no longer send conflicting signals about whether prompts or skills are the preferred extension mechanism
- agent response contracts no longer require broad summary sections unless a durable report is actually part of the task
- the repository defines model-tier guidance for routine versus complex tasks

## Open decisions

These decisions should be made early because they affect several downstream edits.

1. Should prompts remain first-class artifacts, or should they be explicitly repositioned as legacy or transitional support?
2. Should concise-output rules be enforced globally, or only for selected agents and instructions where verbose summaries are currently required?
3. Should model-tier guidance be expressed as advisory policy in instructions, or enforced directly in agent metadata where supported?

## Recommended execution order

1. Create the token-discipline instruction file first so later edits can align to a stable policy.
2. Add `AGENTS.md` and `lessons.md` next so stable-learning guidance exists before broader agent cleanup.
3. Update repository-wide instructions before editing individual agents and prompts.
4. Resolve the prompt-versus-skill position before changing large numbers of prompt or agent assets.
5. Apply model-tier guidance last so it reflects the final asset structure rather than an intermediate state.

## Notes

- This plan intentionally treats the review's "highest-leverage changes" as the starting point, but expands the scope enough to cover the partial-alignment findings as well.
- The plan does not assume every observed gap must be solved with a new file. Where an existing instruction or agent file is the right home for a rule, the preferred change is to update that asset rather than create a parallel document.
- If this plan is executed, the follow-up implementation work should produce a new physical review or alignment report rather than overwrite the source review document.