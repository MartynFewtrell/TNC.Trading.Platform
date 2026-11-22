---
title: Trailing Stops Final Synthesis Review
description: Final quality review of the trailing-stops post-implementation failure research
author: GitHub Copilot
ms.date: 2026-08-30
ms.topic: troubleshooting
---

## Research scope

Review the primary trailing-stops post-implementation failures report against all
same-day trailing-stops subagent reports. Assess verified startup signature,
bounded no-hang test diagnosis, root and cascade separation, evaluated alternatives,
migration safety, provider correctness, exact evidence, bounded validation, and
explicit evidence limits.

## Verdict

Needs targeted correction before it is treated as the final quality record. The
primary report is technically coherent and captures every critical defect found
by the four subagent investigations. It correctly identifies the reproduced
startup signature, bounded fixture-wait cascade, independent date-drift failure,
migration hazard, and provider correctness defects. No missing critical finding
was found.

The report is not fully auditable as written. It overstates what the unmodified
startup evidence can prove, gives indirect rather than direct path-and-line
evidence for most correctness findings, and specifies a watchdog example that
does not satisfy its own child-diagnostic requirement. A migration gate also
needs a narrower blocking scope. Two chronology statements in the remediation
alternatives report are stale.

## Criteria assessment

1. Verified startup signature passes. The exception and route-construction
	context at
	`.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md:95-108`
	agree with the direct reproduction and current route and registration source.
2. Bounded no-hang diagnosis passes. The report distinguishes a fast API exit,
	bounded fixture polling, five-minute amplification paths, and a separate
	structurally unbounded SQL risk at the primary report's lines 110-121. It
	does not claim that an individual test body or SQL operation was observed
	hanging.
3. Root, cascade, and pre-existing separation passes. Missing API composition is
	the root; Web, fixture, lease, and retention cancellation effects are cascades;
	the account-details date drift is explicitly independent and pre-existing.
4. Alternatives and one selected approach pass. The primary report evaluates
	rejected alternatives for startup, migration, and provider behavior and
	selects one staged conservative repair.
5. The migration safety decision gate passes in substance but needs a sequencing
	clarification. Unknown non-disposable deployment history correctly defaults
	to preserving `20260830112227_SnapshotRepair`.
6. Provider correctness passes. The report covers the wrong wire member,
	mismatching confirmation, uncertain malformed acknowledgements, telemetry,
	partitioning, cursor validation, environment policy, presentation, retention,
	and UI cancellation.
7. Exact path-and-line evidence partially passes. Startup and migration roots
	are directly cited, but the consolidated correctness inventory at the primary
	report's lines 139-148 has no direct source references.
8. Actionable bounded validation partially passes. The ordered stop-on-failure
	gates and 55-second test watchdog are useful, but the sample does not capture
	child output even though line 206 requires preserved child diagnostics.
9. Explicit evidence limits pass. Lines 335-341 clearly reserve the missing TRX,
	reconstructed 15-test membership, unproved SQL hang, and unverified real IG
	behavior.

## Findings

### High: startup restoration is stated with unsupported certainty

The primary report states at line 198 that the missing registrations are all
that is required to restore production startup in the reproduced database
state. No registration repair or post-fix startup was performed. The evidence
proves the first deterministic startup blocker, not that no later blocker will
appear after endpoint construction succeeds.

Current source confirms the initiating defect at
`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-60`,
`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:130-146`,
and
`src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:18-46`.
The retained direct run confirms only execution through SQL initialization and
failure at endpoint construction.

### High: the correctness inventory lacks direct evidence

The primary report's lines 139-148 summarize release-blocking and medium-risk
defects but require readers to search broad subagent ranges for proof. Add direct
references beside each claim:

* Wire contract and malformed acknowledgement:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-17`
  and the same file's lines 78-99
* Confirmation mismatch and update telemetry:
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:20-38`
* GET telemetry bypass:
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:22-41`
* Global history and permissive cursor fallback:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-50`
* Broker-only environment guard:
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:5-10`
* Legacy environment presentation:
  `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:9-13`
* Inert retention configuration:
  `src/TNC.Trading.Platform.Api/appsettings.json:32-37` and
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:16-21`
  plus the same processor's lines 52-55 and 86-89
