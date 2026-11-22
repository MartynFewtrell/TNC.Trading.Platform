<!-- markdownlint-disable-file -->
# Task Research: Project Build Failure

Determine why the current TNC.Trading.Platform project does not build and identify one evidence-backed remediation path.

## Task Implementation Requests

* Reproduce the current build failure using the repository's declared SDK and solution entry point.
* Trace the first meaningful failure to its owning project or configuration boundary.
* Evaluate remediation options and select the smallest root-cause fix.

## Scope and Success Criteria

* Scope: Build configuration, SDK/tooling selection, project references, source compilation, and directly related validation. Exclude unrelated cleanup and feature changes.
* Assumptions: The working tree may contain user changes; do not revert or overwrite them. The current date is 2026-08-06.
* Success Criteria:
  * The failure is reproduced with command, exit status, and first actionable diagnostics.
  * The controlling cause is supported by file-and-line evidence where available.
  * A recommended remediation and focused validation command are documented.

## Outline

1. Reproduction and environment evidence
2. Repository and project graph findings
3. Root-cause analysis
4. Evaluated alternatives
5. Selected approach and implementation impact

## Potential Next Research

* After the duplicate is corrected, rerun Web, AppHost, and solution builds to expose any independent errors.
  * Reasoning: The current investigation stopped at the first actionable compiler failure.
  * Reference: `src/TNC.Trading.Platform.Web/Components/Pages/Administration.razor` and nested Administration page.
* Review package vulnerability warnings separately.
  * Reasoning: The warnings are not the immediate build blocker.

## Research Executed

### File Analysis

* `global.json:2-5`
  * Pins SDK `10.0.200` with `latestFeature` roll-forward. The build selected an available SDK and lower-level projects compiled, so SDK absence is not the immediate cause.
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration.razor`
  * Exists in the current worktree but contains no source lines. It remains a Razor input despite being empty.
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor:1-2,7-70`
  * Contains the full Administration component, declares namespace `TNC.Trading.Platform.Web.Components.Pages`, and uses route `/administration/authentication`.
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-19`
  * The Web project targets `net10.0` and references Application and ServiceDefaults. Its focused build reproduces the solution's blocking error.
* `TNC.Trading.Platform.slnx:1-41`
  * Includes six source projects and ten test projects. The solution is not the controlling failure because the Web project fails independently.

### Code Search Results

* `Administration.razor`
  * Two same-named Razor component paths are present under `src/TNC.Trading.Platform.Web/Components/Pages/`.
* `CS0111` / `BuildRenderTree`
  * The Web build reports one blocking error in generated Razor output: `Type 'Administration' already defines a member called 'BuildRenderTree' with the same parameter types`.
* Project references
  * The production project graph is structurally valid. Application builds independently; AppHost and dependent tests expose the Web compilation failure downstream.

### External Research

* No external research was required. The failure is directly reproducible from repository source and generated Razor diagnostics.

### Project Conventions

* Standards referenced: `.github/copilot-instructions.md`, `.github/instructions/architecture.instructions.md`, `.github/instructions/aspire.instructions.md`, `.github/instructions/dotnet-stack.instructions.md`, and `.github/instructions/tests.instructions.md`.
* Instructions followed: research-only changes were confined to `.copilot-tracking/research/`; existing working-tree changes were not reverted.

## Key Discoveries

### Project Structure

The solution contains six source projects and ten test projects. The relevant ownership boundary is the Web Razor project; AppHost and several tests depend on it, but the duplicate component is generated within Web.

### Implementation Patterns

The .NET Web SDK discovers both `.razor` files. Razor derives the component type from the file name and namespace, so an empty stale file still collides with a same-named component in a nested directory.

### Complete Examples

```powershell
dotnet build .\TNC.Trading.Platform.slnx --nologo --verbosity:minimal
dotnet build .\src\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.csproj --nologo --verbosity:minimal
```

### API and Schema Documentation

Not applicable to the immediate compile failure.

### Configuration Examples

```text
No build configuration change is required for the immediate failure.
```

## Technical Scenarios

### Build Reproduction and Root-Cause Remediation

The solution build exits with code `1` and reports one error plus 210 warnings. The Web project independently reproduces the same error, while the Application project builds successfully.

**Requirements:**

* Leave exactly one Razor input that generates the `Administration` component in namespace `TNC.Trading.Platform.Web.Components.Pages`.
* Preserve the intended route and page implementation.
* Rebuild Web first, then AppHost and the solution, before triaging separate package advisories.

**Preferred Approach:**

* Complete the apparent page move by retaining `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor` and removing the stale empty `src/TNC.Trading.Platform.Web/Components/Pages/Administration.razor`, after confirming the nested path is the intended final location. This is the smallest change that removes the duplicate generated type while preserving the full current page.

```text
src/TNC.Trading.Platform.Web/Components/Pages/
|- Administration/Administration.razor  # retain: full component and route
`- Administration.razor                 # remove: stale empty Razor input
```

**Implementation Details:**

The first actionable diagnostic is:

```text
src\TNC.Trading.Platform.Web\obj\Debug\net10.0\Microsoft.CodeAnalysis.Razor.Compiler\Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator\Components\Pages\Administration\Administration_razor.g.cs(133,33): error CS0111: Type 'Administration' already defines a member called 'BuildRenderTree' with the same parameter types
```

The generated `obj` file is only the symptom location. The source cause is the pair of same-named Razor files. After the intended path correction, run:

```powershell
dotnet build '.\src\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.csproj' --nologo --verbosity:minimal --no-restore
dotnet build '.\src\TNC.Trading.Platform.AppHost\TNC.Trading.Platform.AppHost.csproj' --nologo --verbosity:minimal --no-restore
dotnet build '.\TNC.Trading.Platform.slnx' --nologo --verbosity:minimal --no-restore
```

Expected outcome: the Web build no longer reports `CS0111`; AppHost and the solution can then reveal any independent errors. The build currently also emits package vulnerability and nullable warnings, but these are not the cause of the failed compilation.

#### Considered Alternatives

* Retain the top-level page and remove or rename the nested page. This is valid only if the current page reorganization is not intended; it risks discarding the full component currently present in the nested file.
* Change SDK or package versions. Rejected as the immediate fix because the pinned SDK is available, the Application project builds, and the failure is a deterministic generated Razor duplicate.
* Repair project references or test configuration. Rejected because the Web project fails independently and no unresolved-reference diagnostic appears.

## Evidence Log

* `dotnet --version` returned `10.0.200`, matching `global.json`.
* `dotnet build .\TNC.Trading.Platform.slnx --nologo --verbosity:minimal` exited `1`; the first actionable error was `CS0111` for generated `Administration_razor.g.cs`.
* `dotnet build .\src\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.csproj --nologo --verbosity:minimal` independently exited `1` with the same error.
* Focused Application build succeeded, ruling out a universal SDK or restore failure.
* `git status --short` showed an in-progress page reorganization: the tracked top-level Administration file is modified and the nested Administration directory is untracked. No product files were changed during research.
