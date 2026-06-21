---
description: 'Encodes token-thrift operating rules for repository customization assets so agents, prompts, skills, and templates stay concise, evidence-based, and cache-friendly.'
applyTo: '.github/**/*.md'
---

# Token discipline instructions

## Overview

These instructions define token-thrift operating rules for repository customization assets under `./.github/`.
They keep searches targeted, edits incremental, responses concise, and durable guidance stable across sessions.

## Scope

Applies to: `.github/**/*.md`

- These rules apply when creating or updating agent, prompt, skill, instruction, template, and related repository-guidance assets under `./.github/`.
- When multiple instruction files apply, prefer the more specific scope for content rules and use this file for operating discipline.

## Instructions

### MUST

- Load stable guidance before volatile execution context when practical. Preferred order: `./.github/copilot-instructions.md`, `./.github/AGENTS.md`, relevant `./.github/instructions/*.instructions.md`, `./.github/lessons.md`, then task-specific plans, prompts, or reports.
- Start from the most specific relevant asset and keep context gathering narrow. Read only the files and line ranges needed to form the next concrete edit.
- Prefer semantic, symbol, or code-graph-aware search before iterative text-search expansion when those capabilities are available for the current environment.
- Keep searches and reads bounded by using targeted queries, narrow include patterns, capped result sets, and line-range file reads.
- Parallelize independent read-only searches, reads, and validations when it is safe to do so.
- Prefer existing repository scripts or tasks for repeatable validation, generation, and maintenance workflows. When the same command sequence is likely to be reused and no script exists, propose adding one in the appropriate repository location instead of normalizing repeated ad hoc command lists.
- Use diff-first editing for existing files. Prefer patch-style updates over full-file rewrites unless the file is being intentionally replaced.
- Use file-and-line evidence in findings, review reports, mitigation plans, and similar diagnostic outputs. Treat path-only references as insufficient unless the environment cannot provide stable line references.
- Keep completion responses terse and execution-focused. Require a summary section only when the task's durable artifact, template, or report genuinely needs one.
- Stop after three failed attempts on the same implementation slice, diagnosis path, or wording loop. Record the blocker or uncertainty and re-plan or escalate instead of continuing to churn.
- Suggest a fresh chat or an explicit scope reset when the task changes materially enough that the current session no longer represents one coherent objective.
- When repository customization work reveals a stable routing rule, response pattern, or lesson that should survive the current task, update `./.github/AGENTS.md` or `./.github/lessons.md` in the same change.
- Classify repository agents with an explicit routine-or-complex model-tier marker in asset metadata or a central registry when the host supports only one approved model identifier.

### SHOULD

- Prefer updating an existing instruction, agent, or prompt when it already owns the behavior instead of introducing a parallel rule file.
- Prefer concise stable-learning notes in `./.github/lessons.md` over re-encoding the same lesson across multiple asset files.
- Use higher-capability models for cross-file orchestration, multi-step planning, and complex analysis.
- Use lighter-weight models for narrow review, formatting, inventory, or single-slice asset maintenance when the host supports model tiering.
- Prefer `Overview`, `Context`, `Key findings`, or similarly direct section names over generic `Summary` headings in new templates unless a durable artifact format explicitly requires that wording.

### MUST NOT

- MUST NOT broaden repository scanning once a falsifiable local edit target exists.
- MUST NOT emit broad narrative preambles, end-of-turn recaps, or repeated summaries when a short completion statement is sufficient.
- MUST NOT keep retrying the same failing path without changing approach, reducing scope, or surfacing the blocker.
- MUST NOT rewrite entire customization assets just to make small textual edits.

## Output and Validation (optional)

- Expected artifacts: updated `.github/**/*.md` files that encode concise operating guidance without duplicating large sections of existing content.
- Validate success by checking that:
  - new guidance does not contradict existing repository instructions
  - prompt, agent, and instruction assets use compatible response-shape guidance
  - durable reports still permit the structured sections their templates require

## References (optional)

- `./.github/copilot-instructions.md`
- `./.github/AGENTS.md`
- `./.github/lessons.md`