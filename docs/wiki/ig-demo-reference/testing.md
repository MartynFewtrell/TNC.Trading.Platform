# Testing Strategy

## Purpose

Summarize automated test coverage and the validation possible from this checkout.

## Key points (TL;DR)

* No `test/` directory, test project, unit tests, or integration tests are
  present in the tracked repository.
* `.travis.yml` contains a C# build declaration but references a solution file
  absent from the tracked file list.
* The attempted `dotnet build` cannot proceed with the installed toolchain.
* IG behavior has not been validated against a live or demo account.

## Details

### Existing verification

The README gives manual build-and-run instructions. The source tree has no
automated test files. The only CI configuration is `.travis.yml`, which names
`IGWebApiClient.sln`; the repository's root solution is
`IGWebApiDotNetSample.sln`.

The attempted command `dotnet build IGWebApiDotNetSample.sln --no-restore` failed
before compilation because the .NET Framework 4.6.1 reference assemblies and
legacy Portable Class Library targets are missing. No test command can be
identified from repository configuration.

### Recommended tests for a replica

* Unit-test route/version construction, JSON serialization, response parsing,
  and error-body handling with a mocked HTTP transport.
* Unit-test session creation and token extraction without storing real
  credentials.
* Test streaming field parsing with representative snapshots and partial
  updates, including null values and malformed fields.
* Test subscription ownership and cleanup when switching tabs, accounts, or
  application lifetime.
* Add an opt-in demo-account smoke test only when credentials and vendor test
  environment are provisioned securely.

These are recommendations, not existing tests.

## Evidence (repo references)

* Tracked file inventory: no test project or test source files.
* `.travis.yml`: C# CI setup references `IGWebApiClient.sln`.
* `IGWebApiDotNetSample.sln`: tracked root solution name.
* `IGWebApiClient/IGRestService.cs` and `IGWebApiClient/IGStreamingApi.cs`:
  testable transport and field parsing boundaries.

## Open questions / assumptions (if any)

* Assumption: No external test suite runs outside the checked-in repository.
  * Missing evidence: No external pipeline configuration or test report.
  * Risk/impact: Existing external validation cannot be reproduced from this
    checkout.
