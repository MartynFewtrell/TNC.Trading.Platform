# Generic Prompt Template

Copy this file content into a new `*.prompt.md` file and fill in the placeholders.

```markdown
---
agent: 'agent' # one of: agent | ask | Plan
description: '<one sentence describing what this prompt does>'

# Strongly recommended
name: <kebab-case-short-name>
model: 'gpt-5.2' # or the model you validated this prompt with

# Optional
# tools: ['search', 'web/fetch', 'runCommands', ...]
# tags: [domain, framework, task-type]
---

# <Prompt Title>

## Purpose

<Describe the role the assistant should take and the outcome it must produce.>

## When to use

- <Use case 1>
- <Use case 2>

## Inputs

### Required

- <What the user must provide (files, URLs, requirements, constraints)>

### Optional

- <Optional context, preferences, tech choices>

## Configuration variables (optional)

${VAR_NAME="default"} <!-- explain what this controls -->

## Constraints

- MUST: <non-negotiable requirement>
- MUST NOT: <prohibited behavior>
- SHOULD: <preferred behavior>
- Output MUST be: <format>

## Process

1. <Step 1>
2. <Step 2>
3. <Step 3>

## Output format

<Specify exactly what the assistant should return.>

```markdown
# <Output Title>

## Summary

## Details

## Next steps
```

## Examples (optional)

### Example request

<user request example>

### Example response (optional)

<short example of the expected shape of the answer>
```
