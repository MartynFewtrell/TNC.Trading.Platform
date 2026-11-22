<!-- markdownlint-disable-file -->
# Task Research: Operational retention cancellation during startup

## Task implementation requests

* Reassess the `TaskCanceledException` at `OperationalRecordRetentionService.ExecuteAsync` line 31 using the new fact that it occurred while the application was starting, rather than during an intentional user shutdown.
* Trace the actual application, Generic Host, Aspire/AppHost, debugger, configuration, and failure lifecycles.
* Identify what can cancel the `BackgroundService` stopping token during startup, what evidence distinguishes those cases, and precisely revise the prior recommendation.
* This report is research only. No product or test source was changed.

## Scope and success criteria

* **Scope:** The current `net10.0` API host, retention worker and registration, explicit pre-host initialization, other hosted services, AppHost resource graph, configuration/launch behavior, and authoritative Microsoft/.NET Aspire documentation.
* **Excluded:** Product changes and test changes. Archived Aspire logs were inspected read-only; API-process stdout/stderr, debugger output, dashboard resource-detail history, and process-exit diagnostics were not available in the inspected log root.
* **Assumptions:** “During startup” is evaluated from the available AppHost resource timeline. The API reached AppHost `Running` and the distributed application reported started, but no API-side `IHostApplicationLifetime.ApplicationStarted` record was available; those facts must not be conflated.
* **Success criteria:**
  * Explain exactly why line 31 throws and distinguish the supplied token from unrelated cancellation tokens.
  * Establish whether a cancellation at that location during startup is normal, a startup failure, or inconclusive from the exception alone.
  * Give observable proof/disproof criteria for each plausible source of cancellation.
  * Provide one evidence-based diagnostic and implementation recommendation without suppressing real failures.

## Executive conclusion — revised

**The previous conclusion is no longer sufficient.** A `TaskCanceledException` at `OperationalRecordRetentionService.ExecuteAsync` line 31 is mechanically consistent with a host stop, because the line awaits `Task.Delay(..., stoppingToken)`. However, the new timing means it **must not be classified as an expected graceful shutdown unless host-stop evidence is present**.

`BackgroundService.StartAsync` starts `ExecuteAsync` asynchronously and normally returns immediately. Thus the retention service can reach its initial processor run and one-hour delay *while the host is still starting other hosted services*. If a later hosted-service startup fault, an externally cancelled host-start token, AppHost/IDE teardown or restart, process termination, or another application-requested stop occurs during that window, the Generic Host calls `BackgroundService.StopAsync`, cancels its internal stopping CTS, and line 31 throws. In that situation the retention exception is a **downstream cancellation symptom**; it is not the startup root cause and must not be “fixed” by treating it as successful retention completion.

There remains one narrow, valid service-local improvement: use a cancellation filter covering both processor work and the delay, **but only** for `OperationCanceledException` when the supplied `stoppingToken.IsCancellationRequested`. That removes inconsistent canceled-task reporting during an already-confirmed stop. It does **not** resolve the startup incident. The first failure, first stop request, and lifecycle event ordering must be captured before changing production behavior.

## AppHost log follow-up — complete ordered lifecycle evidence

**Evidence sources (absolute paths):**

* `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log`
* `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101752_eb17e349.log`

The bounded inspection of `C:\Users\martynfewtrell\.aspire\logs` found no separate API-named stdout, stderr, resource, or structured-log file. The first source above is therefore the only located source of bounded API resource lifecycle evidence. A complete text search of that log root found no `OperationalRecordRetentionService`, `TaskCanceledException`, `OperationCanceledException`, `Unhandled exception`, `ApplicationStopping`, `ApplicationStopped`, or `Application is shutting down` entry. The available evidence does **not** contain the API process exit reason or exit code, an API exception, an API-side host-lifetime stopping record, or a timestamp for the retention exception. Its line numbers below are physical lines in the stated log file.

