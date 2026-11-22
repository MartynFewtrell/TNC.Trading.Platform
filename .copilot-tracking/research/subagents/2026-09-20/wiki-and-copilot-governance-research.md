---
title: Wiki and Copilot Governance Research
description: Evidence-based research on environment documentation and planning governance.
ms.date: 2026-09-20
ms.topic: reference
---

## Research Scope

Determine the existing documentation for environments, configuration, accounts, and authentication; determine whether Copilot planning artifacts require wiki consultation; and identify minimal durable documentation or Copilot-artifact changes.

## Findings

### Environment and operator documentation

The wiki already documents the supported local environment in detail.

* `docs/wiki/local-development.md:3-27` defines Docker-backed AppHost local runtime, SQL Server, `platformdb`, Mailpit, and Keycloak with an imported realm. It also distinguishes persistent local development from the volume-free Keycloak lifecycle used by distributed authentication tests.
* `docs/wiki/local-development.md:112-126` distinguishes Aspire-managed SQL Server and Keycloak administration credentials from the seeded local operator account and gives the approved IG Demo credential entry path.
* `docs/wiki/local-development.md:126-151` documents protected IG credential storage, real Demo proof-data verification, and Test-environment Account Preferences boundaries.
* `docs/wiki/architecture.md:239-248` records the same local topology and states that API and Web receive SQL, SMTP, and authentication configuration through environment variables.
* `docs/wiki/operator-guide.md:255-330` documents the configuration UI, environment selection, restart-required semantics, write-only credentials, SQL-backed Data Protection keys, and the Test-platform guard that leaves the Live broker option disabled.
* `docs/wiki/application-overview.md:9-15` and `docs/wiki/application-overview.md:30-31` describe environment selection, operator-managed configuration, and SQL Server persistence as application responsibilities.
* `docs/wiki/api-reference.md:21-24` and `docs/wiki/api-reference.md:35-36` publish the configuration endpoint surface and its secret-safe response behavior.

The local-environment approach is therefore documented and consistent: use Aspire and Docker, run Keycloak locally, persist operator-managed configuration in SQL Server, and enter IG Demo credentials only through the secret-safe operator UI. `docs/wiki/runtime-behavior.md:301-303` also records Keycloak locally and Azure-aligned Microsoft Entra configuration. The reviewed wiki evidence does not establish a corresponding Azure deployment or operational procedure.

One stale statement needs correction: `docs/wiki/application-overview.md:30-32` still presents an in-memory persistence fallback as a current capability. That contradicts `docs/wiki/local-development.md:14-27` and `docs/wiki/architecture.md:243-248`, which establish Docker-backed SQL Server and state that in-memory SQL is not a supported application runtime. The fallback may still be valid test composition, but the overview must not present it as an application runtime alternative.

### Existing governance

The repository-level Copilot rule already requires Docker plus Aspire-orchestrated Keycloak locally and Microsoft Entra ID in Azure: `.github/copilot-instructions.md:19-20` and `.github/copilot-instructions.md:32-35`. It also makes `docs/wiki/` the implementation documentation source of truth and requires affected wiki updates before a numbered plan is complete: `.github/copilot-instructions.md:49-50`.

Scoped guidance reinforces the policy:

* `.github/instructions/auth.instructions.md:23-35` mandates local Keycloak, Azure Entra ID, standards-based OIDC/OAuth, and externally supplied identity configuration.
* `.github/instructions/configuration.instructions.md:23-38` mandates SQL Server for operator-managed configuration, write-only secrets, and Blazor-managed updates.
* `.github/instructions/docs.instructions.md:36-37` and `.github/instructions/work-packages.instructions.md:57-58` require affected wiki updates before a plan is marked complete.
* `.github/AGENTS.md:21-39` defines the customization context order and routes markdown-heavy wiki alignment to `documentation-specialist`.
* `.github/lessons.md:1-11` contains durable workflow lessons, but none require planning-time wiki consultation.

