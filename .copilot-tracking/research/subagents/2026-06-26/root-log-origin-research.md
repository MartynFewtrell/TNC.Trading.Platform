---
title: Root Log Origin Research
description: Repository research into the origin of root-level AppHost and dashboard log files
author: GitHub Copilot
ms.date: 2026-06-26
ms.topic: troubleshooting
keywords:
  - apphost logs
  - aspire
  - local development
  - output redirection
estimated_reading_time: 5
---

## Research Scope

* Search the repository for references to apphost.out.log, apphost.err.log, wildcard log names, output redirection, and related root artifacts.
* Inspect committed local-development guidance, AppHost launch configuration, and any VS Code task surface.
* Compare the attached log content with normal Aspire AppHost console output.
* Determine the most likely origin and practical prevention options.

## Status

Complete.

## Findings

* The committed local-development workflow tells developers to start the application with a plain AppHost run command from the repository root: `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`. There is no documented redirection or log-file capture in the repository guidance. Evidence: docs/wiki/local-development.md:35-41.

* The committed AppHost launch profile is normal Aspire development configuration. It enables `dotnetRunMessages`, browser launch, URLs, and Aspire endpoint environment variables, but it does not declare any file sink, transcript path, or custom log output path. Evidence: src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:1-27.

* The attached apphost output is ordinary Aspire AppHost console startup text, not a distinct Aspire-generated artifact format. It shows the standard `Using launch settings...`, `Building...`, Aspire hosting logger entries, listener URL, and dashboard login URL that appear on stdout during `dotnet run`. Evidence: apphost.out.log:1-14.

* The matching stderr files are empty, which fits shell redirection of stderr into a file even when no errors were written. Evidence: apphost.err.log exists and is empty; apphost-dashboard-err.log exists and is empty.

* The repository root currently contains multiple similarly named local artifacts, including apphost.out.log, apphost.err.log, apphost-dashboard-out.log, apphost-dashboard-err.log, dashboard.html, dashboard-cookies.txt, artifacts-apphost-out.log, artifacts-apphost-err.log, artifacts-apphost-out.txt, artifacts-apphost-err.txt, and artifacts-functional-out.txt. That naming pattern looks like ad hoc local capture output from shell commands or manual/browser automation rather than files emitted by Aspire itself.

* No committed `.vscode` task, launch, or extension configuration explains the files. The `.vscode/` directory is present but empty in this workspace snapshot.

* The repository already ignores generic log files, which supports the conclusion that these are local byproducts rather than intended tracked outputs. `.gitignore` ignores `*.log`, and it also ignores most `.vscode` content. Evidence: .gitignore:107-108; .gitignore:395-400.

* The repository also ignores `artifacts/`, but not the root-level `artifacts-*.txt` or `artifacts-*.log` files. That means a local script can still create those files at the repository root even though the `artifacts/` directory itself is ignored. Evidence: .gitignore:49-51.

* Repository inspection found no committed references to `apphost.out.log`, `apphost.err.log`, `apphost-dashboard-out.log`, `apphost-dashboard-err.log`, `dashboard.html`, `dashboard-cookies.txt`, `artifacts-apphost-out.*`, or `artifacts-apphost-err.*` outside prior research notes. That strongly suggests the generating command lives outside committed repository files, such as an interactive shell command, an uncommitted local task, or a one-off automation command.

## Conclusion

The most likely origin is local stdout and stderr redirection around the normal AppHost startup command, most likely something equivalent to one of these patterns run from the repository root:

```powershell
dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj *> apphost.out.log
dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj > apphost.out.log 2> apphost.err.log
```

The same conclusion applies to apphost-dashboard-out.log and apphost-dashboard-err.log. Their content matches the same standard AppHost console startup stream captured to different filenames. The additional root files `dashboard.html` and `dashboard-cookies.txt` also look like manually saved browser or HTTP-session artifacts, not Aspire-generated defaults.

Nothing in the committed repository configuration indicates that Aspire itself is configured to emit these files automatically. The evidence points to a local execution workflow layered on top of the documented `dotnet run` command.

## Prevention Options

* Stop redirecting AppHost stdout and stderr to files in the repository root. Use the documented direct command from docs/wiki/local-development.md:35-41 and let output stay in the terminal.

* If log capture is needed, redirect to an ignored subdirectory or a temporary folder instead of the repository root. Examples: `artifacts\local\apphost.out.log` or `%TEMP%\tnc-trading\apphost.out.log`.

* If a local script or task is doing the capture, change it to set an explicit output path away from the root and keep that path ignored.

* If the team wants defense in depth, add explicit ignore rules for the root artifact name patterns. That would prevent accidental tracking, but it would not stop creation. Creation stops only when the redirect target changes or the redirect is removed.

## Strongest Evidence

* docs/wiki/local-development.md:35-41
* src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:1-27
* apphost.out.log:1-14
* .gitignore:49-51
* .gitignore:107-108
* .gitignore:395-400

## Unresolved Ambiguities

* The exact generating command was not found in committed repository files.
* The most likely remaining sources are an uncommitted local task, an interactive terminal command, or an external helper script run from the repository root.

## Recommended Next Research

* Check the user's shell history or any uncommitted local scripts/tasks for `dotnet run` commands that redirect stdout or stderr.
* Check any local browser-automation or dashboard-scraping workflow for commands that write `dashboard.html` or `dashboard-cookies.txt` into the repository root.