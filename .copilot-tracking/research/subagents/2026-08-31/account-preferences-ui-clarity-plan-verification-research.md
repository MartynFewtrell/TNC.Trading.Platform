<!-- markdownlint-disable-file -->
# Account Preferences UI Clarity Plan Verification Research

## Research Topics

* Verify the current exact owning production, test, and wiki files with useful line anchors.
* Verify current build and test commands plus project conventions relevant to implementation.
* Determine whether matching plan, detail, or log artifacts already exist and should be extended.
* Identify conflicts between the current code and the selected delivery sequence.
* Identify exact repository commands that validate only or primarily
	`docs/wiki/operator-guide.md` and `docs/wiki/testing-and-quality.md`, with
	prerequisites and a fallback when no scoped Markdown command exists.

## Status

Complete on 2026-08-31. Research was limited to repository inspection and one
focused bUnit run. The two scoped wiki validation commands recommended below
were also executed successfully. No production, test, wiki, work-package, or
execution-log file was changed.

## Findings

The focused account-preferences baseline passed 10 of 10 tests.

## Primary Inputs

* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md`
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md`

Both inputs still describe the current pre-implementation UI. Their selected
four-file Web boundary is correct, subject to the delivery-sequence corrections
below.

## Owning Files and Anchors

### Production ownership

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:1-7`
	owns the route, Operator authorization, presenter injection, and navigation
	access check.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:9-25`
	owns all requested visible copy and interaction markup. Current anchors are
	the Test-specific introduction at line 15, error feedback at line 16,
	checkbox and save action at lines 17-20, combined status at line 21, and
	history section at lines 23-25.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:29-45`
	owns component-lifetime cancellation and load, save, and paging forwarding.
	A selection callback, dismiss callback, Save `ElementReference`, and focus
	restoration would be added here.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:3-9`
	owns mutable loading, saving, error, and transient message state.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:11-26`
	owns initial preference and history loading. A confirmed preference can be
	applied before history loading fails, but both failures currently collapse
	into `Unable to load account preferences.`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:28-44`
	owns save confirmation. Update and history refresh share one `try`, so a
	confirmed update followed by history failure leaves success plus the generic
	save error.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:47-53`
	owns history paging and does not require behavior changes for this task.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-9`
	owns one editable Boolean plus application and history state. It has no
	unconfirmed, pending, or last-confirmed distinction.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:11-22`
	applies authoritative preference data and history pages.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`
	does not exist. It is the one new production file in the selected boundary.
	The nearest scoped-CSS precedent is
	`src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css:1-59`.

The transport and application files are supporting evidence, not implementation
targets:

* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-166,183-185` already
	reads and writes non-nullable `bool TrailingStopsEnabled` and captures request
	JSON without a contract change.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:13-60`
	performs the PUT and authoritative GET. It returns success only when observed
	equals requested; mismatch becomes `NotApplied` and maps to HTTP 409.

### Test ownership

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:10-174`
	contains the 10 current bUnit tests. Useful method anchors are lines 17, 28,
	40, 54, 71, 95, 120, 140, 150, and 166.
* Checkbox-specific assertions are concentrated at lines 14-49. Pending-save
	coverage at lines 117-135 checks only the Save button, not the preference
	control. Load-failure coverage at lines 137-146 does not assert that an
	unconfirmed default editor is suppressed.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:20-135`
	already supplies in-memory bUnit rendering, loose JS interop, Radzen services,
	authentication, the API client, and the scoped presenter.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/SequencedHttpMessageHandler.cs:5-39`
	records HTTP method, URI, authorization, and serialized content. It can verify
	outbound `true` and `false` without another test helper.

No account-preferences functional or E2E test exists. A deterministic
server-side provider fixture remains a prerequisite for reliable Playwright CI
coverage; browser request interception cannot replace the Blazor Server API and
provider path.

### Wiki ownership

* `docs/wiki/operator-guide.md:565-580` describes a Test-account toggle and
	confirmed-value replacement. Extend this section with explicit
	Enabled/Disabled selection, pending versus confirmed state, dismissible
	confirmation, safe failed-load behavior, and qualified history-refresh
	feedback while preserving the Test-only safety boundary.
* `docs/wiki/testing-and-quality.md:553-566` lists the current bUnit coverage.
	Extend it after implementation with radio semantics, state transitions,
	outbound Boolean, dismissal, failure, partial-success, and load-safety cases.

The account-preferences sections in `docs/wiki/api-reference.md:551-586`,
`docs/wiki/application-overview.md:195-208`,
`docs/wiki/architecture.md:600-615`,
`docs/wiki/local-development.md:144-157`, and
`docs/wiki/runtime-behavior.md:728-748` describe unchanged Test-only authority,
Boolean contracts, provider confirmation, and layer ownership. No edit is
currently justified there.

## Commands and Conventions

The repository uses SDK `10.0.200` with `latestFeature` roll-forward from
`global.json:1-6`. The Web and Web unit-test projects target `net10.0`.
`src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:7-15` references
Radzen Blazor 10.3.2. The unit-test project references bUnit 1.40.0 and xUnit
2.9.3 at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:1-27`.

