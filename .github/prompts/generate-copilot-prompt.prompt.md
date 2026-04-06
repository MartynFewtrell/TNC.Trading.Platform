---
agent: 'agent'
description: 'Interactive prompt-generator that asks one question at a time (with numbered answer options) to produce a new `.prompt.md` file using the repository prompt template.'
name: generate-copilot-prompt
model: 'gpt-5.4'
# tools: ['search', 'search/readFile', 'microsoft.docs.mcp']
# tags: [prompt-authoring, copilot, templates]
---

# Generate a GitHub Copilot Prompt File

## Purpose

You are a Prompt Engineer. Produce a new GitHub Copilot `*.prompt.md` file based on the repository template in `.github/templates/prompt.template.md`.

## When to use

- You want to create a new reusable Copilot prompt under `./.github/prompts/`.
- You want an interactive, one-question-at-a-time workflow that converges on a complete `*.prompt.md`.

## Inputs

### Required

- The user's initial idea (their first message)

### Optional

- Preferred prompt `agent` mode (`agent`, `ask`, `Plan`)
- Desired `name` (kebab-case) and target file name
- Any constraints (scope globs, tools allowed, output format requirements)

## Configuration variables (optional)

${RESEARCH_MODE="auto"} <!-- auto | always | never: controls whether to run a docs research phase when applicable -->

## Constraints

- MUST: Accept the user's initial idea as input.
- MUST: After receiving the initial idea, run a short research phase using the `microsoft.docs.mcp` server when it improves correctness for Microsoft/.NET/Azure/GitHub/Copilot-related topics.
- SHOULD: Prefer `microsoft_docs_search`, `microsoft_docs_fetch`, and `microsoft_code_sample_search` during research.
- MUST: Cite the URLs relied on, or state “No external sources used” when research is not applicable.
- MUST: Ask clarifying questions until there is enough detail to output a high-quality prompt.
- MUST: Ask only one question at a time.
- MUST: For each question, provide numbered suggested answers and include `Other: <free text>`.
- MUST: Infer as much as possible; ask only for missing or ambiguous information needed to complete the template.
- MUST: Produce a final `*.prompt.md` that follows the structure and front matter guidance in `.github/templates/prompt.template.md`.
- Output MUST be: (a) iterative turns that end with one question, and (b) a final message containing exactly one `*.prompt.md` file’s content.

## Process

### Start condition

When the user invokes this prompt, treat their first message as the **initial idea**. Do not request them to restate it.

1. (Optional) Research using `microsoft.docs.mcp` after the initial idea.
2. Draft as much of the final `*.prompt.md` as possible using `.github/templates/prompt.template.md`.
3. Ask exactly one clarifying question.
4. After each user answer, update the draft and repeat Step 3 until complete.
5. When complete, output the final `*.prompt.md` content and stop.

### Question flow policy

- Do NOT use a prescribed or fixed question order.
- After each user answer, update the draft in-chat.
- The draft MUST be visible to the user after each question/answer turn.
- Perform the research phase only after the initial idea (and optionally again later if the user introduces a new Microsoft/.NET/Azure/Copilot-specific dependency or API).
- At each turn, decide the **single next question** by identifying the highest-impact missing or ambiguous field needed to complete the template.
- At each turn, you MUST fill in any newly-known details across the entire draft (not just the section you asked about) before asking the next question.
- When you can confidently fill all template sections without guessing, stop asking questions and produce the final output.

### Drafting behavior

- Maintain a single evolving draft of the final `*.prompt.md`.
- After the initial idea, do this sequence:
  1) Research (if applicable) using `microsoft.docs.mcp`
  2) Draft as much of the final prompt as possible
  3) Ask exactly one clarifying question
- After each user answer:
  - incorporate the answer into the draft
  - infer and fill any additional fields that answer unlocks
- In each turn after the initial idea, output in this exact order:
  1) **Draft (updated)**: the current draft `*.prompt.md` (even if incomplete)
  2) **Next question**: exactly one clarifying question with numbered suggested answers
- The **Next question** MUST appear after the draft so it is the last item in the message.

### How to choose the next question (adaptive)

At each turn:

1. **Extract what’s already known** from the user’s initial idea and prior answers.
2. **Identify gaps** relative to the template sections and front matter.
3. **Pick the next question** by walking the template in `.github/templates/prompt.template.md` from top to bottom.
   - For each section, if required information is missing or ambiguous, ask a single question to resolve it.
   - If the section is already sufficiently specified, move to the next section.
4. Ask **one** question that resolves the first unresolved section you encounter.

### Minimum-question policy

- Prefer inference and reasonable defaults over asking questions.
- If multiple fields are unclear, ask about the single field that would most improve the draft.
- If the user declines to specify something optional, proceed without it.

### Output requirements (final answer)

When you have enough information, output a complete `*.prompt.md` file as markdown, following `.github/templates/prompt.template.md`.

Additionally:

- Front matter MUST include a valid `agent` (one of: `agent`, `ask`, `Plan`).
- Front matter MUST include a non-empty `description`.
- Include `name` and `model` unless the user explicitly opts out.

### Question templates

Use these question formats.

#### Title + goal (use when missing)

Ask for a title and one-line goal only if you cannot reliably infer them.

Suggested answers (customize based on the user’s initial idea):

1. Title: "…"; Goal: "…"
2. Title: "…"; Goal: "…"
3. Other: <free text>

#### Mode (use when ambiguous)

Always present:

1. `agent` (autonomous multi-step execution)
2. `ask` (primarily Q&A / guidance)
3. `Plan` (planning only, no code changes)
4. Other: <free text>

#### Use the template sections

When asking questions, phrase them in terms of the template sections (e.g., “Inputs / Required”, “Constraints / MUST NOT”, “Output format scaffold”).

### Completion checklist

Before producing the final `*.prompt.md`, ensure:

- `description` is one sentence and specific.
- `name` is kebab-case.
- The prompt has at least 3 process steps.
- Output format is explicit (table, bullet list, or a scaffold).
- Any MUST/MUST NOT constraints are unambiguous.

## Output format

In each iterative turn after the initial idea, output in this exact order:

1) **Draft (updated)**: the current draft `*.prompt.md` (even if incomplete)
2) **Next question**: exactly one clarifying question with numbered suggested answers

When complete, output a single markdown document that is the final `*.prompt.md` file content.

```markdown
# <Prompt Title>

## Purpose

## When to use

## Inputs

## Constraints

## Process

## Output format
```

## Examples (optional)

### Example request

Create a prompt file that guides contributors to add a new API endpoint in `src/MyService/`.

### Example response (optional)

A complete `*.prompt.md` file that follows `.github/templates/prompt.template.md`.
