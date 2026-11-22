---
title: Recommendation Source Research
description: Research into the source referenced by an incomplete request to implement recommended changes
ms.date: 2026-08-30
ms.topic: reference
---

## Research question

Determine the most likely document or issue referenced by the incomplete request:
"Create a plan to impliment the changes recomended in #".

The investigation covers recent and relevant review reports, recommendation documents,
git context, and existing `.copilot-tracking` research and plans. It must not assume that
the token-thrift review or alignment plan is the intended source.

## Working hypothesis

The trailing `#` is an incomplete issue, pull request, file, or editor-context reference
that may be recoverable from repository text or recent git history. An exact search for
the request wording and literal dangling references is the cheapest check of that hypothesis.

## Candidate sources

### 1. Account Details automatic de-duplication failure research

Confidence: medium-high as a contextual inference, but insufficient to execute
without confirmation.

The strongest candidate is the current-day research set led by
`.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md`.
It is the only recent recommendation artifact that is both planning-ready and
not already paired with a plan. Its scope selects one focused fix after
evaluating alternatives (lines 14-19), confines the recommended change to one
unit-test file (line 148), and records the selected recommendation at lines
199-205.

Two same-day delegated reports reinforce that lifecycle:

* `.copilot-tracking/research/subagents/2026-08-30/account-details-test-failure-evidence.md:342-353`
	recommends a fixed test clock and identifies fake-store trigger fidelity as a
	separate focused correction
* `.copilot-tracking/research/subagents/2026-08-30/account-details-test-fix-alternatives.md:137-151`
	selects the fixed-clock option, includes trigger-source fidelity, and limits
	the blast radius to one unit-test file

The current branch is `012-add-account-details`, three commits ahead of
`origin/main`, and the visible active terminal targets
`TNC.Trading.Platform.Application.UnitTests.csproj`. Those session signals
match the failing Account Details test research. No
`.copilot-tracking/plans/2026-08-30/` directory or corresponding plan was found.

### 2. Web UI navigation-order research

Confidence: low.

`.copilot-tracking/research/2026-08-17/web-ui-navigation-order-research.md`
identifies the exact implementation location and recommends focused tests at
lines 100-129, then gives actionable next steps at lines 138-142. It has no
matching named plan in `.copilot-tracking/plans/2026-08-17/`.

This candidate is weakened by git chronology. Commit `5617c48`, dated
2026-08-17 and titled `Updated order on Navigation`, is the branch tip. The
recommended reorder appears to have been implemented already, so creating a
new implementation plan would likely be stale or limited to missing ordered
navigation tests.

### 3. GitHub token-thrift review

Confidence: very low.

`docs/github-token-thrift-review-2026-06-06.md:97-103` summarizes the gaps and
lists four highest-leverage recommended changes. This document is surfaced in
the workspace summary and uses the same recommendation-to-plan vocabulary as
the incomplete request.

It is not a likely unplanned source. The adjacent
`docs/github-token-thrift-alignment-plan-2026-06-06.md:1-19` explicitly says it
translates that review into an implementation plan. The plan covers the review
through five workstreams and a sequenced plan at lines 39-125. Later repository
files, including `.github/instructions/token-discipline.instructions.md`,
`.github/AGENTS.md`, and `.github/lessons.md`, show that much of the plan was
subsequently implemented.

### 4. Historical work-package test and refactoring reviews

Confidence: very low.

The repository contains review reports under `docs/002-environment-and-auth-foundation/`,
`docs/003-authentication-and-authorisation/`, and
`docs/005-refactor-app-host/`. Their recommendations already have corresponding
numbered mitigation plans under each work package's `plans/` directory. The
current branch, current-day research, and active test project do not point to
those older reviews.

## Literal reference resolution

The dangling `#` cannot be resolved from the available repository or editor
context.

* An exact repository search found no occurrence of the misspelled request
	text, `Create a plan to impliment` or `changes recomended`.
* A repository-wide search for lines ending in a literal `#` found only six
	comment-only lines in generated Playwright `protocol.yml` files under test
	build output. They are unrelated to issues, pull requests, or documents.
* The current Copilot transcript records the parent agent saying, "the
	reference after `#` is missing," at
	`c:/Users/martynfewtrell/AppData/Roaming/Code/User/workspaceStorage/d380cd56fde34a0ec47784a74216e247/GitHub.copilot-chat/transcripts/e23f989e-ad75-430f-943d-34d49c84389c.jsonl:2`.
* The delegated request is recorded with `"attachments":[]` in the same
	transcript at line 9. No file, selection, issue, pull request, or symbol was
	attached to restore the intended suffix.
* Git history contains merged pull requests through `#21` and the current
	branch `012-add-account-details`, but there is no evidence that the bare hash
	meant pull request `#22`, issue `#12`, or any other inferred number. Branch
	numbering is not a reliable substitute for the missing reference.

