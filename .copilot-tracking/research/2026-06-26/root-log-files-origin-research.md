<!-- markdownlint-disable-file -->
# Task Research: Root Log Files Origin

Determine where root-level log files such as `apphost.out.log` and `apphost.err.log` originate in this repository, whether they are created by repository configuration or local execution workflow, and how to prevent them from appearing.

## Task Implementation Requests

* Identify the source of root-level log files like `apphost.out.log` and `apphost.err.log`.
* Determine whether Aspire itself creates them or whether they come from local tooling, scripts, or task configuration.
* Explain practical ways to stop them from being created in the project root.

## Scope and Success Criteria

* Scope: Repository configuration, run scripts, VS Code tasks, launch settings, tracked artifacts, and attached log evidence. Excludes global machine settings unless repository evidence points there.
* Assumptions:
  * The attached `apphost.out.log` and `apphost.err.log` are representative examples.
  * The repository root is the current workspace root.
  * The user wants cause analysis first, with prevention guidance second.
* Success Criteria:
  * Identify the most likely creator of the root log files.
  * Distinguish repository behavior from Aspire default behavior.
  * Provide one recommended way to stop the files appearing.

## Outline

1. Search the repository for explicit references to the log filenames or output redirection patterns.
2. Inspect launch and task configuration around AppHost startup.
3. Compare findings with the attached log content.
4. Consolidate a recommendation.

## Potential Next Research

* Check local shell history or user-level editor tasks outside the repository if the exact generating command is needed.
  * Reasoning: Committed repository files do not reference the log filenames or any redirection workflow that would create them.
  * Reference: .gitignore:107, .gitignore:395, docs/wiki/local-development.md:35-42.

## Research Executed

### File Analysis

* apphost.out.log
  * Contains ordinary AppHost stdout from `dotnet run`, including launch-settings, build, Aspire hosting, listener URL, and dashboard login messages. Evidence: apphost.out.log:1-9.
* src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json
  * Defines standard AppHost launch profiles and environment variables, but no file logging or output redirection. Evidence: src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:1-26.
* docs/wiki/local-development.md
  * Documents the supported local run command as a plain `dotnet run --project ...` from the repository root, with no output capture to files. Evidence: docs/wiki/local-development.md:35-42.
* .gitignore
  * Ignores `*.log` files globally and allows `.vscode/tasks.json` / `.vscode/launch.json` to exist unignored, which suggests root log files are expected local byproducts if created, not committed project artifacts. Evidence: .gitignore:107, .gitignore:395-399.

### Code Search Results

* `apphost.out.log|apphost.err.log|dashboard.html|dashboard-cookies.txt|artifacts-apphost|> apphost|2> apphost|Out-File|Start-Transcript|Tee-Object`
  * No committed matches outside research notes, which strongly suggests the generating workflow is not stored in repository code or config.
* `.vscode/*`
  * No committed workspace task or launch files were present to explain the behavior.
* `*.ps1`, `*.cmd`, `*.bat`
  * No committed helper scripts were found in the repository root that would account for these files.

### External Research

* None yet

### Project Conventions

* Standards referenced: Repository local-development guide, AppHost launch settings, ignore rules
* Instructions followed: Task Researcher mode

## Key Discoveries

### Project Structure

The repository root currently contains several ad hoc artifact-style files, including `apphost.out.log`, `apphost.err.log`, `dashboard.html`, `dashboard-cookies.txt`, and similarly named `artifacts-*` captures. That pattern does not align with normal Aspire project output structure, which ordinarily stays in the terminal, Dashboard, and build output folders unless explicitly redirected.

### Implementation Patterns

The attached `apphost.out.log` matches normal stdout from running AppHost with `dotnet run`. The empty stderr companion file fits a shell command that redirected stdout and stderr separately, for example `> apphost.out.log 2> apphost.err.log`, rather than any built-in Aspire file sink. Aspire itself does not create `apphost.out.log` in the repository root as part of its default local startup behavior.

Because the repository’s own run instructions and launch settings contain no file logging configuration, the strongest conclusion is that these files come from a local interactive command, uncommitted VS Code task, user-level task, shell alias, or one-off automation workflow outside committed repo files.

### Complete Examples

```text
Repository-supported local run command
  dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj

Typical local redirection patterns that would create the observed files
  dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj > apphost.out.log 2> apphost.err.log
  dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj *> apphost.out.log
```

### API and Schema Documentation

Not applicable. The behavior under investigation is local execution and output capture, not API design.

### Configuration Examples

```text
launchSettings.json
  commandName = Project
  dotnetRunMessages = true
  applicationUrl = https://localhost:17257;http://localhost:15046
  environmentVariables = ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, Aspire dashboard endpoints

No settings present for:
  file logging
  console output redirection
  transcript capture
```

## Technical Scenarios

### Root Log File Creation

The most likely origin of the root log files is not Aspire itself. It is a local workflow that runs the documented AppHost command and redirects console output into files in the repository root.

The strongest evidence is that the log content is ordinary AppHost startup text, the committed repo has no script or task referencing those filenames, the launch settings are standard, and the repository documentation tells developers to run AppHost directly without any file capture.

**Requirements:**

* Identify origin of log files.
* Determine whether Aspire creates them by default.
* Recommend how to stop them appearing.

**Preferred Approach:**

* Treat the files as local redirection artifacts, remove the redirection at its source, and if capture is still needed, redirect into an ignored subfolder or temp path instead of the repository root.

```text
Recommended prevention hierarchy
1. Stop redirecting AppHost stdout/stderr to repo-root files.
2. If logs are needed, redirect to an ignored location such as:
   artifacts\local\apphost.out.log
   %TEMP%\tnc-trading\apphost.out.log
3. Use the terminal or Aspire dashboard directly when persistent log capture is unnecessary.
4. Only use additional .gitignore rules if the goal is to hide files from Git, not to stop creation.
```

**Implementation Details:**

The committed repository does not appear to be the source. There is no `.vscode/tasks.json`, no committed launch task, no repository script, and no matching file-name references in code or docs. The root README and local development guide both describe normal AppHost startup, and the AppHost launch settings only specify URLs and environment variables.

That is why you have not seen this in other Aspire projects. This is not a normal Aspire convention. It is almost certainly specific to a local command or tool on this machine or in this workspace state.

If you want to stop the files appearing, find the local command that is starting AppHost and remove the redirection operators or change the destination path. In PowerShell, the patterns to look for are `>`, `2>`, `*>`, `Out-File`, `Tee-Object`, or `Start-Transcript`. A user-level VS Code task or shell profile could also be doing this.

```text
Most likely source classes

Interactive shell command
  dotnet run ... > apphost.out.log 2> apphost.err.log

User-level VS Code task or launch config outside repo
  task command redirects terminal output to files

Shell alias or helper function
  wrapper around dotnet run or aspire run

One-off automation command
  script that captures dashboard or AppHost startup artifacts
```

#### Considered Alternatives

Alternative 1: Aspire writes these files by default.
Rejected because the output content is standard stdout, the repo run guide uses plain `dotnet run`, and the launch settings contain no file-log configuration. Evidence: apphost.out.log:1-9, docs/wiki/local-development.md:35-42, src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:1-26.

Alternative 2: The repository contains a committed task or script that creates the files.
Rejected because searches found no committed `.vscode` files, no helper scripts, and no references to the filenames outside research notes. Evidence: .vscode search results, repository-wide filename search.

Alternative 3: The files are produced by standard build output.
Rejected because normal .NET build and AppHost output goes to the terminal and bin/obj folders, not ad hoc root filenames like `apphost.out.log` and `dashboard-cookies.txt`.