### Planning-time wiki consultation

The reusable plan generator includes wiki maintenance only as an output/completion obligation. `.github/prompts/generate-delivery-plan.prompt.md:58-61` requires wiki update tasks, and `.github/prompts/generate-delivery-plan.prompt.md:89-98` requires explicit wiki-update tasks while drafting. Its research inputs list requirements, business requirements, technical specification, and repository instructions, but do not require reading `docs/wiki/` before planning: `.github/prompts/generate-delivery-plan.prompt.md:73-88`.

The delivery-plan template contains an affected-wiki completion checkbox but no planning input or assessment record: `.github/templates/delivery-plan.template.md:78-102` and `.github/templates/delivery-plan.template.md:111-119`.

The work-package workflow recommends a research stage that inspects relevant documentation, then planning: `.github/work-package-workflow-usage.md:62-107`. It does not explicitly require the research or plan stages to consult the applicable wiki pages for environment, identity, configuration, or operator workflow changes. It also references three agents under `.github/agents/` at `.github/work-package-workflow-usage.md:36-47`, but the repository has no `.github/agents/` directory. This makes the documented routing unverifiable and is a separate governance-maintenance issue.

Existing plans demonstrate the intended outcome but not a universal planning gate. `docs/003-authentication-and-authorisation/plans/001-delivery-plan.md:16-22` names local Keycloak, Azure-compatible configuration, and required wiki updates. `docs/002-environment-and-auth-foundation/plans/001-delivery-plan.md:1-25` records the original SQL-backed environment and configuration approach. These plans are evidence that authors have performed the work manually, not evidence of a repeatable planner requirement.

## Minimal Durable Updates

1. Update `.github/instructions/work-packages.instructions.md` to require authors of numbered plans that affect environment, authentication, operator configuration, accounts, local development, runtime topology, or testing to consult the relevant `docs/wiki/` pages before drafting. Require the plan to name the assessed pages and whether each will change. This is the shared, enforceable policy location.
2. Update `.github/prompts/generate-delivery-plan.prompt.md` to add the applicable wiki pages as required planning inputs, add a process step to assess them before the first draft, and require a concise wiki-impact entry for every work item. This operationalizes the shared rule for the existing planner.
3. Update `.github/templates/delivery-plan.template.md` with a short `Wiki impact` input/assessment section and a per-work-item column or detail checklist that names the relevant pages and change outcome. This makes the planning evidence durable without duplicating the environmental policy.
4. Correct `docs/wiki/application-overview.md:32` so in-memory persistence is described as test-only, if it remains supported there, or remove the capability claim. This reconciles the overview with the local-runtime policy without changing product behavior.
5. Reconcile `.github/work-package-workflow-usage.md:36-47` with the absent `.github/agents/` directory. Either restore the named agents or revise the workflow to reference the available prompt-based workflow. Do not add the wiki consultation rule only to an absent agent.
6. Do not add a second environment policy to `.github/copilot-instructions.md`, `.github/AGENTS.md`, or `.github/lessons.md`. Existing rules already define the approach. Add a concise lesson only if the planning-time wiki assessment becomes a stable operational rule not adequately captured by the scoped instruction.
7. Defer a new Azure deployment wiki page until a delivery task needs Azure operator or deployment guidance. The current gap is discoverable, but this research does not establish the required Azure topology, configuration source, or operations procedure needed to author accurate guidance.

## Clarifying Questions

* Should the durable planning gate apply to every numbered plan, or only to plans that change environment, authentication, configuration, accounts, runtime topology, operator workflows, local development, or testing? The focused scope is the smaller, more proportional default.
* Is there an approved Azure deployment design that should be documented alongside the current local-environment approach?
* Are the missing `.github/agents/` artifacts intentionally supplied externally, or is `.github/work-package-workflow-usage.md` stale?

## Status

Complete. The environment approach, account/configuration/auth documentation, planning-governance gap, and smallest durable remediation set have been verified. No product files were modified.