In VS Code chat, `#` can begin a context reference such as a file or selection.
The empty attachment array is consistent with the user having submitted before
choosing that reference, but it does not identify what they intended to choose.

## Implied task scope

### Candidate 1 scope

Create a narrow test-fix plan for
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs`.
The plan would inject a fixed `TimeProvider`, derive the seeded trading day from
that instant, seed an `Automatic` snapshot, align the in-memory existence
predicate with the production EF predicate, run the fully qualified failing
test, and then run the Application unit-test project. Product code, API,
schema, and wiki behavior are out of scope.

### Candidate 2 scope

Plan a Web-only navigation reorder in
`src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` and add
ordered-link assertions in `MainLayoutTests.cs`. Because the reorder commit
already exists, the remaining credible scope is test hardening and verification
rather than implementing the requested order.

### Candidate 3 scope

Plan broad repository-customization changes under `.github/`: token-discipline
rules, stable-learning files, execution discipline, prompt and skill alignment,
and model-tier guidance. This would duplicate the existing alignment plan and
risks planning work already implemented.

### Candidate 4 scope

Plan test or refactoring mitigation for one historical work package. The
specific report would still need to be named, and existing numbered plans must
be checked before any new artifact is created.

## Recommendation

Clarification is required before creating an implementation plan. The available
context supports a leading candidate but does not recover the user's intended
reference, and the difference between candidates ranges from one test file to
the entire `.github` customization surface.

Ask the user to provide the missing `#` reference or confirm this provisional
interpretation:

> Did you mean the recommendations in
> `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md`?

If the user confirms, use that consolidated research as the primary source and
the two same-day subagent reports as supporting evidence. Do not generate a new
token-thrift plan unless the user explicitly selects the June review, because
`docs/github-token-thrift-alignment-plan-2026-06-06.md` already fulfills that
request.

## Evidence

* `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md:14-19`
	defines the current failure-research scope and requires one selected fix
* `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md:40-58`
	identifies the fixed-date/system-clock mismatch and repository clock pattern
* `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md:148-148`
	states that no API or schema change is needed and limits the fix to one test
	file
* `.copilot-tracking/research/2026-08-30/account-details-automatic-deduplication-failure-research.md:199-205`
	selects the deterministic fixed-clock and fake-store fidelity correction
* `.copilot-tracking/research/subagents/2026-08-30/account-details-test-failure-evidence.md:19-19`
	states the falsifiable date-drift hypothesis
* `.copilot-tracking/research/subagents/2026-08-30/account-details-test-failure-evidence.md:342-362`
	records the recommendation, limits, and absence of implementation blockers
* `.copilot-tracking/research/subagents/2026-08-30/account-details-test-fix-alternatives.md:137-151`
	selects the one-file test-only implementation approach
* `.copilot-tracking/research/2026-08-17/web-ui-navigation-order-research.md:100-129`
	recommends the navigation implementation location and focused tests
* `.copilot-tracking/research/2026-08-17/web-ui-navigation-order-research.md:138-142`
	records its actionable next steps
* `docs/github-token-thrift-review-2026-06-06.md:97-103` records the review's
	recommendation list
* `docs/github-token-thrift-alignment-plan-2026-06-06.md:1-19` explicitly
	identifies itself as the implementation plan for that review
* `docs/github-token-thrift-alignment-plan-2026-06-06.md:39-125` defines the
	five workstreams and sequence, demonstrating that the review is not awaiting
	an initial plan
* Git branch evidence: `HEAD` and `origin/012-add-account-details` point to
	`5617c48`; `origin/main...HEAD` reports zero commits only on main and three
	commits only on the branch
* Git source evidence: commit `8863cd9` introduced the Account Details feature
	and failing date-sensitive test on 2026-08-07; commit `155d719` implemented
	the tabbed Account Details layout; commit `5617c48` updated navigation order

## Unresolved questions

* What file, issue, pull request, selection, or symbol was intended after `#`?
* If the Account Details failure research is intended, does the user want a
	standalone tracking plan or a numbered work-package plan under `docs/`?
* If an issue or pull request number was intended, is it local to
	`MartynFewtrell/TNC.Trading.Platform`, and what is its number?

## Recommended next research

* [ ] Obtain the missing context reference from the user
* [ ] If Candidate 1 is confirmed, inspect the applicable plan template and
	existing August tracking-plan conventions before drafting
* [ ] If a GitHub issue or pull request is supplied, inspect its body, comments,
	review findings, and linked commits before defining scope
* [ ] If the token-thrift review is confirmed, compare the existing alignment
	plan and current `.github` state to identify only unfinished work rather than
	creating a duplicate plan