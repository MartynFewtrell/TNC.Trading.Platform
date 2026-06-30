---
agent: 'Work Package Reviewer'
description: 'Reviews a delivered or in-progress work package for conformance to its requirements, technical specification, plan, instructions, and related wiki updates.'
name: review-work-package-delivery
model: 'gpt-5.4'
# tags: [work-package, review, quality]
---

# Review a Work Package Delivery

## Purpose

Use the dedicated `Work Package Reviewer` agent to perform a general post-delivery review for a work package.

This stage checks whether the delivered implementation matches the documented requirements, technical specification, delivery plan, repository instructions, and relevant wiki updates. It is intentionally broader than a test review and narrower than a full redesign critique.

## When to use

- You want an overall delivery-conformance review after implementation or before declaring a work package complete.
- You want a review artifact that checks the package docs, implementation evidence, validation evidence, and wiki alignment together.
- You want a general review before entering the specialist test and refactoring review loops.

## Inputs

### Required

- Target work-package folder under `./docs/00x-work/`.

### Optional

- Specific work items, files, or validation commands to emphasize.
- Preferred review depth (`quick`, `standard`, or `deep`).
- A preferred output path if it does not overwrite an existing report.

## Configuration variables (optional)

${REVIEW_DEPTH="standard"} <!-- quick | standard | deep: controls how much detail to include in the delivery review -->

## Constraints

- MUST: Route the task through the `Work Package Reviewer` agent.
- MUST: Use `.github/templates/work-package-delivery-review.template.md` as the output scaffold.
- MUST: Review the target work-package `requirements.md`, `technical-specification.md`, and relevant numbered plan files.
- MUST: Create a physical numbered review report in the target work-package folder before returning the final answer.
- MUST: Keep this review focused on overall delivery conformance.
- SHOULD: Position this review before `review-test-approach` and `review-refactoring-approach` when a general package-level validation pass is needed.
- MUST NOT: Treat this review as a replacement for the specialist testing or refactoring review flows.
- MUST NOT: Start implementation changes.

## Process

1. Route the task through the `Work Package Reviewer` agent.
2. Read the target package artifacts and the minimum supporting repository context needed for review.
3. Assess implementation, validation evidence, documentation alignment, and instruction conformance.
4. Write a numbered delivery review report to the target work-package folder.
5. Return a concise summary with the top findings and suggested next step.

## Output format

Return a single markdown report that follows `.github/templates/work-package-delivery-review.template.md`.

Also create the physical numbered review report inside the target work-package folder.

## Examples (optional)

### Example request

Review the delivered work package in `./docs/006-refactor-app/` before moving into specialist hardening reviews.

### Example response (optional)

A numbered delivery review report that summarizes conformance strengths, gaps, validation evidence, and recommended follow-up.