| Order | Timestamp | Verified event | Exact evidence | Correlation and significance |
| --- | --- | --- | --- | --- |
| 1 | 10:17:42.702 | CLI host started. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:8` | Begins the AppHost CLI session. |
| 2 | 10:17:45.644–10:17:45.677 | AppHost build completed and its build process exited `0`. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:110-115` | Rules out an AppHost build failure as the stop event. |
| 3 | 10:17:45.678–10:17:47.563 | CLI connected to AppHost and initialized dashboard setup. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:118-149` | AppHost launch/orchestration phase. |
| 4 | 10:17:47.269–10:17:47.370 | AppHost hosting and distributed application began. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:155-160,173` | Resource orchestration started. |
| 5 | 10:17:48.124–10:17:48.132 | API and web entered `Starting`/`Waiting`; Keycloak and Mailpit started. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:188-196` | The API had not yet started. |
| 6 | 10:17:48.170–10:17:48.497 | Database resources started and became ready; Mailpit and dashboard became ready. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:197-245` | Matches the documented database/Mailpit readiness prerequisites. |
| 7 | 10:17:50.714–10:17:52.765 | Keycloak ran and became ready. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:265-277` | The shown authentication dependency was ready before the API began. |
| 8 | 10:17:52.906 | API entered `Starting`. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:282` | Begins the relevant API startup window. |
| 9 | 10:17:54.566 | API entered `Running`; AppHost marked it ready. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:283-286` | The API reached the AppHost resource `Running` state. |
| 10 | 10:17:54.576 | The distributed application reported started. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:291-293` | AppHost-level startup completed. This supersedes the earlier assumption that the API had not unambiguously started. |
| 11 | 10:17:56.304 | Web entered `Running` and ready. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:296-298` | The remaining shown application resource became operational. |
| 12 | **10:17:59.719** | **AppHost observed the API resource transition `Running` → `Finished`; AppHost stopped API health monitoring.** | **`C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:303-304`** | **Earliest observed adverse API lifecycle event. The transition reason is unrecorded. It occurred 5.143 seconds after distributed-application startup and 7.094 seconds after API `Starting`.** |
| 13 | 10:18:52.038–10:18:52.039 | Backchannel disconnected; CLI exited `0`; CLI host stopped. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:305-312` | Occurred 52.319 seconds after the API finished; the `0` is the CLI exit code, not an API process result, and these later events cannot explain the earlier resource transition. |
| 14 | 10:18:52.199 | CLI received a termination signal and requested cancellation. | `C:\Users\martynfewtrell\.aspire\logs\cli_20260922T101742_87a4caa9.log:313` | Also later than the API stop, so it is not the initiating cancellation event. |

### Revised diagnosis from the ordered evidence

The earliest verified relevant event is AppHost observing the API resource transition to `Finished` at **10:17:59.719** (log lines **303-304**), after both the API and distributed application had reached their AppHost `Running`/started states. The retention worker's reported `TaskCanceledException` at `OperationalRecordRetentionService.ExecuteAsync` line 31 is **mechanically consistent with, but not causally attributable to, API-host shutdown**: the documented `BackgroundService.StopAsync` path cancels the worker token, which cancels the token-aware `Task.Delay`.

The evidence does **not** prove that causal link, because the retention cancellation timestamp, API `ApplicationStopping` record, API exception, API exit code, and transition reason are absent. It also does not prove a normal user/CLI shutdown: the only logged CLI termination/cancellation event is at 10:18:52.199, after the API had already finished. The retained worker exception is consequently **inconclusive and not an identified startup root cause**. The available ordering establishes only that the distributed application had reported started before AppHost observed the API transition to `Finished`; it does not establish why that process ended.

### Focused follow-up recommendations

