# Project Graph Research

- Date: 2026-08-06
- Scope: Build graph and configuration relevant to the reported build failure.
- Constraint: Research only; no product files changed. This document is the only permitted write path.

## Questions

- What SDK and workload/tooling versions are selected?
- What projects are in the solution and how do project references flow?
- Which shared build, package, and NuGet configuration applies?
- Does a focused build distinguish SDK/tooling, project-reference, package, or compile causes?

## Findings

### SDK and repository configuration

- `global.json` pins SDK `10.0.200` and permits `latestFeature` roll-forward (`global.json:2-4`). The machine has `10.0.302` and `10.0.400-preview.0.26322.102`; `dotnet --info` selected the latter, emitted only `NETSDK1057` preview information, and found .NET 10 runtimes. This is not a missing-SDK failure, but the preview roll-forward is a reproducibility risk.
- The repository root contains `global.json`, `.editorconfig`, `.aspire/`, `src/`, `test/`, and the solution; no `Directory.Build.props`, `Directory.Build.targets`, `Directory.Build.rsp`, or NuGet config file was found in the workspace scan. `.gitignore` explicitly ignores `artifacts/`, `bin/`, `obj/`, and `.copilot-tracking/` (`.gitignore:25-31`, `.gitignore:63-69`, `.gitignore:1-1`). `.editorconfig` sets UTF-8 BOM, CRLF, and four-space indentation (`.editorconfig:3-10`) but does not alter project evaluation or package sources.
- Recent history is dominated by the Clean Architecture refactor: `8df68d8` (2026-07-28), `bbf9fa2` (2026-08-03), and merge `f23e50c` (2026-08-03). The tracked top-level Administration page was introduced by `6e577eb Add platform authentication and role-based authorization`.

### Solution and production graph

- `TNC.Trading.Platform.slnx` explicitly includes six source projects and ten test projects (`TNC.Trading.Platform.slnx:1-31`), so a solution build evaluates all sixteen projects.
- Every source project targets `net10.0` with nullable and implicit usings enabled: Application (`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:3-7`), Infrastructure (`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:3-9`), ServiceDefaults (`src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:3-8`), API (`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:16-20`), Web (`src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:13-17`), and AppHost (`src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:9-15`).
- Project-reference flow is coherent: Infrastructure -> Application (`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:13-15`); API -> Application, Infrastructure, ServiceDefaults (`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:3-7`); Web -> Application, ServiceDefaults (`src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:3-6`); AppHost -> API, Web (`src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:3-8`). No missing project-reference path was reported by the build.
- AppHost uses `Aspire.AppHost.Sdk/13.2.3` and packages `Aspire.Hosting.Keycloak` preview and `Aspire.Hosting.SqlServer` 13.2.3 (`src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-8`). API/Web use ASP.NET 10.0.5 packages and Scalar/Radzen (`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:9-13`; `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:8-11`). Infrastructure uses EF Core 10.0.5, Azure packages, and data protection packages (`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:17-28`).

### Test graph

- All ten test projects target `net10.0`, are non-packable, and use `Microsoft.NET.Test.Sdk` 17.14.1, xUnit 2.9.3, and xUnit VS runner 2.8.2. The common shape is visible in Architecture (`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj:3-15`), API unit tests (`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:3-15`), Application unit tests (`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:3-15`), Infrastructure unit tests (`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj:3-15`), and Web unit tests (`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:3-16`).
- API integration tests reference AppHost and Aspire.Hosting.Testing (`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:11-28`); AppHost unit tests reference AppHost and ServiceDefaults (`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:11-28`); Infrastructure integration tests reference Infrastructure and ServiceDefaults (`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:11-29`); Web functional/E2E tests reference AppHost and Aspire/Playwright packages (`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:11-28`; `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:11-28`).
- No test project reference is structurally missing. The test projects depending on Web/AppHost inherit the Web compile failure; this is a downstream symptom, not an independent test SDK or graph failure.

### Build evidence

- Focused no-restore build of `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj` succeeded. This rules out a basic SDK selection or universal package-assets failure.
- Focused no-restore build of `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj` built Application, ServiceDefaults, Infrastructure, and API, then failed in Web with: `Components/Pages/Administration/Administration_razor.g.cs(133,33): CS0111 Type 'Administration' already defines a member called 'BuildRenderTree'`. The only compile error was this duplicate generated Razor member. Non-blocking warnings included `NU1903` for `Microsoft.OpenApi` 2.0.0 and `NU1902/NU1903` for MessagePack 2.5.192.
- Solution no-restore build reproduced the same Web Razor error. It additionally showed Architecture, Application, API unit, and other lower-level projects succeeding; AppHost/Web-dependent tests failed as a consequence of the same referenced Web compilation. Package vulnerability warnings included `NU1904` for `Microsoft.AspNetCore.DataProtection` 10.0.5 and several `NU1902/NU1903` advisories for MessagePack, but none was the build-stopping error.
- The source cause is visible in the current worktree. `src/TNC.Trading.Platform.Web/Components/Pages/Administration.razor` exists but is empty (zero source lines), while `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor:1-2` declares the `TNC.Trading.Platform.Web.Components.Pages` namespace and the same `/administration/authentication` route, with component markup and code following at `:7-70`. Razor derives the same component type name `Administration` from both paths, so source generation emits two `BuildRenderTree` methods on the same type.
- `git status --short` shows the tracked top-level Administration file modified and the nested Administration directory untracked, alongside other page moves/deletions. These are pre-existing user working-tree changes observed during research; they were not reverted or edited. The nested file has no committed history result, while the tracked path has history ending at `6e577eb`.

## Diagnosis

The reported build failure is a compile-time Razor duplicate caused by two same-named component paths in the current working tree, not by SDK availability, a missing project reference, or package restore. The AppHost and solution builds merely expose it because their graph includes Web. Package vulnerability warnings are a separate dependency-hygiene concern and should not be used as the explanation for the failed build.

## Alternatives Considered

- SDK/tooling mismatch: considered and weakened. The pinned SDK rolls to an installed preview SDK; the build runs and lower-level projects succeed. Verify later against the intended CI SDK because `global.json` permits preview roll-forward.
- Project-reference graph error: considered and weakened. All referenced projects restore/build far enough to compile, and the failure is inside generated Web Razor source with no unresolved-reference diagnostic.
- Package restore/version failure: considered and weakened as the immediate cause. `--no-restore` builds use existing assets and reach compilation; package issues appear as vulnerability warnings, not restore or missing-type errors.
- Ordinary C# compile error: considered and rejected for this failure. The diagnostic is `CS0111` in `obj/.../Microsoft.CodeAnalysis.Razor.Compiler/...Administration_razor.g.cs`, and the duplicate is explained by the two source paths above.
- User working-tree path collision: best-supported explanation. The tracked empty file and untracked nested file are both included by the Web SDK default Razor item discovery and produce the same generated component type.

## Recommended Next Investigation

1. Confirm the intended page location with the owner of the current uncommitted page move, then remove or rename exactly one of the two `Administration.razor` paths. Do not alter unrelated page moves.
2. Delete `src/TNC.Trading.Platform.Web/obj` or run a clean Web build after the path correction, then rerun `dotnet build src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj --no-restore`.
3. Rerun `dotnet build TNC.Trading.Platform.slnx --no-restore` to confirm the downstream test projects recover.
4. Separately review the package advisories and determine whether package versions are intentionally pinned; this is independent of the Razor compile fix.
5. Decide whether CI should use a non-preview SDK and/or `rollForward: latestPatch` (or another explicit policy) after checking the repository's intended .NET 10 SDK baseline.
