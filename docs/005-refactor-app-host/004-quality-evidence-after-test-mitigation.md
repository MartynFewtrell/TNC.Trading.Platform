# Quality evidence after test mitigation

This note captures the refreshed coverage and mutation evidence gathered after completing the AppHost test-mitigation work for work package `005-refactor-app-host`. It supplements, but does not replace, the baseline review in [Project test review report](002-project-test-review-report.md).

## Scope

- Work package: `docs/005-refactor-app-host/`
- Source mitigation plan: `plans/003-work-package-test-mitigation-plan.md`
- Baseline comparison: `002-project-test-review-report.md`
- Measurement date: `2026-05-25`

## Coverlet rerun summary

| Test project | Latest result | Line coverage | Branch coverage | Baseline comparison | Coverage report |
| --- | --- | --- | --- | --- | --- |
| `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests` | Success | `94.69%` | `100.00%` | New direct AppHost signal added after the baseline review | `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TestResults/fe903818-1220-4c26-844a-a4fdacc33553/coverage.cobertura.xml` |
| `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` | Success | `55.11%` | `42.81%` | No material change versus baseline | `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TestResults/1aa5adf1-40ef-417c-8722-528bfb4e3263/coverage.cobertura.xml` |
| `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` | Success | `36.51%` | `24.46%` | Improved from `35.82%` line and `22.93%` branch | `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TestResults/0793359b-4aff-4879-be80-dca7586d1540/coverage.cobertura.xml` |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` | Success | `5.87%` | `7.23%` | Improved from `4.88%` line and `5.10%` branch | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TestResults/387f93df-3660-4285-8289-227ab0953e20/coverage.cobertura.xml` |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` | Success | `34.17%` | `26.16%` | No material change versus baseline | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TestResults/37334fc9-3b68-41bc-b0a3-a2211ce95372/coverage.cobertura.xml` |

## Coverlet conclusions

- The mitigation added the previously missing AppHost-focused coverage and turned the AppHost refactor support units into a high-signal, low-cost regression net.
- API unit coverage improved, but it remains the weakest lower-level coverage area and still requires future focused growth if more host or endpoint behavior is added.
- Infrastructure unit coverage improved modestly and remains useful, but it still shows room for more branch-sensitive assertions in configuration and persistence paths.
- Web unit coverage held steady because the mitigation work primarily reconciled and documented already-delivered component coverage rather than broadening the suite substantially.

## Stryker rerun summary

| Test project | Latest result | Final mutation score | Baseline comparison | Report output |
| --- | --- | --- | --- | --- |
| `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests` | Success | `77.86%` | New mutation signal added after the baseline review | `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/StrykerOutput/2026-05-25.15-21-26/reports/mutation-report.html` |
| `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` | Success | `18.80%` | No material change versus baseline `18.80%` | `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/StrykerOutput/2026-05-25.15-23-44/reports/mutation-report.html` |
| `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` | Success | `27.83%` | Improved from baseline `25.67%` | `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/StrykerOutput/2026-05-25.15-24-44/reports/mutation-report.html` |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` | Success | `26.56%` | Improved from baseline `18.52%` | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/StrykerOutput/2026-05-25.15-22-43/reports/mutation-report.html` |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` | Failed | Not available | Still blocked; baseline review also failed to obtain a score | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StrykerOutput/2026-05-25.15-26-30/` |

## Stryker conclusions

- The new AppHost unit suite now provides strong mutation resistance around AppHost configuration parsing and composition wiring, which closes the previous direct-evidence gap for findings `F1` and `F6`.
- API mutation resistance improved materially, but low-cost API coverage still leaves many mutants uncovered or surviving outside the newly hardened auth-audit and registration seams.
- Infrastructure mutation resistance improved slightly, but the surviving-mutant picture is still concentrated in persistence and configuration-heavy code where broader branch and state assertions would help more than percentage chasing.
- Application mutation resistance did not improve during this work item because the mitigation scope targeted AppHost, API, distributed-runtime reuse, and Web coverage reconciliation rather than broader application orchestration behavior.
- Web mutation evidence is still blocked by a Stryker compiler failure when mutating `src/TNC.Trading.Platform.Web/Program.cs`; the tool reports `CS0246` for `App` after mutation and exits before producing a JSON or HTML report.

## Survived-mutant hotspots reviewed

The latest successful Stryker JSON reports show these top survived-mutant hotspots:

- AppHost: `AppHostEnvironmentWiring.cs`, `AppHostInfrastructureRegistration.cs`, and `AppHostProjectRegistration.cs`
- Application: `PlatformStateCoordinator.cs`, `PlatformRetryCycle.cs`, and `PlatformRuntimeState.cs`
- Infrastructure: `SqlPlatformConfigurationStore.cs`, `PlatformDbContext.cs`, and `OperationalRecordRetentionProcessor.cs`
- API: `UpdatePlatformConfigurationValidator.cs`, `PlatformAuthAuditEventResolver.cs`, and `PlatformApiAuthenticationServiceCollectionExtensions.cs`

No extra tests were added during this evidence pass solely to move percentages. The surviving-mutant hotspots were reviewed as prioritization input for future work, and the follow-up value remains highest in the Application orchestration paths and the still-low-signal API unit areas.

## Deferred follow-up items

- Keep the Web Stryker failure explicit until the underlying Stryker-versus-Blazor mutation compile issue is resolved or isolated away from `Program.cs`.
- Use the Application and Infrastructure survived-mutant hotspots as future hardening candidates when related behavior changes are already in scope.
- Continue expanding cheap API unit coverage before adding new high-cost distributed cases if future API host behavior is introduced.
