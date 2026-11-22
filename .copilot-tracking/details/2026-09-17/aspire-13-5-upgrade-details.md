<!-- markdownlint-disable-file -->
# Implementation Details: Aspire 13.5 Upgrade

## Context Reference

Sources: user-provided `.copilot-tracking/research/2026-09-17/aspire-13-5-upgrade-research.md`; verified scope at `.copilot-tracking/research/subagents/2026-09-17/aspire-13-5-upgrade-scope-research.md`; repository Aspire, authentication, and test instructions.

## Implementation Phase 1: Update the Aspire Cohort

<!-- parallelizable: false -->

### Step 1.1: Establish a current dependency baseline

From the repository root, run `dotnet restore TNC.Trading.Platform.slnx`, then retain restore output and run `dotnet list TNC.Trading.Platform.slnx package --include-transitive`. Compare resolved versions and advisories with the research before editing. Reconfirm package availability because the research snapshot is date-sensitive.

Files:

* `TNC.Trading.Platform.slnx` - Restore target for the complete solution.

Success criteria:

* The current resolved graph is captured before edits.
* Any difference from the researched package targets is recorded before choosing final versions.

Dependencies:

* .NET SDK `10.0.200`
* NuGet feed access

### Step 1.2: Update direct Aspire declarations atomically

Modify the exact package declarations below manually. Do not run `aspire update` after manual edits, and do not pin `MessagePack` directly.

Files:

* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj` - Change `Aspire.AppHost.Sdk` and `Aspire.Hosting.SqlServer` to `13.5.4`; change `Aspire.Hosting.Keycloak` to `13.5.4-preview.1.26464.4`.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - Change `Aspire.Hosting.Testing` to `13.5.4`.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj` - Change `Aspire.Hosting.Testing` to `13.5.4`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` - Change `Aspire.Hosting.Testing` to `13.5.4`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` - Change `Aspire.Hosting.Testing` to `13.5.4`.

Success criteria:

* No direct Aspire package remains on `13.2.3`.
* The Keycloak preview integration exactly matches the researched 13.5 preview train.
* AppHost source and topology are unchanged in this package-only phase.

Context references:

* `.copilot-tracking/research/subagents/2026-09-17/aspire-13-5-upgrade-scope-research.md` (Lines 13-23) - Direct Aspire package owners and targets.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:17-36` - Keycloak and local infrastructure behavior to preserve.

Dependencies:

* Step 1.1 completion

### Step 1.3: Validate cohort cohesion

Run `dotnet build TNC.Trading.Platform.slnx --no-restore` followed by `dotnet list TNC.Trading.Platform.slnx package --include-transitive`. Confirm that `MessagePack 2.5.192` is not resolved and that no Aspire `13.2.3` package remains.

Success criteria:

* The solution compiles after the Aspire cohort change.
* Dependency inspection shows a coherent Aspire cohort and no vulnerable baseline MessagePack version.

Dependencies:

* Step 1.2 completion

## Implementation Phase 2: Service Advisory Parents

<!-- parallelizable: false -->

### Step 2.1: Service direct .NET advisory parents

Update only the identified direct advisory parents and adjacent EF Core packages to `10.0.12`. Do not broaden the update to unrelated `10.0.5` package declarations unless fresh restore output identifies them as advisory parents.

Files:

* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj` - Update `Microsoft.AspNetCore.OpenApi` to `10.0.12`.
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj` - Update `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.InMemory`, and `Microsoft.EntityFrameworkCore.SqlServer` to `10.0.12`.
* `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj` - Update `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, and `Microsoft.AspNetCore.OpenApi` to `10.0.12`.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj` - Update `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.SqlServer` to `10.0.12`.

Discrepancy references:

* Addresses DR-01 by limiting direct servicing to confirmed advisory parents and their EF Core cohort.

Success criteria:

* OpenAPI and Data Protection/XML Crypto advisories resolve through updated direct parents.
* Paired EF Core packages retain the same `10.0.12` servicing version within each affected project.

Context references:

* `.copilot-tracking/research/subagents/2026-09-17/aspire-13-5-upgrade-scope-research.md` (Lines 34-48) - Confirmed package owners.

Dependencies:

* Step 1.1 completion; this step may be edited in parallel with Step 1.2 because it touches different project files.

### Step 2.2: Add the narrow AngleSharp test override

Add a direct `AngleSharp` reference with version `1.5.0` or later to the Web unit-test project only. Add or update a concise project-file comment stating it is temporary and may be removed after bUnit publishes a stable patched dependency floor.

Files:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` - Add the temporary direct `AngleSharp` override beside the existing bUnit reference.

