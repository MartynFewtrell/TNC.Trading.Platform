---
name: run-stryker
description: Run Stryker.NET correctly against a selected .NET test project and report the mutation testing output location.
---

# Run Stryker.NET against a test project

## When this skill is useful

Use this skill when you need to execute mutation testing for a specific .NET test project and want to avoid common Stryker mistakes such as running from the wrong directory or targeting an unnecessarily broad or slow test suite.

## General context

- Workspace root: the solution or repository root containing the target test project
- Prefer a focused unit-test project for the first pass
- Use functional, integration, or E2E test projects only when explicitly requested or when mutation testing those layers is the actual goal, because they are slower and usually produce less actionable results
- Microsoft Learn guidance for Stryker.NET says to install `dotnet-stryker` and run `dotnet stryker` from the directory where the unit test project is located

## Steps

1. Identify the exact test project directory to mutate.
2. Prefer a focused unit-test project unless the user explicitly asks for a broader or slower test suite.
3. Change to the chosen test project directory from the repository root.
4. Ensure Stryker.NET is available by installing or updating the global tool if needed.
5. Optionally run `dotnet restore` for the test project if packages have not been restored yet.
6. Run `dotnet stryker` from the test project directory.
7. Wait for Stryker to finish running the test suite and generating its report.
8. Report the console result summary and the generated `StrykerOutput` location under the selected test project.

## Commands

```powershell
# Change to the target test project directory
Set-Location path/to/Your.TestProject

# Install or update Stryker.NET if needed
dotnet tool update -g dotnet-stryker
# If update fails because the tool is not installed yet, use:
# dotnet tool install -g dotnet-stryker

# Optional if restore has not already happened
dotnet restore

# Run mutation testing from the test project directory
dotnet stryker
```

## Correctness rules

- Run Stryker from the directory that contains the target test project `.csproj` unless the user explicitly provides an alternative supported invocation.
- Do not run Stryker from the solution root and assume it will pick the intended test project.
- Prefer unit-test projects for the first pass because mutation testing is expensive.
- Expect Stryker to create a `StrykerOutput` folder with HTML report artifacts beneath the selected test project directory.
- If mutation testing is too slow or noisy, narrow the target project or scope before suggesting broader use.

## Output expectations

Return a concise status with:
- selected test project
- commands executed
- whether Stryker completed successfully
- mutation score summary if available
- report folder path
- only blocking errors, if any
