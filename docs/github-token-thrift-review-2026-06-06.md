# .github Token-Thrift Review

This report reviews the assets under `./.github/` against the fourteen token-thrift principles provided for this assessment. The focus is on whether the repository's agent, instruction, prompt, skill, template, and top-level metadata assets encode those principles directly, partially, or not at all.

## Scope Reviewed

- Top-level assets: `CODEOWNERS`, `copilot-instructions.md`
- Agent definitions: 13 files under `./.github/agents/`
- Instruction files: 16 files under `./.github/instructions/`
- Prompt files: 16 files under `./.github/prompts/`
- Skills: 3 skill folders under `./.github/skills/`
- Templates: 13 files under `./.github/templates/`
- Workflows: `./.github/workflows/` exists but is empty

## Strong Alignment

### Planning-first workflow is deeply embedded

The prompt library is built around a staged workflow rather than ad hoc iteration. The help guide defines a fixed sequence from business requirements through delivery, review, and mitigation in [`.github/prompts/help.md`](../.github/prompts/help.md#L7). The delivery orchestrator also requires reading the numbered plan first and executing in plan order in [`.github/agents/execute-delivery.agent.md`](../.github/agents/execute-delivery.agent.md#L43) and [`.github/agents/execute-delivery.agent.md`](../.github/agents/execute-delivery.agent.md#L84).

This aligns well with principle 2, although it is oriented toward large work-package flows rather than lightweight tasks.

### Delegation is explicit and structured

The orchestration agent pushes specialized work to dedicated agents instead of treating all work as a monolith. Examples include delegating broker-auth work in [`.github/agents/execute-delivery.agent.md`](../.github/agents/execute-delivery.agent.md#L24) and generic implementation and test work in [`.github/agents/execute-delivery.agent.md`](../.github/agents/execute-delivery.agent.md#L28).

This is a clear fit for principle 11. The `.github/agents/` folder is one of the strongest parts of the current asset set.

### Repeatable work is captured as reusable skills

The repository guidance explicitly prefers skills for Copilot artifacts in [`.github/copilot-instructions.md`](../.github/copilot-instructions.md#L35). The existing skill set also encodes repeatable operational work, including targeted Stryker execution with scope-narrowing guidance in [`.github/skills/run-stryker/SKILL.md`](../.github/skills/run-stryker/SKILL.md#L15).

This is a good match for principle 4, especially where the task is operational and repeatable.

## Partial Alignment

### Scope discipline exists, but not session discipline

The delivery orchestrator limits questioning and tries to reduce conversational drift in [`.github/agents/execute-delivery.agent.md`](../.github/agents/execute-delivery.agent.md#L40). That helps with principle 1, but no reviewed asset tells the agent to stop and propose a fresh chat when the topic changes. The current assets control task flow inside a session; they do not enforce one task per session.

### Parallel capability is present, but not operationalized as a rule

The TDD agent exposes multiple search, edit, build, and test tools in [`.github/agents/tdd-delivery.agent.md`](../.github/agents/tdd-delivery.agent.md#L5). That means the asset set supports principle 10, but the files reviewed do not consistently direct agents to parallelize independent reads or validations whenever safe.

### Static-first structure is visible, but cache-friendly ordering is not explicit

The main instruction file uses a stable structure of overview, scope, general guidelines, then MUST and SHOULD rules in [`.github/copilot-instructions.md`](../.github/copilot-instructions.md#L6). That is relatively friendly to static context reuse, but none of the reviewed assets explicitly require loading stable reference material before volatile plan or execution state, so principle 9 is only partially reflected.

## Gaps

### No asset encodes output-capping discipline

I did not find guidance that tells agents to cap search results, prefer line-range reads, or use structured result selection. This is a direct miss for principle 5. The reviewed assets optimize for completeness, but not for token thrift during tool usage.

### File-and-line citation is not enforced as a repository reporting norm

The project review agent requires evidence-backed reporting and file creation in [`.github/agents/project-test-reviewer.agent.md`](../.github/agents/project-test-reviewer.agent.md#L10) and [`.github/agents/project-test-reviewer.agent.md`](../.github/agents/project-test-reviewer.agent.md#L18), but the reviewed assets do not impose a repository-wide rule that every code reference must use `file:line`. Principle 6 is therefore not encoded.

### Search strategy preference is missing

The TDD agent includes both `code_search` and `find_references` in [`.github/agents/tdd-delivery.agent.md`](../.github/agents/tdd-delivery.agent.md#L5), but I found no instruction that explicitly says semantic or code-graph search should precede iterative grep-style searching. Principle 7 is not currently represented as an operating rule.

### Model tiering is absent

All reviewed agent definitions use the same top-level model declaration, such as [`.github/agents/execute-delivery.agent.md`](../.github/agents/execute-delivery.agent.md#L4) and [`.github/agents/project-test-reviewer.agent.md`](../.github/agents/project-test-reviewer.agent.md#L4). I did not find a routine-versus-complexity model policy anywhere in `.github`. Principle 8 is not implemented.

### Narrative summaries are still part of the expected response shape

The execution agent explicitly asks for a `Summary` section in [`.github/agents/execute-delivery.agent.md`](../.github/agents/execute-delivery.agent.md#L130). That conflicts with principle 3, which rejects preambles, narration, and end-of-turn summaries. The current assets favor structured summaries over terse completion output.

### Stable-learning artifacts are missing

There is no `AGENTS.md` under `.github`, and there is no `lessons.md` under `.github`. File searches for both names returned no matches. That leaves principle 12 unsupported, even though the broader repository guidance already expects reusable Copilot artifacts and stable patterns to be captured.

### Retry-budget and blocker rules are not defined

The reviewed assets encourage escalation when blocked, but I did not find an explicit "stop after three failed attempts" rule. Principle 13 is not encoded as a hard operating constraint.

### Diff-only editing is not enforced at the asset level

The TDD agent permits editing via `edit_file` in [`.github/agents/tdd-delivery.agent.md`](../.github/agents/tdd-delivery.agent.md#L5), but the reviewed `.github` assets do not define a preference for diff-based edits over full-file rewrites. Principle 14 is not represented directly.

## Internal Tensions

### Skills are preferred, but prompts remain a large active surface

The repository-wide instruction says to implement requested Copilot artifacts as skills rather than prompts in [`.github/copilot-instructions.md`](../.github/copilot-instructions.md#L35). At the same time, `.github/prompts/` still contains sixteen prompt assets and the prompt workflow guide continues to describe them as the normal delivery path in [`.github/prompts/help.md`](../.github/prompts/help.md#L7).

That is not necessarily wrong, but it does mean the asset set is carrying two different extension models at once. From a token-thrift perspective, that increases surface area and can blur the preferred path.

### Documentation-heavy orchestration favors completeness over thrift

The project review agent requires a physical report file and broad evidence collection in [`.github/agents/project-test-reviewer.agent.md`](../.github/agents/project-test-reviewer.agent.md#L18). That matches this repository's quality-reporting goals, but it pulls away from strict principle 3 and principle 5 because it optimizes for durable documentation rather than minimal token spend.

## Overall Assessment

The `.github` asset set is strong on planning, specialization, repeatable workflows, and durable documentation. It is weak on explicit token-thrift controls. The biggest gaps are output capping, search-order discipline, model tiering, retry budgets, diff-only editing rules, and the absence of `AGENTS.md` and `lessons.md`.

If the goal is to align this repository more closely with the supplied token-thrift principles, the highest-leverage changes would be:

1. Add a compact token-discipline instruction file covering output limits, search order, retry budgets, and diff-only edits.
2. Add `AGENTS.md` and `lessons.md`, then reference them from [`.github/copilot-instructions.md`](../.github/copilot-instructions.md#L35).
3. Decide whether prompts are legacy support or first-class artifacts, and make that consistent across [`.github/copilot-instructions.md`](../.github/copilot-instructions.md#L35) and [`.github/prompts/help.md`](../.github/prompts/help.md#L7).
4. Introduce model-tier guidance so routine review and generation agents are not all pinned to the same model declaration.