The cheapest implementation check is:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"
```

This session verified the equivalent file-scoped run: 10 passed, 0 failed.
After component work, the appropriate project gates are:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj
dotnet format src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --verify-no-changes --no-restore
dotnet format test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --verify-no-changes --no-restore
```

The existing repository-wide gate should be preferred for final evidence over
inventing another build/test wrapper:

```powershell
.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId account-preferences-ui-clarity
```

`.github/scripts/Invoke-CleanArchitectureQualityGate.ps1:1-92` builds and tests
`TNC.Trading.Platform.slnx`, writes transcript and TRX evidence under
`artifacts/quality-gates/<GateId>/<timestamp>/`, and fails on either command.
It requires the distributed-suite prerequisites documented at
`docs/wiki/testing-and-quality.md:424-453`, including Docker and Playwright.
If the documented intermittent MSBuild child-node failure occurs, record the
failed attempt and use the serialized fallback:

```powershell
dotnet test TNC.Trading.Platform.slnx -m:1
```

Relevant implementation conventions are:

* Keep presentation state and feedback in the Web inbound adapter and leave the
	application/provider confirmation policy unchanged.
* Prefer semantic HTML and built-in Blazor controls when they are sufficient;
	Radzen is preferred for shared controls but complex components are not
	mandatory.
* Keep every behavior edit small and rerun the focused class immediately.
* Name tests with `MethodName_StateUnderTest_ExpectedResult` and place an XML
	traceability comment immediately above every added or changed test. Do not
	add `Arrange/Act/Assert` comments.
* Use existing task or work-package requirement identifiers only when they
	genuinely apply. The account-preferences clarity task has no dedicated
	numbered requirements document, so comments can cite the source research
	task items rather than invent identifiers.
* Update affected wiki pages before implementation is considered complete.

## Existing Artifacts

Matching research artifacts already exist:

* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md`
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-repository-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-accessibility-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-alternatives-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md`

The last item is a duplicate delivery-plan research artifact with more complete
instruction and baseline evidence. Before implementation, reconcile its useful
corrections into the primary delivery-plan research named by this task and use
that primary file as the canonical plan. Do not maintain two independently
evolving delivery plans.

No matching implementation-detail artifact or execution log exists. No
account-preferences plan exists under `docs/*/plans/`. The completed
`docs/004-ui-update-and-refactor/plans/001-delivery-plan.md` did not prioritize
Account Preferences and must not be reopened. Repository rules also prohibit a
new numbered work package unless explicitly requested.

## Scoped Markdown Validation

### Exact recommendation

Run these commands from the repository root after changing the two affected
wiki pages:

```powershell
git diff --check -- docs/wiki/operator-guide.md docs/wiki/testing-and-quality.md
```

```powershell
$files = @('docs/wiki/operator-guide.md', 'docs/wiki/testing-and-quality.md'); $broken = foreach ($file in $files) { $base = Split-Path $file; $text = Get-Content -Raw $file; foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\((?<target>[^)\s]+)(?:\s+"[^"]*")?\)')) { $target = $match.Groups['target'].Value; if ($target -notmatch '^(?:[a-z][a-z0-9+.-]*:|#)') { $path = ($target -split '#', 2)[0]; if ($path -and -not (Test-Path (Join-Path $base ([uri]::UnescapeDataString($path))))) { "$file -> $target" } } } }; if ($broken) { $broken; exit 1 }
```

The first command is the cheapest repository-native, path-scoped check. It
fails for whitespace errors but does not validate Markdown structure or link
targets. The second command is the required scoped fallback for repository
relative links: it inspects only the two pages, resolves each local link or
image target relative to its containing page, prints broken targets, and exits
with code 1 when any target is missing. The current pages contain seven such
links: three in `operator-guide.md` and four in `testing-and-quality.md`.

Prerequisites are Git for the first command and Windows PowerShell 5.1 or
PowerShell 7 for the second. Both commands assume the current directory is the
repository root. Neither command requires Docker, the .NET SDK, Node.js, an
application build, or running services. The link check intentionally excludes
HTTP(S), other URI schemes, and same-page fragments; external URL and fragment
validity remain manual review items.

### Full Markdown lint fallback

No repository-owned script, task, package command, workflow, or checked-in
Markdown configuration provides scoped Markdown syntax linting. If a full
Markdown lint is required in addition to the repository-native checks, use this
external fallback:

```powershell
npx --yes markdownlint-cli2@latest "docs/wiki/operator-guide.md" "docs/wiki/testing-and-quality.md"
```

