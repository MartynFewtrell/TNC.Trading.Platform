---
name: run-coverlet
description: Run .NET tests with Coverlet to collect cross-platform code coverage and report the generated Cobertura output location.
---

# Run test coverage with Coverlet

## When this skill is useful

Use this skill when you need to measure .NET test coverage for a specific test project or solution area and want a consistent Coverlet-based command that produces a human-readable Cobertura report.

## General context

- Workspace root: the repository or solution root containing the target test project
- Prefer running coverage against a focused test project first so the output is easier to interpret and the run stays fast
- Microsoft Learn documents Coverlet collection with `dotnet test --collect:"XPlat Code Coverage"`
- The preferred output from the Coverlet collector is `coverage.cobertura.xml` under the test project's `TestResults` folder

## Steps

1. Identify the exact test project to measure.
2. Prefer a focused unit-test project unless the user explicitly asks for broader coverage.
3. Ensure the selected test project can use the Coverlet collector.
4. Change to the selected test project directory from the repository root.
5. Optionally run `dotnet restore` if packages have not already been restored.
6. Run `dotnet test --collect:"XPlat Code Coverage"` from the selected test project directory.
7. Wait for the test run to finish and for Coverlet to write the Cobertura report to `TestResults`.
8. Report the test outcome and the generated `coverage.cobertura.xml` path.

## Commands

```powershell
# Change to the target test project directory
Set-Location path/to/Your.TestProject

# Optional if restore has not already happened
dotnet restore

# Run tests and collect coverage with Coverlet
dotnet test --collect:"XPlat Code Coverage"
```

## Correctness rules

- Run the command from the directory that contains the intended test project `.csproj`, unless the user explicitly provides a different project path.
- Prefer the Coverlet collector approach for cross-platform coverage collection.
- Expect Coverlet to write `coverage.cobertura.xml` beneath the test project's `TestResults` directory.
- If the collector is unavailable for the target project, add or restore the appropriate Coverlet integration before retrying.
- Prefer a focused unit-test project for the first pass before expanding to slower functional or integration suites.

## Output expectations

Return a concise status with:
- selected test project
- commands executed
- whether the test run completed successfully
- coverage report file path
- only blocking errors, if any