1. Obtain the API process stdout/stderr, debugger Output, dashboard/resource-detail history, process exit code, and API `Microsoft.Hosting.Lifetime` events for the inclusive interval **2026-09-22 10:17:52.906–10:17:59.719**. Identify the first exception, explicit stop request, exit code, or termination reason. This is the decisive unobserved interval.
2. Obtain the exact retention exception timestamp and full stack/inner-exception chain, then correlate it with `ApplicationStarted`, `ApplicationStopping`, `stoppingToken.IsCancellationRequested`, and `ApplicationStopping.IsCancellationRequested`. Classify it as expected downstream cancellation only when the stop state is already requested.
3. Prioritize the API’s first stop/exit cause over retention scheduling changes. The existing `PlatformAuthenticationSupervisor.StartAsync` initial awaited tick remains a startup-sensitive path to inspect, but the AppHost log alone does not implicate it.
4. Do not broadly catch or suppress cancellation. Retain the existing recommended narrow filter only for `OperationCanceledException` where the worker's supplied `stoppingToken.IsCancellationRequested` is true, after lifecycle correlation is established.

## Evidence log

| Evidence | Verified finding | Relevance |
| --- | --- | --- |
| `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionService.cs:8-34` | The retention worker derives from `BackgroundService`, passes its host token to processor work, and awaits a one-hour `Task.Delay` with that token at line 31. Its existing filtered `OperationCanceledException` handling covers processor work but not the delay. | The reported await throws when its supplied token is cancelled. |
| `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:88-90` | The processor is scoped and the service is registered through `AddHostedService`. | The Generic Host—not an application caller—owns service start/stop and the worker token. |
| `src/TNC.Trading.Platform.Api/Program.cs:43-45` | The API registers `PlatformAuthenticationSupervisor` and `AccountPreferencesReconciliationSupervisor` as hosted services. | These startup participants can fail or be stopped while the retention service has already begun asynchronously. |
| `src/TNC.Trading.Platform.Api/Program.cs:47-61,103-105` | Explicit initialization occurs before `app.RunAsync()`. Its error path logs `Fatal error during startup scope; the application will not start.` and rethrows. | This is a separate, pre-host startup-failure domain. It uses `CancellationToken.None`, so it cannot directly cancel the retention worker token. |
| `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:32-42,50-55` | `StartAsync` awaits `RunSingleTickAsync(cancellationToken)` before calling `base.StartAsync`; its recurring loop handles cancellation only when its worker token is cancelled. | The initial authentication tick is a genuine, host-blocking startup operation, unlike ordinary `BackgroundService.ExecuteAsync`. A cancellation/fault here can abort hosted-service startup. |
| `src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs:21-33` | This worker yields into its loop, uses its stopping token for delay, and handles expected cancellation. | It is a normal asynchronous worker; cancellation during a confirmed stop is expected. |
| `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:21-27`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupExtensions.cs:14-18` | Startup initialization invokes the retention processor once before the host runs, through an awaited async scope. | The startup call is distinct from the worker. A line-31 stack frame cannot be this invocation. |
| `src/TNC.Trading.Platform.Api/Program.cs:49-53` | The pre-host initialization path uses `CancellationToken.None`. | It rules out ordinary host cancellation as the token source for that pre-host retention execution. |
| `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-14` | AppHost builds and runs the distributed application. | AppHost owns local orchestration and can stop/restart child resource processes. |
| `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:13-24,38-47` | The API has `WithReference(platformDatabase)`, `WaitFor(platformDatabase)`, and `WaitFor(mailpit)`; the web project waits for the API. No Keycloak `WaitFor` appears in this registration method. | SQL Server and Mailpit are explicit readiness gates. Authentication dependencies can still be unavailable to the custom initial authentication tick unless another registration supplies a gate. |
| `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:21-52` | SQL Server, Mailpit, and Keycloak are resource registrations with configured lifetimes. | Their AppHost resource events are required evidence when deciding whether orchestration stopped the API. |
| `src/TNC.Trading.Platform.Api/appsettings.json:36-38`; `src/TNC.Trading.Platform.Application/Services/OperationalRecordRetentionPolicy.cs:5-23`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:15-113` | `Retention:OperationalRecordsDays` controls deletion eligibility (default/fallback 90 days); it does not configure the one-hour cadence. All EF work receives the passed token. | Retention-days configuration cannot cause line-31 cancellation. Database work can be cancelled by the same worker token. |
| `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:17-21`; `global.json:2-5` | The application targets `net10.0` and selects SDK `10.0.302`. | The cited .NET 10 API/source behavior is the relevant runtime contract. |
| `.github/copilot-instructions.md:22-29,45-47,67-75` | The repository requires Microsoft Learn grounding, line evidence, documented tests, and `MethodName_StateUnderTest_ExpectedResult` test naming. | Applied to recommendations; no tests are added by this research. |
| [dotnet/runtime `BackgroundService.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting.Abstractions/src/BackgroundService.cs) | `StartAsync` creates a linked stopping CTS, queues `ExecuteAsync`, and normally returns `Task.CompletedTask`; `StopAsync` cancels that CTS. | Explains how a host stop during startup reaches line 31. |
| [dotnet/runtime `Host.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs) | Effective startup is lifetime wait, validation, lifecycle `StartingAsync`, hosted-service `StartAsync`, lifecycle `StartedAsync`, then `ApplicationStarted`. It uses `HostOptions.ServicesStartConcurrently`; it suppresses an already-stopping cancelled background task as an error. | Defines startup ordering, concurrent-start behavior, and the exact normal-stop distinction. |
| [Microsoft Learn: Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host) | Documents hosted-service startup and shutdown lifecycle. | Authoritative lifecycle context and expected `Microsoft.Hosting.Lifetime` records. |
| [Microsoft Learn: Worker services](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers) and [`BackgroundService.ExecuteAsync`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice.executeasync?view=net-10.0-pp) | The stopping token is triggered by `StopAsync`; `ExecuteAsync` represents the long-running operation. | Confirms normal use of a token-aware delay and narrowly filtered cancellation handling. |
| [Microsoft Learn: `HostOptions.ServicesStartConcurrently`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.hostoptions.servicesstartconcurrently) and [`BackgroundServiceExceptionBehavior`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.hostoptions.backgroundserviceexceptionbehavior) | Hosted-service start concurrency and unexpected background-task fault behavior are host options. | Required configuration audit points; no repository evidence currently establishes an override. |
| [Microsoft Learn: .NET Aspire AppHost overview](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview) and [Aspire resource lifetimes](https://aspire.dev/app-host/resource-lifetimes/) | AppHost defines resources/relationships; a `WaitFor` relationship waits for the named resource’s readiness. | Differentiates a verified readiness edge from a mere reference/configuration relationship. |
| [Microsoft Learn: Stop Debugging](https://learn.microsoft.com/en-us/visualstudio/ide/reference/debug-stop-debugging?view=vs-2022) | Stopping an F5 debug session ends the launched application process; restart tears down the old process first. | A Visual Studio Stop/Restart can legitimately produce the observed downstream cancellation. An attach session has different ownership. |

## Actual startup and cancellation flow

```text
AppHost (when used)
  └─ starts API after explicit WaitFor database and Mailpit readiness
       └─ Program pre-host initialization (CancellationToken.None)
            └─ initial retention processor ApplyAsync                 [not line 31]
       └─ app.RunAsync()
            └─ Generic Host starts registered IHostedService instances
                 ├─ BackgroundService.StartAsync queues ExecuteAsync
                 │    └─ retention ExecuteAsync(stoppingToken)
                 │         ├─ processor.ApplyAsync(stoppingToken)
                 │         └─ Task.Delay(1 hour, stoppingToken)     [reported line 31]
                 └─ PlatformAuthenticationSupervisor.StartAsync
                      └─ awaited initial RunSingleTickAsync(host start token)
```

### Why the reported event can occur before startup completes

For the base `BackgroundService`, the `ExecuteAsync` task is started independently and `StartAsync` returns immediately. Therefore “line 31 occurred during startup” does **not** mean retention startup itself failed. It can mean:

1. the retention worker made its first pass and began waiting;
2. the host was still executing a later service’s `StartAsync` or lifecycle phase;
3. a stop/failure path began; and
4. `StopAsync` cancelled the retention service’s linked stopping CTS, completing its delay as cancelled.

The host source distinguishes a cancelled background task while `ApplicationStopping` is already requested from an error condition. That distinction is unavailable from the exception type/stack frame alone.

### Hosted-service ordering and options

* In the absence of an override, `HostOptions.ServicesStartConcurrently` defaults to `false`; service start is sequential in registration order. An exception escaping a `StartAsync` then aborts startup before later services are started.
* If configuration sets `ServicesStartConcurrently=true`, starts overlap and exceptions are collected/observed after the starts; it changes timing, not the fact that an escaping `StartAsync` can fail startup.
* Search the effective launch configuration and environment-specific configuration for `HostOptions`, `ServicesStartConcurrently`, `ServicesStopConcurrently`, `ShutdownTimeout`, and `BackgroundServiceExceptionBehavior`. There is no verified repository evidence in this research of an override; do not assert defaults from absence until the deployed configuration is inspected.
* `ShutdownTimeout` bounds shutdown waiting. It is not a cancellation source for `ExecuteAsync` at startup and increasing it cannot cure a root startup failure.

## Plausible cancellation sources during startup and proof matrix

| Scenario | Mechanism that cancels/ends the retention worker | What proves it | What disproves it / required response |
| --- | --- | --- | --- |
| **Intentional VS/AppHost stop or restart** | VS terminates the F5-launched AppHost/API, or AppHost begins teardown; Generic Host invokes worker stop and cancels the token. | VS Debug Output shows Stop/Restart; AppHost resource events show API stopping; API has `ApplicationStopping`/`Application is shutting down` before or with line 31. | No user/tool stop and no stopping event: do not call it intentional shutdown. |
| **External process/container/orchestrator termination** | Ctrl+C, SIGTERM, Docker/launcher teardown, IDE resource restart, or process-management action asks the host to stop. | Process/container event and host lifetime stop records precede exception; exit code/reason identifies external termination. | No termination event means investigate host-start cancellation and preceding exception. |
| **Another hosted service fails startup** | An exception/cancellation escapes a custom `StartAsync`, especially `PlatformAuthenticationSupervisor.StartAsync` before base start; host aborts startup and stops already started workers. | Earliest error is `PlatformAuthenticationSupervisor` initial tick or host “failed to start/hosted service failed” record; `ApplicationStarted` never occurs; retention cancellation follows. | Retention’s cancellation is not the root cause. Fix the earliest dependency/auth/configuration error, not retention delay handling. |
| **Host-start token is externally cancelled** | The token passed into hosted-service startup is cancelled by a caller/lifetime integration before startup completes. A custom pre-base `StartAsync` using that token can throw; host then stops started workers. | Break/log at `PlatformAuthenticationSupervisor.StartAsync` shows its `cancellationToken.IsCancellationRequested`; host lifetime logs or AppHost caller supplies cancellation; no independent service error precedes it. | If start token is not cancelled, find the first non-cancellation failure instead. |
| **AppHost dependency/readiness failure or rapid resource restart** | API may be stopped after a resource failure/reconfiguration; additionally, Keycloak is not proven as an API `WaitFor` dependency in the inspected registration. | AppHost dashboard/resource logs show dependency health transition, restart, or API stop; auth tick logs connection/auth failure before cancellation. | SQL/Mailpit `WaitFor` only establish their own readiness. Do not infer Keycloak readiness from them. |
| **Explicit Program initialization fails** | Pre-`RunAsync` initialization rethrows and application never begins normal hosting. | `Fatal error during startup scope; the application will not start.` is logged. | This path uses `CancellationToken.None` and cannot itself create a line-31 retention-worker exception; if both appear, establish ordering rather than joining them causally. |
| **Retention processor/database operation is cancelled** | The same worker stopping token is passed into EF calls; once stopping begins, database work can also throw cancellation. | `stoppingToken.IsCancellationRequested` and lifecycle stopping event are true; SQL/EF cancellation follows stop. | Database timeout/provider cancellation while the worker token is false is an independent fault; preserve it as diagnostic data. |
| **Unexpected retention background task fault after start** | An exception not attributable to host stopping faults the execution task. Depending on `BackgroundServiceExceptionBehavior`, the host can then request stop. | `ApplicationStarted` preceded failure, no stopping signal preceded it, and first retention exception is non-cancellation or unrelated-token cancellation. | Do not mask it with a broad cancellation catch; inspect root exception and effective `BackgroundServiceExceptionBehavior`. |

## Observable diagnostic procedure (selected approach)

### 1. Correlate events in one timestamped timeline

Collect API logs from process start through exit, AppHost resource events/logs, and Visual Studio Debug Output. Preserve the **first** exception and complete inner-exception chain, not only the final `TaskCanceledException`.

Record in this order:

1. process/API start and AppHost resource state;
2. explicit Program initialization result;
3. each hosted-service startup entry/exit;
4. `ApplicationStarted`, if it occurs;
5. first fault, cancellation, or dependency/readiness event;
6. `ApplicationStopping`, `ApplicationStopped`, and process exit.

The decisive fact is whether application stopping is already requested when line 31 is cancelled, and what precedes that request.

### 2. Enable focused, temporary logging

Use the effective launch configuration (not a source change) to enable:

```text
Microsoft.Hosting.Lifetime = Information
Microsoft.Extensions.Hosting.Internal.Host = Debug
TNC.Trading.Platform.Api.Hosting = Debug
```

Also enable the category used by the platform-authentication reconciliation/client. Capture messages that identify:

* `Application is shutting down`;
* hosted-service startup failure/aborted-start messages;
* the project’s `Fatal error during startup scope; the application will not start.`;
* SQL/Mailpit/Keycloak resource health, restart, connection, credential, or readiness failures;
* the source and state of an authentication initial-tick cancellation.

### 3. Use targeted debugger observations

Temporarily break on thrown `System.OperationCanceledException` and `System.Threading.Tasks.TaskCanceledException`, then inspect:

```csharp
stoppingToken.IsCancellationRequested
applicationLifetime.ApplicationStopping.IsCancellationRequested
applicationLifetime.ApplicationStarted.IsCancellationRequested
```

Set breakpoints at:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionService.cs:18,31`;
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:34,40,53`;
* `src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs:31`.

At the retention delay:

* stopping token **true** + application stopping **true**: expected downstream cancellation; diagnose what requested stop if it was not intentional;
* stopping token **true** + application stopping **false**: unusual; collect host/CTS ownership evidence, because the ordinary host contract does not explain it as graceful stop;
* stopping token **false**: the reported cancellation is not the delay cancellation implied by this stack frame alone—inspect another token/inner exception/source.

## Evaluated alternatives

### Rejected: retain the former “expected host shutdown” classification

It was reasonable only under the former assumption that a user initiated shutdown. The new timing removes that assumption. The Generic Host source expressly makes cancellation non-error only when `ApplicationStopping` has already been requested. A startup-time cancellation absent that evidence can be the result of a startup failure, external cancellation, or orchestration teardown.

### Rejected: remove the token from `Task.Delay`

```csharp
await Task.Delay(TimeSpan.FromHours(1)).ConfigureAwait(false);
```

This would conceal the stack frame by delaying worker shutdown for up to an hour. It conflicts with the `BackgroundService` stopping-token contract and risks exceeding `HostOptions.ShutdownTimeout`; it does not explain why startup was stopped.

### Rejected: catch every `TaskCanceledException` / `OperationCanceledException`

```csharp
catch (OperationCanceledException)
{
    return;
}
```

This hides provider, timeout, or independently cancelled-operation failures while the application is still running. It would erase the evidence needed to distinguish the current incident. The filter must remain tied to `stoppingToken.IsCancellationRequested`.

### Rejected: tune retention days, HostOptions, logging filters, or AppHost waits as the first response

Retention days only affect the deletion cutoff. Logging filters discard evidence. `ShutdownTimeout` changes stop waiting but not the initial source of a stop. Adding an AppHost `WaitFor` without proof of the missing dependency/relation could serialize startup unnecessarily and does not correct a bad startup token or service exception. These are follow-up changes only if the ordered diagnostic evidence identifies the relevant cause.

## Recommended implementation impact

1. **First perform the selected correlation procedure.** Treat the incident as an unresolved startup-stop sequence until logs establish `ApplicationStopping`, the first fault, and the process/AppHost/debugger stop initiator.
2. **If a startup fault is found, repair the first failure.** The most startup-sensitive verified code path is `PlatformAuthenticationSupervisor.StartAsync` because it awaits a real initial reconciliation before base `BackgroundService.StartAsync`. Check its dependency readiness and the cancellation token supplied by the host; do not attribute that failure to retention.
3. **Make only the narrow retention lifecycle hardening change after classification:** place both `processor.ApplyAsync(stoppingToken)` and `Task.Delay(..., stoppingToken)` under `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)`. This has one purpose: allow expected, already-confirmed host stopping to complete the worker normally.
4. **Do not broaden suppression.** Any cancellation while the host is not stopping, and every non-cancellation exception, must remain logged and actionable.
5. **If code changes are later authorized, add deterministic lifecycle tests** named according to repository convention:
   * `ExecuteAsync_StoppingTokenCancelsDuringDelay_CompletesNormally`;
   * `ExecuteAsync_StoppingTokenCancelsProcessor_CompletesNormally`;
   * `ExecuteAsync_UnrelatedCancellationOccurs_LogsAsFailure`;
   * a host-level test showing a subsequent hosted-service `StartAsync` failure stops an already-started retention worker without misclassifying it as the root failure.

## Final classification guide

| Observation | Classification |
| --- | --- |
| `ApplicationStopping`/AppHost stop/VS Stop or Restart precedes line 31 cancellation | Expected downstream worker cancellation. |
| `ApplicationStarted` never occurs and another service’s `StartAsync` fault/cancellation precedes line 31 | Startup failure; retention cancellation is secondary. |
| The project’s critical pre-host startup message precedes termination | Explicit Program initialization failure; separately investigate its root exception. |
| No stop event, worker token is false, or an unrelated token/provider timeout caused cancellation | Unexpected fault; do not suppress. |
| Full timeline unavailable | **Inconclusive**. The new startup timing prohibits a normal-shutdown conclusion. |

## Research status

* **Selected approach:** classify by correlated host lifetime, AppHost/debugger, and first-failure evidence; then apply only narrowly filtered worker cancellation handling.
* **Critical discoveries:** 8 — the retention await is token-cancellable; base `BackgroundService` starts asynchronously; a custom auth service has an awaited pre-base startup tick; pre-host initialization is separate and uncancellable by host stopping; AppHost has explicit SQL/Mailpit but no verified Keycloak gate; stop/restart can cancel the worker; configuration may alter hosted-service timing; normal cancellation requires an actual stopping signal.
* **Alternatives evaluated:** 4 rejected alternatives plus the superseded former classification.
* **Outstanding evidence:** API stdout/stderr, debugger output, dashboard/resource-detail history, API process exit code/reason, API `Microsoft.Hosting.Lifetime` records, and the exact line-31 cancellation timestamp/stack/token states for `2026-09-22 10:17:52.906–10:17:59.719`; also effective runtime `HostOptions`/launch configuration.
