<!-- markdownlint-disable-file -->
# Task Research: Operational Record Retention Cancellation

Investigate the reported `TaskCanceledException` at
`OperationalRecordRetentionService.ExecuteAsync` line 31 and recommend a
safe, narrow resolution. This research does not change production code.

## Task Implementation Requests

* Determine whether the exception represents a retention-processing failure or
  expected application shutdown.
* Trace the service's registration, startup and periodic invocation paths,
  cancellation flow, configuration, and current automated-test coverage.
* Reassess the cancellation using the fact that it occurred while application
  startup was still in progress.
* Recommend one implementation approach, including test coverage, with
  evidence.

## Scope and Success Criteria

* Scope: The `net10.0` hosted API, Infrastructure retention implementation,
  configuration policy, and directly related tests.
* Excluded: Capturing a live runtime trace and modifying application code.
* Assumption: The reported stack trace refers to the current
  `OperationalRecordRetentionService.cs:31`. The complete ordered API,
  AppHost, and debugger log timeline was not supplied.
* Success criteria:
  * Identify the exact cancellable operation at the reported line.
  * Distinguish normal host-stop behavior from an operational failure.
  * Select a precise handling strategy that preserves fault diagnostics.
  * Specify deterministic regression tests.

## Executive Conclusion

The reported `TaskCanceledException` is a **downstream symptom**, not the
startup root cause. A controlled Aspire CLI reproduction established the
failure chain:

1. ASP.NET Core builds the endpoint pipeline during host startup.
2. Minimal API endpoint handlers take
   `IBrokerEnvironmentCatalogService service` parameters.
3. The active `AddPlatformInfrastructure` implementation does not register
   that interface, so ASP.NET Core infers `service` as an HTTP request body.
4. A `GET` endpoint cannot use an inferred body parameter. Endpoint-pipeline
   construction throws `InvalidOperationException`, so hosting fails to start.
5. The Generic Host stops the background services. Their stopping tokens
   cancel, resulting in the retention worker's line-31
   `TaskCanceledException`.

The active Infrastructure registration has diverged from a duplicate extension
that does register the service. Add the missing scoped
`IBrokerEnvironmentCatalogService` registration to the active
`Infrastructure.DependencyInjection.PlatformInfrastructureServiceCollectionExtensions`
method. Do not switch to the older duplicate extension. The narrow retention
cancellation handling remains valid hardening, but cannot fix the DI failure.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionService.cs:8-34`
  * The service inherits `BackgroundService`.
  * It passes `stoppingToken` to processor work at line 18 and to the
    one-hour `Task.Delay` at line 31.
  * Its `OperationCanceledException when
    (stoppingToken.IsCancellationRequested)` handler covers processor work but
    not the delay.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:88-90`
  * The processor is scoped and the retention service is registered with
    `AddHostedService`.
* `src/TNC.Trading.Platform.Api/Program.cs:36-53,105`
  * Infrastructure registration occurs in the API composition root.
  * Startup cleanup finishes before `app.RunAsync` begins the hosted-service
    lifecycle.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:93-124`
  * The mapped broker-environment endpoint handlers declare
    `IBrokerEnvironmentCatalogService service` parameters.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:1-125`
  * This is the active extension imported by `Program.cs`; it omits an
    `IBrokerEnvironmentCatalogService` registration.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:1-106`
  * An unreferenced duplicate extension registers
    `IBrokerEnvironmentCatalogService` as
    `SqlBrokerEnvironmentCatalogService`, evidencing composition-root drift.
* `src/TNC.Trading.Platform.Application/Features/BrokerEnvironments/IBrokerEnvironmentCatalogService.cs:1-12`
  * The interface defines the catalog operations required by the endpoint
    handlers.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:21-27`
  * The same processor runs once at startup before periodic cleanup begins.
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:32-55`
  * Its `StartAsync` awaits an initial authentication tick before delegating to
    `BackgroundService.StartAsync`, making it a host-blocking startup path
    that can fail after the retention worker has started.
* `src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs:21-33`
  * This is a conventional background worker that cooperates with cancellation
    during a confirmed stop.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:13-24,38-47`
  * The API explicitly waits for SQL Server and Mailpit. The inspected
    registration contains no verified Keycloak readiness wait for the custom
    initial authentication tick.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:15-113`
  * EF Core work receives the supplied token on every path, making
    cancellation during processor execution an expected shutdown scenario.
* `src/TNC.Trading.Platform.Api/appsettings.json:36-38`
  * The default retention age is 90 days.
* `src/TNC.Trading.Platform.Application/Services/OperationalRecordRetentionPolicy.cs:5-23`
  * The configured age controls the deletion cutoff only and does not affect
    the one-hour scheduling delay.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalRecordRetentionProcessorTests.cs:10-117`
  * Tests cover deletion eligibility and cutoff semantics, not hosted-service
    cancellation behavior.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:699-738`
  * Tests cover the SQL Server `ExecuteDeleteAsync` path, not the worker
    lifecycle.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/PlatformStartupInitializerTests.cs:90-99`
  * Startup cancellation propagation is intentionally tested and must remain
    distinct from normal hosted-service stop handling.

