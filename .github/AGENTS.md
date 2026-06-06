# Agent operating model

This file captures the stable operating model for repository Copilot assets under `./.github/`.

## Asset hierarchy

Use the repository asset types in this order of preference.

1. Instructions for stable repository rules and scoped standards.
2. Agents for specialized multi-step execution roles.
3. Skills for repeatable operational tasks.
4. Prompts only when maintaining the existing prompt library or when a prompt-format artifact is explicitly required.

## Context loading order

When working on repository customization assets, load context in this order when practical.

1. `./.github/copilot-instructions.md`
2. this file
3. the relevant scoped instruction files under `./.github/instructions/`
4. `./.github/lessons.md`
5. the task-specific asset being changed

## Delegation model

- Use `execute-delivery` for plan-driven orchestration and cross-agent coordination.
- Use `tdd-delivery` for narrow implementation slices that should follow red-green-refactor.
- Use `minimal-api-slice`, `blazor-operator-ui`, and `broker-auth-integration` for domain-specific delivery work.
- Use `documentation-specialist` for markdown-heavy authoring or wiki alignment.
- Use `project-test-reviewer` and `test-stability-investigator` for independent testing review and flaky-test diagnosis.

## Response discipline

- Prefer terse execution updates over narrative status reporting.
- Use file-and-line evidence in findings when the output format supports it.
- Preserve structured summaries inside durable markdown reports when the template or report purpose requires them.
- Avoid requiring summary sections in chat responses unless they add clear operational value.

## Session discipline

- Keep each session focused on one coherent objective.
- If the topic changes materially, suggest a fresh chat or an explicit scope reset instead of blending unrelated work.

## Model-tier policy

- Complex orchestration, cross-file reasoning, and review synthesis should use the highest-capability available model tier.
- Narrow formatting, inventory, or single-slice asset maintenance may use a lighter tier when the host supports it.
- Do not change model declarations mechanically across all agents without a task-based reason.

## Tier registry

- `complex`: `aspire-expert`, `blazor-operator-ui`, `broker-auth-integration`, `execute-delivery`, `minimal-api-slice`, `pr-resolution`, `project-test-reviewer`, `tdd-delivery`, `test-stability-investigator`
- `routine`: `documentation-specialist`, `generate-delivery-plan`, `generate-requirements`, `generate-technical-spec`

## Stable-learning upkeep

- When repository customization work confirms a durable routing rule, response contract pattern, or authoring lesson, update this file or `./.github/lessons.md` in the same change.
- Prefer adding one concise durable note here over restating the same lesson across multiple prompts or agents.