Discrepancy references:

* Addresses DR-01 without applying a solution-wide transitive override.

Success criteria:

* The resolved graph no longer includes vulnerable `AngleSharp 1.2.0`.
* No production project gains an AngleSharp dependency.

Context references:

* `.copilot-tracking/research/subagents/2026-09-17/aspire-13-5-upgrade-scope-research.md` (Lines 50-54) - bUnit ownership and removal condition.

Dependencies:

* Step 1.1 completion; independent from Step 1.2.

### Step 2.3: Confirm all advisory warnings are removed

Run `dotnet restore TNC.Trading.Platform.slnx`, then `dotnet build TNC.Trading.Platform.slnx --no-restore`, inspect the transitive graph, and run targeted `dotnet nuget why` checks for `MessagePack`, `AngleSharp`, `Microsoft.OpenApi`, and `System.Security.Cryptography.Xml`. Do not add `NoWarn`, set a lower audit level, or suppress advisories to satisfy this gate.

Validation commands:

* `dotnet restore TNC.Trading.Platform.slnx` - Re-resolve all package declarations.
* `dotnet build TNC.Trading.Platform.slnx --no-restore` - Verify a warning-free build.
* `dotnet list TNC.Trading.Platform.slnx package --include-transitive` - Confirm vulnerable baseline versions are absent.
* `dotnet nuget why TNC.Trading.Platform.slnx MessagePack` - Confirm the Aspire RPC path no longer resolves the vulnerable baseline.
* `dotnet nuget why TNC.Trading.Platform.slnx AngleSharp` - Confirm the Web unit-test override resolves the patched version.
* `dotnet nuget why TNC.Trading.Platform.slnx Microsoft.OpenApi` - Confirm OpenAPI resolves through serviced parents.
* `dotnet nuget why TNC.Trading.Platform.slnx System.Security.Cryptography.Xml` - Confirm Data Protection resolves through serviced parents.

Success criteria:

* Restore and build report zero `NU1902`, `NU1903`, and `NU1904` warnings.
* No advisory suppression or standalone MessagePack, Microsoft.OpenApi, Data Protection/XML Crypto pin is introduced.

Dependencies:

* Steps 1.2, 2.1, and 2.2 completion

## Implementation Phase 3: Focused Regression Tests

<!-- parallelizable: false -->

### Step 3.1: Execute the staged test suite

Run focused projects in this order, retaining the normal test output for diagnosis:

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore`
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --no-restore`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore`

Success criteria:

* Each focused suite passes before the next runtime acceptance gate.
* Unit and AppHost checks fail fast before Docker-backed functional and E2E execution.

Dependencies:

* Step 2.3 completion

### Step 3.2: Protect test behavior during repair

For any test failure, make the smallest compatibility repair after confirming it is caused by the updated package graph. Preserve `GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)` in `AppHostEnvironmentWiringTests`; do not replace it with run-mode resolution, which has been observed to hang in this repository.

Files:

* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs` - Preserve publish-mode inspection at lines 212, 249, and 322-323 if compatibility edits are necessary.

Success criteria:

* Repairs retain closed-box AppHost boundaries and disposal conventions.
* No unrelated product behavior changes are included.

Dependencies:

* Step 3.1 test result

## Implementation Phase 4: Distributed Runtime and Final Validation

<!-- parallelizable: false -->

### Step 4.1: Update and verify the Aspire CLI

Run `aspire update --self` before runtime acceptance, then run `aspire --version` and record the result in the implementation evidence. Use the selected stable CLI update channel only; do not enable preview, staging, or development CLI channels.