### External Research

* Microsoft Learn: [BackgroundService.ExecuteAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice.executeasync?view=net-10.0-pp)
  * The supplied stopping token is triggered when `StopAsync` is called;
    cancellation is represented by an `OperationCanceledException` on the
    returned task.
* Microsoft Learn: [BackgroundService.StopAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice.stopasync?view=net-10.0-pp)
  * Graceful shutdown triggers the hosted service's stop lifecycle.
* Microsoft Learn: [Task.Delay](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.delay?view=net-10.0)
  * The cancellation-token overload of `Task.Delay` is intended to end early
    when the token is cancelled.
* Microsoft Learn: [HostOptions.ShutdownTimeout](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.hostoptions.shutdowntimeout?view=net-10.0-pp)
  * Host shutdown has a finite timeout, reinforcing the need for cooperative
    cancellation rather than waiting for the periodic interval.
* Microsoft Learn: [Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
  * Hosted-service startup and shutdown are part of the host lifecycle and
    should be correlated with `Microsoft.Hosting.Lifetime` events.
* .NET runtime: [BackgroundService source](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting.Abstractions/src/BackgroundService.cs)
  * `StartAsync` creates the linked stopping source, queues `ExecuteAsync`,
    and normally returns immediately; `StopAsync` cancels that source.
* .NET runtime: [Host source](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs)
  * The host starts lifecycle phases and hosted services before
    `ApplicationStarted`; a cancelled worker is non-error only after stopping
    has been requested.

### Project Conventions

* `.github/copilot-instructions.md:22-29,45-47,67-75`
  * .NET recommendations require Microsoft Learn grounding and tests use the
    `MethodName_StateUnderTest_ExpectedResult` convention with documentation.
* `.github/instructions/csharp.instructions.md`
  * Exception handling must be intentional and must not broadly swallow
    exceptions.

## Key Discoveries

### Reproduced Root Cause

The application was started from
`src\TNC.Trading.Platform.AppHost` with:

```text
aspire start --non-interactive
```

Aspire resource telemetry reported the API as `Finished` with exit code
`-532462766`, the CLR unhandled-exception status. Its console log shows the
first fault:

```text
System.InvalidOperationException:
Body was inferred but the method does not allow inferred body parameters.

Parameter | Source
service   | Body (Inferred)
```

The fault occurs in `RequestDelegateFactory` while
`GenericWebHostService.StartAsync` builds mapped endpoints. It is followed by
`Hosting failed to start`, then cancellation exceptions from
`PlatformAuthenticationSupervisor.ExecuteAsync` and
`OperationalRecordRetentionService.ExecuteAsync`, then
`Application is shutting down`.

`Program.cs` invokes the active `AddPlatformInfrastructure` extension from the
`Infrastructure.DependencyInjection` namespace. That extension omits the
catalog interface registration, whereas the duplicate extension in the
`Infrastructure.Platform` namespace includes:

```csharp
services.AddScoped<IBrokerEnvironmentCatalogService, SqlBrokerEnvironmentCatalogService>();
```

The active extension must receive that registration. Adding `[FromServices]`
to the endpoint parameters is not a root fix: it would defer the problem to
runtime service resolution while leaving the required dependency absent.

### Execution Flow

```text
Program.cs
  -> AddPlatformInfrastructure(...)
     -> AddScoped<OperationalRecordRetentionProcessor>()
     -> AddHostedService<OperationalRecordRetentionService>()
  -> InitializePlatformAsync(CancellationToken.None)
     -> processor.ApplyAsync(...)                 // one-off startup cleanup
  -> app.RunAsync()
     -> ExecuteAsync(stoppingToken)
        -> processor.ApplyAsync(stoppingToken)
        -> Task.Delay(one hour, stoppingToken)    // reported line 31
```

`Retention:OperationalRecordsDays` defines how old a record must be before
deletion. It is not a periodic-execution setting, so changing it cannot
resolve a cancellation from the scheduled delay.

### Behavior Classification

| Event | Current behavior | Classification |
|---|---|---|
| Confirmed host stop while processor work is executing | The filtered cancellation handler returns normally | Expected and handled |
| Confirmed host stop during the one-hour delay | `Task.Delay` throws outside the cancellation handler | Expected downstream cancellation, but handled inconsistently |
| Startup stops after another hosted service faults or is cancelled | The worker's delay is cancelled as the host tears down | Secondary symptom; find the preceding failure |
| AppHost, debugger, launcher, or external process stops/restarts the API | The worker's delay is cancelled as the process shuts down | Secondary symptom; verify external stop evidence |
| Processor throws without the host stopping | The error is redacted, logged, and retried after the delay | Actual retention failure |
| A cancellation exception occurs while the stopping token is not cancelled | It reaches general failure handling | Potentially abnormal; preserve visibility |

### AppHost Log Correlation

The latest supplied AppHost log,
`C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log`,
narrows the investigation:

| Time | Log lines | Event | Finding |
|---|---|---|---|
| 10:17:52.906 | 282 | API enters `Starting` | Beginning of the API lifetime |
| 10:17:54.566 | 283,286 | API reaches `Running`/ready | The API was ready before it exited |
| 10:17:54.576 | 291,293 | Distributed application startup completes | The AppHost startup sequence completed |
| 10:17:59.719 | 303-304 | API changes from `Running` to `Finished` | The API stopped independently of the later CLI termination |
| 10:18:52.039-10:18:52.199 | 313 | CLI/AppHost shutdown | Occurred over 52 seconds after the API had finished; cannot explain its exit |

The AppHost log therefore **does not support an AppHost startup failure as the
trigger**. It does support the theory that the API host stopped, which would
cancel the retention worker's token, but it does not record the API exit
reason, first API exception, or the timestamp of the retention exception.
An inspection of the supplied Aspire log directory found no API-process
stdout/stderr or structured API log for this run. The missing decisive evidence
is the API's ordered stdout/stderr or structured logs, API exit code/reason,
`ApplicationStarted`/`ApplicationStopping` records, and retention-cancellation
timestamp from 10:17:52.906 through 10:17:59.719.

### Aspire MCP Availability Check

On 2026-09-22, the Aspire MCP server's AppHost discovery reported no active
AppHosts in or outside the workspace. Aspire's monitoring backchannel is
available only while an AppHost is running, so console logs, structured logs,
and traces for this already-exited run cannot be recovered through Aspire MCP.

For the next reproduction, keep the AppHost running while collecting
telemetry. Query the active API resource in this order:

1. Resource state and endpoints.
2. Structured API logs.
3. API console output.
4. Traces correlated to the failing startup interval.

Capture this output before stopping the AppHost, together with the debugger's
first-chance exception and process exit details. This will provide the
process-level evidence absent from the CLI log.

## Technical Scenarios

### Diagnosing Startup-Time Cancellation

**Requirements:**

* Identify the first fault or stop request rather than classifying the final
  cancellation as the cause.
* Distinguish a host stop already in progress from an unrelated cancellation.
* Preserve the original exception and its inner-exception chain.

#### Selected Diagnostic Procedure

Capture the API process's ordered stdout/stderr and structured logs from
10:17:52.906 through 10:17:59.719, then correlate them with the supplied
AppHost log and Visual Studio Debug Output. Record the result of explicit
Program initialization, hosted-service startup entry/exit,
`ApplicationStarted`, the first fault/cancellation/resource event,
`ApplicationStopping`, `ApplicationStopped`, and process exit. Preserve the
first exception and its complete inner-exception chain.

Temporarily enable these effective launch-configuration categories:

```text
Microsoft.Hosting.Lifetime = Information
Microsoft.Extensions.Hosting.Internal.Host = Debug
TNC.Trading.Platform.Api.Hosting = Debug
```

At a debugger break on the thrown cancellation, inspect:

```csharp
stoppingToken.IsCancellationRequested
applicationLifetime.ApplicationStopping.IsCancellationRequested
applicationLifetime.ApplicationStarted.IsCancellationRequested
```

Set breakpoints at
`OperationalRecordRetentionService.cs:18,31` and
`PlatformAuthenticationSupervisor.cs:34,40,53`. If the stopping token and
`ApplicationStopping` are both true, line 31 is expected downstream
cancellation; investigate the earliest preceding stop/fault. If the stopping
token is true while `ApplicationStopping` is false, retain all host and CTS
ownership evidence because normal graceful shutdown does not explain it.

### Handling a Confirmed Host Cancellation During the Periodic Wait

**Requirements:**

* Stop promptly when the host requests shutdown.
* Do not log a normal host-stop cancellation as a retention failure.
* Continue to log and retry non-cancellation retention failures.
* Do not hide a cancellation unrelated to the service's stopping token.

#### Selected Lifecycle-Hardening Approach

Extend the existing narrow
`OperationCanceledException when (stoppingToken.IsCancellationRequested)`
boundary so that it includes both processor execution and the token-aware
scheduled delay. When that specific token has been cancelled, exit the worker
normally. Retain `Task.Delay(..., stoppingToken)` and retain general-exception
logging/retry behavior.

The implementation may use an outer filtered `try`/`catch` around the loop or
move the delay inside the existing filtered boundary. The essential invariant
is that the await at
`OperationalRecordRetentionService.cs:31` is covered by the same filter as
processor cancellation:

```csharp
catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
{
    // Normal graceful host shutdown.
}
```

This approach directly handles the verified cancellation point after a stop is
confirmed, preserves prompt shutdown, and does not reclassify unrelated
cancellation exceptions. It does not repair a startup failure that caused the
host to stop.

#### Regression Tests

Add deterministic lifecycle tests without a real one-hour wait. A minimal
delay/scheduling seam (such as an injected `TimeProvider` or test scheduler)
may be introduced only if necessary.

1. `ExecuteAsync_ShouldCompleteNormally_WhenStoppingTokenCancelsDuringDelay`
   should complete the initial processor invocation, cancel the token while
   the service is waiting, and assert normal task completion.
2. `ExecuteAsync_ShouldCompleteNormally_WhenStoppingTokenCancelsProcessor`
   should block processor work until cancellation, then assert the same normal
   result.
3. `ExecuteAsync_ShouldLogAndContinue_WhenProcessorThrowsNonCancellationException`
   should demonstrate that a genuine processing exception still follows the
   error-and-retry path.

Fully document the tests per repository convention and ensure a cancellation
from any token other than `stoppingToken` is not silently treated as graceful
shutdown.

#### Considered Alternatives

* **Remove the stopping token from `Task.Delay`: rejected.** The service could
  delay shutdown by up to one hour and risk exceeding the host shutdown
  timeout.
* **Catch every `OperationCanceledException`: rejected.** It would conceal a
  provider timeout or independently-cancelled dependency while the host is
  still running.
* **Change `HostOptions`, logging filters, or retention days: rejected.**
  None covers the throwing await; these options hide or defer the symptom
  rather than correcting the inconsistent lifecycle handling.

## Recommendation and Implementation Impact

1. In the active
   `Infrastructure.DependencyInjection.PlatformInfrastructureServiceCollectionExtensions`
   method, add the missing scoped registration:

   ```csharp
   services.AddScoped<IBrokerEnvironmentCatalogService, SqlBrokerEnvironmentCatalogService>();
   ```

   Include the existing Broker Environments application namespace import.
2. Add a regression test that starts/builds the API host endpoint pipeline and
   proves the broker-environment `GET` endpoint can be constructed. A test
   that only resolves the service collection is insufficient because the
   reported failure occurs during endpoint-pipeline construction.
3. Re-run the AppHost and verify the API stays `Running`, the
   `Body (Inferred)` error is absent, and no retention cancellation is emitted
   during startup.
4. Separately apply the narrow retention lifecycle hardening change so a
   confirmed host stop does not report the periodic-delay cancellation as an
   independent background-service failure. Preserve non-cancellation error
   logging/retry and unrelated cancellation visibility.
5. Schedule a focused refactoring of the duplicate Infrastructure registration
   extensions. Do not replace the active extension with the duplicate now: it
   lacks registrations present in the active composition root and would create
   further regressions.

## Potential Next Research

* Confirm the concrete testability seam after selecting the unit-test project
  and its existing fake-time utilities.
  * Reasoning: The current hard-coded one-hour delay must not produce
    slow/flaky tests.
  * Reference:
    `OperationalRecordRetentionService.cs:31`