* Uncancelled page operations:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:27-35`

The independent account-details finding should also cite
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:96-120`
and the same file's lines 145-163 directly, rather than only citing another
research report.

### Medium: the migration gate can unnecessarily block startup restoration

Stage 0 at the primary report's lines 272-274 appears to precede and block Stage
1. Migration deployment inventory is necessary before destructive migration
cleanup, but it is not needed to add the four missing registrations or prove the
route graph. The reproduced database already passed `MigrateAsync`.

Make Stage 0 a parallel decision track that gates Stage 3 only. Preserve
`SnapshotRepair` by default if the inventory is incomplete. This keeps the safe
migration decision without delaying the independent startup repair.

### Medium: the watchdog does not preserve failure diagnostics

The validation contract requires preserved child-process diagnostics at line
206, while the example beginning at line 318 uses `Start-Process -NoNewWindow`
without redirecting standard output or standard error. The wrapper also needs to
be explicitly applied to builds and API startup, not only the example test.

Use separate temporary output and error files, redirect both streams, include
their bounded tails in timeout and nonzero-exit errors, and delete them in
`finally`. Require both `--blame-hang` and `--blame-hang-timeout 45s`; the timeout
option alone does not enable hang-dump collection. Keep complete process-tree
termination as the external authority.

### Low: two supporting-report statements are stale

The remediation alternatives report says the primary report is still a pending
scaffold at
`.copilot-tracking/research/subagents/2026-08-30/trailing-stops-remediation-alternatives-research.md:27`.
It also says the build/test report is incomplete at the same file's line 290.
The primary and build/test reports are now complete. These are chronology errors,
not contradictions in the technical synthesis.

Mark both statements as point-in-time observations or replace them with current
status. Retain the valid limitation that the original 15-test TRX and full
exception inventory were not preserved.

## Exact corrections required

1. Replace the first sentence at the primary report's line 198 with: "The four
	missing API registrations are the only confirmed initiating production
	blocker in the reproduced database state; post-fix startup remains unverified
	and may expose a later blocker."
2. Change the next sentence at line 198 to separate necessity from hardening:
	registration repair unblocks the route graph; fail-fast fixture behavior is
	required before broad distributed validation can provide bounded diagnostics.
3. Add the direct source references listed above to every correctness bullet at
	lines 139-148 and to the independent account-details finding.
4. State under Stage 0 that migration inventory blocks Stage 3 migration edits,
	not Stage 1 composition repair or its focused route test.
5. Replace the watchdog sample with a reusable process-tree wrapper that captures
	standard output and error, reports bounded tails, and wraps every build,
	startup, and test gate. Require `--blame-hang` with its timeout.
6. Correct or time-qualify the two stale status statements in the remediation
	alternatives report at lines 27 and 290.

## Evidence limits

No tests, application startup, database operation, or production/test file
change was performed for this review. Conclusions use the retained runtime
evidence, all four same-day trailing-stops subagent reports, current source, and
the previously accepted provider-contract research.

The missing August 30 TRX prevents exact proof of all 15 failed test names. The
15-member real-authentication class remains a high-confidence reconstruction.
No evidence proves an August 30 SQL hang, a successful post-registration startup,
deployment scope for `SnapshotRepair`, or real IG Test endpoint behavior.

## Recommended next research

* [ ] Inventory `SnapshotRepair` in non-disposable databases before Stage 3
* [ ] Capture a bounded post-fix startup result and exact-filter TRX during implementation validation
* [ ] Obtain API-contract approval for the proposed HTTP 409 mismatch payload
* [ ] Capture sanitized manual IG Test evidence without adding a real-provider automated test

## Clarifying questions

No question blocks this quality verdict. Database owners must answer whether
`SnapshotRepair` exists outside disposable local state before migration cleanup,
and API owners must approve the exact mismatch Problem Details contract before
that public behavior is implemented.