Success criteria:

* The CLI self-update completes and the recorded version is compatible with the updated Aspire 13.5 AppHost.
* Runtime acceptance uses the verified CLI rather than an existing unknown version.

Dependencies:

* Step 2.3 completion
* Internet access for CLI self-update

### Step 4.2: Exercise the Docker-backed AppHost topology

With Docker available, launch the AppHost using `aspire run` or `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`. Update the VS Code Aspire extension, then use its resource controls, logs, health views, and dashboard behavior to verify SQL Server, Mailpit, Keycloak, API, and Web become healthy. Open the dashboard explicitly because Aspire 13.5 no longer auto-opens it in VS Code.

Success criteria:

* Resources start through Aspire without manual infrastructure setup.
* API and Web retain `https+http://api` service discovery and dependency readiness.
* The current VS Code Aspire extension exposes expected resource health, controls, and logs.

Dependencies:

* Step 3.1 and Step 4.1 completion
* Docker runtime

### Step 4.3: Complete user-facing runtime acceptance

Verify Keycloak realm import, the configured stable local authority/port, OIDC sign-in, persistent local state, API and Web health endpoints, and Scalar/OpenAPI behavior. Account for the Aspire 13.5 VS Code extension behavior, which no longer automatically opens the dashboard.

Success criteria:

* Authenticated browser flow succeeds against Aspire-orchestrated Keycloak.
* Existing runtime topology and observable API behavior remain intact.

Dependencies:

* Step 4.2 completion

### Step 4.4: Record Aspire 13.5 feature assessment outcome

Record in release evidence or the pull request that VS Code Aspire extension validation is adopted for this update. Explicitly defer custom resource commands, file input, experimental terminals, certificate/progress interaction APIs, TypeScript AppHost, Azure/Kubernetes/Radius additions, Redis modules, and CLI bundling because no concrete repository need exists.

Success criteria:

* The requested Aspire 13.5 feature assessment has a traceable adopt/defer outcome.
* Deferred features are not introduced as unreviewed scope expansion.

Dependencies:

* Step 4.2 completion

### Step 4.5: Run final solution validation and hand off exceptions

Execute the full validation set. Fix isolated package-upgrade regressions and re-run the failing scoped validation. Record any failure requiring product redesign, new Aspire feature adoption, or security-incident response as a new planning item rather than expanding this change.

Validation commands:

* `dotnet build TNC.Trading.Platform.slnx --no-restore`
* `dotnet test TNC.Trading.Platform.slnx --no-restore`
* `dotnet list TNC.Trading.Platform.slnx package --include-transitive`
* `dotnet nuget why TNC.Trading.Platform.slnx MessagePack`
* `dotnet nuget why TNC.Trading.Platform.slnx AngleSharp`
* `dotnet nuget why TNC.Trading.Platform.slnx Microsoft.OpenApi`
* `dotnet nuget why TNC.Trading.Platform.slnx System.Security.Cryptography.Xml`

Success criteria:

* Final build and tests pass without vulnerability warnings.
* Any remaining deployment key-rotation assessment is reported only if an exposed deployment used an affected Data Protection version.

Dependencies:

* Steps 4.2, 4.3, and 4.4 completion

### Step 4.6: Assign conditional Data Protection security follow-up

The release owner assigns the application security or operations owner to review deployment records for internet exposure while affected Data Protection packages were deployed. If exposure is confirmed, create a tracked security work item to assess protected-artifact impact and perform the advisory-recommended key rotation. Otherwise, record the disposition and close the conditional work item.

Success criteria:

* Deployment exposure history has an accountable owner and recorded disposition.
* Key rotation is initiated only when the exposure assessment warrants it.

Dependencies:

* Step 4.5 completion

## Dependencies

* NuGet package availability reconfirmed at execution time
* Docker-backed local infrastructure
* Aspire 13.5-compatible CLI for runtime acceptance

## Success Criteria

* The solution has a coherent Aspire 13.5 package cohort, resolved advisories, passing staged tests, and verified Docker-backed runtime behavior.
