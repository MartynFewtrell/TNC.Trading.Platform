# Handoff: {Work Item N} {Task N (optional)} — {Agent Role}

<!-- NAMING: save this file as docs/{00x-work}/handoffs/wi{N}-{task-description}-{agent-role}.md
     Examples:
       wi1-task1-documentation-specialist.md
       wi2-broker-auth-integration-agent.md
       wi3-task2-frontend-blazor-agent.md
     Pattern: wi{work-item-number}-{short-kebab-description}-{agent-role}.md
-->

## Target agent

{Agent Role Name}

## Work item reference

`docs/{00x-work}/plans/{NNN}-{plan-name}.md` — Work Item N{, Task N (Steps N–N)} <!-- adjust as needed -->

## Delivery context

- Branch: `{branch-name}`
- Baseline: `dotnet build` succeeds; {N}/{N} unit tests pass
- {Any prerequisite work items or tasks that must be complete before this handoff is executed}

## Scope boundaries — read carefully

**In scope for this handoff:**

- {Bullet list of what this agent is responsible for delivering}

**Out of scope — do not touch:**

- {Bullet list of files, components, or concerns explicitly excluded}

## Key files to read before starting

| File | What to note |
| ---- | ------------ |
| `{path/to/file.cs}` | {Why this file matters and what to look for} |

## Deliverables

<!-- List each deliverable as a numbered H3 section. Be specific: include exact file paths,
     interface/class names, method signatures, and code blocks where precision matters. -->

### Deliverable 1 — {Title}

{Description of what must be created or modified, with enough detail for the agent to act without
 further research. Include exact code blocks where the shape of the output matters.}

### Deliverable 2 — {Title}

{Description}

<!-- Add further deliverables as needed -->

## Validation gates

After all deliverables are complete:

1. Run `dotnet build` — must succeed with zero errors
2. Run `dotnet test --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~E2ETests&FullyQualifiedName!~FunctionalTests"` — all unit tests must pass, including all newly added tests
3. {Any additional confirmation checks specific to this handoff}

## Assumptions to validate

- {List any assumptions the agent should verify before coding, especially enum values, DI lifetimes,
  or registration expectations that could differ from what the handoff assumes}

---

## Agent report

<!-- The sub-agent completing this handoff MUST fill in this section before marking the handoff done.
     Do not leave any subsection blank; write "N/A" if genuinely not applicable. -->

### Files created

<!-- List every new file path created. -->

- {path/to/new-file.cs}

### Files modified

<!-- List every existing file path modified. -->

- {path/to/modified-file.cs}

### Unit tests added

<!-- State the number of new test methods added and list the test class(es) they were added to. -->

Total added: {N}

| Test class | Count |
| ---------- | ----- |
| `{Namespace.TestClass}` | {N} |

### Build gate outcome

```
dotnet build — {succeeded / failed}
{Any warnings worth noting}
```

### Test gate outcome

```
dotnet test (unit filter) — {succeeded / failed}
Total: {N}, Passed: {N}, Failed: {N}, Skipped: {N}
```

### Assumptions validated

<!-- Confirm or refute each assumption listed in "Assumptions to validate" above. -->

- {Assumption text}: {confirmed / refuted — explanation}

### Deviations from handoff

<!-- Any place where the agent diverged from the handoff specification and why.
     Write "None" if the implementation matched the handoff exactly. -->

{None | deviation description and rationale}

### Escalations

<!-- Any blocking issues, ambiguities, or decisions that require the orchestrator's attention.
     Write "None. Ready for next handoff." when there is nothing to escalate. -->

{None. Ready for next handoff. | escalation description}