This fallback requires Node.js with `npm`/`npx` and either network access or a
cached `markdownlint-cli2` package. It is not reproducibly pinned by this
repository and, because no `.markdownlint` configuration is checked in, it
uses the package defaults. Record the resolved tool version with the evidence
if this fallback is used. Do not use the `npm run validate:frontmatter` or
`pwsh scripts/linting/Validate-MarkdownFrontmatter.ps1` examples from the
external Markdown authoring instructions: this repository has no
`package.json` and no `scripts/linting/` implementation for those commands.

### Evidence anchors

* `.github/instructions/docs.instructions.md:24-41` requires semantic heading
	structure, relative repository links, and continued wiki navigation and
	cross-link validity.
* `.github/instructions/docs.instructions.md:51-54` defines one H1, heading
	hierarchy, and internal-link resolution as documentation validation outcomes.
* `.github/prompts/execute-delivery.prompt.md:54-57` explicitly requires
	validating affected Markdown links when wiki pages change.
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1:1-92` is the only
	checked-in repository script. It builds and tests the solution and accepts
	only `GateId`; it has no Markdown or file-path validation mode.
* The repository root has no `package.json`, `.vscode/tasks.json`,
	`.markdownlint*`, or `scripts/linting/` path, and `.github/workflows/` is
	empty. Therefore no repository package, task, or CI command can be narrowed
	to these two Markdown files.
* `docs/wiki/operator-guide.md:561-563` contains three relative wiki links.
* `docs/wiki/testing-and-quality.md:375,549-551` contains four relative
	repository links.

## Delivery-Sequence Conflicts

### Differing HTTP 200 response

The primary delivery plan asks for a controlled HTTP 200 whose Boolean differs
from the pending selection. Current application and API semantics make that
response impossible in production: mismatch returns `NotApplied` and HTTP 409.
Retain a 409 test that preserves confirmed UI state. Remove the differing-200
case unless it is explicitly labelled as defensive deserialization behavior
outside the production contract.

### Shared error state

The plan says selection should clear prior save errors. The current presenter
has one `Error` property for initial preference failure, initial history
failure, save failure, and save-time history failure. Clearing `Error`
unconditionally on selection can erase a still-relevant load/history warning.
Phase 2 must first distinguish transient save feedback from load/history
feedback, or clear only the known save error. This correction belongs before
the component wires selection changes.

### Test traceability

The plan suggests `NF2`, `NF3`, and `NF7` from the completed UI work package.
Those identifiers may support general usability cases, but they are not the
source of every account-preferences state transition. New comments should cite
the current research task items and use existing requirement IDs only where the
test actually proves them.

### Focus verification boundary

bUnit can verify the dismiss control, message removal, and expected JS interop
invocation, but it cannot prove real browser focus placement or assistive
technology announcements. Keep focus restoration in Phase 3, but treat browser
focus and screen-reader behavior as manual completion gates rather than claiming
the focused unit suite proves them.

### Existing conflicts correctly addressed by the plan

The selected sequence correctly fixes these current-code conflicts:

* A failed initial preference load currently falls through to an editable
	default `false`; explicit confirmed state must precede rendering the editor.
* The current checkbox cannot display both Boolean choices explicitly.
* The fixed label cannot express the pending selected state.
* Application status and save confirmation currently replace each other in one
	always-present status paragraph.
* Confirmed update and secondary history refresh currently share one failure
	boundary and can produce contradictory success and error feedback.
* No scoped stylesheet exists for the selected segmented visual treatment.

## Recommended Sequence Adjustments

1. Keep the focused 10-test baseline as Phase 0.
2. Add explicit unconfirmed, pending, and confirmed view-model state in Phase 1.
3. In Phase 2, separate feedback ownership before implementing selection
	 clearing, dismissal, and confirmed-update/history-refresh partial success.
4. Test real mismatch as HTTP 409; omit the contract-impossible differing-200
	 scenario.
5. Implement semantic radios, copy, feedback separation, and focus interop in
	 Phase 3, then add scoped styling in Phase 4.
6. Update only the two affected wiki sections, run the Web gates, then use the
	 existing repository quality-gate script for final evidence when distributed
	 prerequisites are available.

## Clarifying Questions

No question blocks implementation planning. One product choice should remain
explicit in the canonical plan: unchanged saves are currently allowed and
create valid observations. The existing delivery research preserves that
behavior, which is the safest default unless the product owner requests a
change.

## Next Research Recommendations

* [ ] Reconcile the duplicate delivery-plan research files and designate the
	primary research-folder plan as canonical.
* [ ] After implementation, inspect the rendered selector and confirmation at
	desktop width, 320 CSS pixels, and 400 percent zoom.
* [ ] After implementation, verify keyboard operation, focus restoration, and
	announcements with NVDA plus Firefox or Chrome and Narrator plus Edge.
* [ ] Research a deterministic AppHost provider fixture only if automated
	browser coverage becomes a separate requirement.