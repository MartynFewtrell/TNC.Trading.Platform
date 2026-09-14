# Research Brief: 007-build-errors

Status: In progress

Revision: 1 (initial research)

Lifecycle folder: `docs/007-build-errors/`

Canonical artifact path: `docs/007-build-errors/001-research-brief.md`

Provenance: Developer-reported build state in the active chat (one error and 272 warnings); active IDE diagnostics; archived quality-gate logs; and Microsoft Learn documentation. No canonical lifecycle input record was provided.

## Question And Scope

Research the reported solution build state of one error and 272 warnings and recommend an evidence-based sequence to produce zero errors and zero warnings across `TNC.Trading.Platform.slnx`.

The scope includes build-process state, SDK selection, package restore audits, compiler and obsolete-API warnings, and analyzer warnings. It excludes implementation, package changes, broad warning suppression, and diagnostic-policy changes.

## Success Criteria

- Repeatable Debug and Release builds of `TNC.Trading.Platform.slnx` use the SDK selected through `global.json` and report zero errors and zero warnings.
- Affected automated tests pass after every remediation batch.
- No broad suppression, disabled analyzer category, or relaxed warning policy conceals unresolved security, correctness, or compatibility issues.

## Cited Evidence

### Verified Facts

- The repository requests SDK `10.0.200` with `latestFeature` roll-forward in [global.json](../../global.json#L1-L5).
- The solution defines six production projects and ten test projects in [TNC.Trading.Platform.slnx](../../TNC.Trading.Platform.slnx#L1-L31).
- The existing quality gate builds `TNC.Trading.Platform.slnx`, stops when the build fails, and only then runs solution tests with TRX results in a timestamped `artifacts/quality-gates/<GateId>/<timestamp>/` evidence folder: [Invoke-CleanArchitectureQualityGate.ps1](../../.github/scripts/Invoke-CleanArchitectureQualityGate.ps1#L9-L61).
- An archived gate recorded `MSB3026` retries while `TNC.Trading.Platform.Api` and `TNC.Trading.Platform.Web` processes locked `TNC.Trading.Platform.ServiceDefaults.dll`: [phase-11 failure log](../../artifacts/quality-gates/phase-11-final/20260728-133222/quality-gate.log#L236-L255).
- The archived successful gate begins with repeated `NU1902`, `NU1903`, and `NU1904` package-vulnerability audit warnings for `AngleSharp`, `Microsoft.AspNetCore.DataProtection`, `Microsoft.OpenApi`, `MessagePack`, and `System.Security.Cryptography.Xml`: [phase-11 successful log](../../artifacts/quality-gates/phase-11-final/20260728-133409/quality-gate.log#L17-L52).
- The supported local build command is `dotnet build`: [local development guide](../wiki/local-development.md#L87-L93).
- Active editor diagnostics were empty during this Research pass.

### Unverified Input

- The developer reported one error and 272 warnings. No accessible current build transcript corroborates the exact error, warning count, SDK resolution, or diagnostic composition.

### External References

- [global.json overview](https://learn.microsoft.com/dotnet/core/tools/global-json) documents SDK resolution and `latestFeature` roll-forward behavior.
- [MSBuild properties for Microsoft.NET.Sdk](https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props) documents analyzer and warning controls, including `AnalysisLevel`, `EnforceCodeStyleInBuild`, `NoWarn`, and `TreatWarningsAsErrors`.
- [NuGet PackageReference guidance](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files) documents warning controls and dependency-management mechanisms.
- [Obtaining MSBuild logs](https://learn.microsoft.com/visualstudio/msbuild/obtaining-build-logs-with-msbuild) documents detailed and binary build logs for diagnostic evidence.

## Existing Patterns

- The quality-gate script is the existing solution-wide validation route and preserves readable transcript and TRX evidence beneath `artifacts/quality-gates/`.
- Historical output-lock diagnostics identify live Web and API processes as a possible build-environment failure class, not evidence of a current source compilation defect.
- Historical NuGet audit diagnostics repeat across projects, indicating that remediation should be grouped by direct dependency root rather than warning occurrence.

## Assumptions

- The reported total came from a solution-level build rather than an editor-only diagnostic view.
- The existing quality gate is the final acceptance mechanism after targeted build and test validation.
- Archived logs are diagnostic clues only and do not establish the current failure's cause.

## Options And Trade-Offs

1. Capture a fresh baseline, then remediate by diagnostic cause. This establishes the actual error and warning taxonomy before changes, at the cost of one initial evidence-capture pass. This is the recommended option.
2. Upgrade packages identified in archived NuGet audit output immediately. This may reduce repeated security warnings quickly but risks missing current warning families and changing Aspire, Keycloak, Playwright, or test dependency graphs without current evidence.
3. Suppress warnings or loosen policy. This can reduce the displayed count but fails the success criteria and can retain security, correctness, or compatibility defects. It is not recommended.
4. Tighten SDK or analyzer policy before remediation. This may improve reproducibility but mixes policy churn with root-cause fixes. Defer it until the baseline shows whether SDK selection changes diagnostics.

## Risks And Constraints

- `NU1902`, `NU1903`, and `NU1904` must not be suppressed merely to obtain a clean build because they represent vulnerability-audit findings.
- Package upgrades can affect runtime behavior and test behavior, especially in Aspire, Keycloak, and test-tooling dependency graphs.
- Live-process output locks are conditional build-environment issues. They must be checked only when the fresh baseline reports `MSB3026`, `MSB3021`, or `MSB3027` copy diagnostics.
- The current error's ID, project, target framework, phase, and ownership cannot be determined without a current build record.
- This Research environment did not expose command execution, and the shared RPIR lifecycle-core and agent-question-resolution skills were not accessible from the workspace. The brief therefore records the exact runtime limitation rather than treating unavailable evidence capture or skill consultation as completed work.

## Recommendation

1. Produce a fresh Debug baseline for `TNC.Trading.Platform.slnx` using the existing quality gate. Retain its readable transcript and add an MSBuild binary log under the generated `artifacts/quality-gates/007-build-errors/<timestamp>/` evidence folder. Record the resolved SDK, configuration, restore sources, exact error, and every warning grouped by diagnostic ID, project, package/version, and build phase.
2. Resolve the exact error before warning work. When the fresh output reports copy-lock diagnostics, stop the named output-holding process and rerun the build; otherwise assign the error to its owning source, project, or dependency based on the diagnostic's first causally relevant location.
3. Remediate warnings in causal batches: critical `NU1904`, high `NU1903`, and moderate `NU1902` findings by direct dependency root; then confirmed package-pruning warnings; then compiler and obsolete-API warnings by owning project; then analyzer/style diagnostics by rule family.
4. After each dependency or source batch, run focused owning-project tests. Preserve Docker-backed Aspire and Keycloak coverage when AppHost, API, Web, authentication, or integration behavior is affected.
5. Complete with clean Debug and Release solution builds and the full quality gate. Compare diagnostic IDs and counts to the fresh baseline after each batch.
6. Consider a tighter SDK/analyzer reproducibility policy only after functional remediation is complete and the measured baseline demonstrates SDK-induced diagnostic variation.

## Stage Assurance Reconciliation

Stage Assurance found the research direction appropriate but required bounded citations, separation of verified facts from the unverified developer report, removal of broad-suppression recommendations, and conditional treatment of historical output locks. Those corrections are incorporated in this revision.

## Manual Handoff

This record captures Research evidence only. It is not approval or Plan authority and does not authorize or automatically submit a Plan handoff. The developer must approve or amend this exact canonical in-progress brief before valid explicit `/plan` admission can prospectively close the Research stage.