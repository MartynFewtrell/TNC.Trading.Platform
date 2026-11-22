---
title: Web Disposal Lifecycle Blocker Research
description: Evidence for bUnit root disposal during suspended asynchronous component initialization
ms.date: 2026-08-30
ms.topic: troubleshooting
---

## Status

Complete. The blocker is a test-harness API mismatch, not a production cancellation defect.

## Research question

Determine whether disposing the rendered root component in the resolved bUnit version invokes
`IDisposable.Dispose` while `OnInitializedAsync` is suspended on an HTTP content operation.
Identify the smallest deterministic test-harness correction that proves cancellation reaches the
HTTP operation without a production change. If that lifecycle cannot be exercised through bUnit,
identify the nearest valid test boundary and whether production task ownership must change.

## Current hypothesis

The initial hypothesis was partly false. bUnit returns a rendered component handle after the first
render while `OnInitializedAsync` remains suspended. The test can request root teardown at that
point, but `cut.Dispose()` does not request root teardown. It disposes only bUnit's rendered-fragment
wrapper.

## Exact lifecycle finding

The resolved packages are `bunit.web` 1.40.0 and `bunit.core` 1.40.0. The Web unit-test project
targets `net10.0` and resolves the packages' `net9.0` assemblies.

`context.RenderComponent<AccountPreferences>()` returns after the first render even though the
component's `OnInitializedAsync` is awaiting HTTP content. bUnit's own `TestRendererTest.Test100`
specifically verifies this incomplete-initialization behavior.

`await cut.InvokeAsync(cut.Dispose)` invokes `RenderedFragment.Dispose()` on the renderer dispatcher.
That method only sets `IsDisposed`, clears its cached markup, and removes wrapper event handlers. It
does not detach the bUnit root and does not invoke `AccountPreferences.IDisposable.Dispose()`.
Consequently, the component-owned token is not cancelled and the current fact times out while
awaiting `DelayedJsonContent.CancellationObserved`.

The installed component-teardown API is `TestContextBase.DisposeComponents()`. It calls
`ITestRenderer.DisposeComponents()`. In bUnit 1.40.0, `TestRenderer.DisposeComponents()` invokes
`RootComponent.Detach()` on the renderer dispatcher. `Detach()` renders an empty root fragment,
which removes the component tree and causes Blazor to invoke component disposal even while the
asynchronous initialization task remains suspended.

## Evidence

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:5,15`
	targets `net10.0` and references `bunit.web` 1.40.0.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/obj/project.assets.json:87-116,1615-1665`
	resolves `bunit.core` and `bunit.web` 1.40.0 with their `net9.0` assemblies.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:67-90`
	calls `cut.Dispose()` through `InvokeAsync`, then waits for content cancellation. The focused fact
	failed at the cancellation wait on line 84 with `System.TimeoutException`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:150-190`
	already overrides the cancellation-token serialization and stream creation paths. This rules out
	the previous content-overload hypothesis as the remaining blocker.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:28-44`
	owns the request-lifetime source, passes its token to initial load, and cancels it only from the
	component's `Dispose()` method.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:10-27`
	treats owned cancellation as control flow and resets `IsLoading` in `finally`.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:136-166` passes the same token to both
	`HttpClient.SendAsync` and `ReadFromJsonAsync`.
* bUnit v1.40.0 `RenderedFragment.Dispose()` only disposes the rendered wrapper. See
	[RenderedFragment.cs](https://github.com/bUnit-dev/bUnit/blob/v1.40.0/src/bunit.web/Rendering/RenderedFragment.cs).
* bUnit v1.40.0 `TestContextBase.DisposeComponents()` delegates to the renderer. See
	[TestContextBase.cs](https://github.com/bUnit-dev/bUnit/blob/v1.40.0/src/bunit.core/TestContextBase.cs).
* bUnit v1.40.0 `TestRenderer.DisposeComponents()` detaches every root through the dispatcher. See
	[TestRenderer.cs](https://github.com/bUnit-dev/bUnit/blob/v1.40.0/src/bunit.core/Rendering/TestRenderer.cs).
* bUnit v1.40.0 `RootComponent.Detach()` renders an empty fragment. See
	[RootComponent.cs](https://github.com/bUnit-dev/bUnit/blob/v1.40.0/src/bunit.core/Rendering/RootComponent.cs).
* bUnit's disposal documentation states that context-level disposal invokes component
	`IDisposable.Dispose`, and warns that `WaitForAssertion` does not work after disposal because a
	disposed component does not produce another render cycle. See
	[Disposing components](https://bunit.dev/docs/interaction/dispose-components.html).

An isolated local probe used the resolved bUnit 1.40.0 package, a component whose
`OnInitializedAsync` awaited an HTTP content operation with its owned token, and the same
cancellation-aware `HttpContent` shape. It produced:

```text
cut.Dispose cancellation observed: False
context.DisposeComponents cancellation observed: True
```

This executing evidence confirms that real renderer-level root disposal invokes the component's
`IDisposable.Dispose()` while initialization is suspended and that cancellation reaches the HTTP
content operation. The transient probe files and build output were removed after execution.

## Recommendation for Step 9.2

Resume Step 9.2 with a test-harness-only correction. In
`Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError`, replace:

```csharp
await cut.InvokeAsync(cut.Dispose);
```

with:

```csharp
context.DisposeComponents();
```

Keep the finite await of `delayedContent.CancellationObserved` and its successful-completion
assertion. This recommendation is falsifiable: with the single disposal-call replacement, the
focused fact must observe cancellation within five seconds. Failure would disprove the local probe's
applicability to the repository's component shape.

Do not call `cut.WaitForAssertion` after `context.DisposeComponents()`. Real teardown disposes the
rendered wrapper, and bUnit 1.40.0 has no render event on which that eventual assertion can retry.
That assertion is therefore not a deterministic way to prove the presenter continuation settled.

If Step 9.2 must also retain deterministic `IsLoading == false` and `Error == null` coverage, place
those assertions at the presenter boundary in a separate focused fact. Start
`presenter.LoadAsync(cancellationToken)` directly, retain its returned task, wait until the delayed
HTTP content read starts, cancel the test-owned source, await the retained load task with a finite
timeout, and then assert `IsLoading` is false and `Error` is null. The component fact owns the
renderer-disposal-to-HTTP-cancellation contract; the presenter fact owns cancellation completion and
state cleanup.

No production task-ownership change is required for either contract. Such a change would be
justified only if a product requirement says component teardown itself must expose or await the
presenter's in-flight task. The current requirement is cancellation propagation, which the correct
bUnit disposal API exercises.

## Focused validation command

Run only the affected Web fact first:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Dispose_ShouldCancelDelayedInitialLoad_WithoutRenderingError" --verbosity minimal
```

After the fact passes once, repeat that same command ten times and stop on the first failure. Do not
run the broad suite as part of Step 9.2.

## Rejected alternatives

* Keep `cut.Dispose()` and change only `HttpContent`: rejected because `cut.Dispose()` never invokes
	component disposal. The current cancellation-aware content already demonstrates this by timing
	out.
* Wrap `cut.Dispose()` in `cut.InvokeAsync`: rejected because `InvokeAsync` only changes the thread on
	which the rendered-wrapper method runs. It does not change that method's semantics.
* Release the normal response gate after wrapper disposal: rejected because this proves successful
	completion, not cancellation propagation, and can conceal the missing component-disposal call.
* Use `cut.WaitForAssertion` after context-level disposal: rejected because the wrapper is disposed
	and no later component render can trigger a retry.
* Invoke `((IDisposable)cut.Instance).Dispose()` directly: rejected as the renderer still owns the
	component and will dispose it again during context cleanup. The production `Dispose()` method is
	not idempotent after its token source has been disposed, and bypassing renderer teardown weakens
	the lifecycle contract.
* Add sleeps, uncancellable gates, or polling loops: rejected because they do not provide an
	operation-completion boundary and make the fact timing-dependent.
* Add production tracking for the initial-load task: rejected for the current requirement because
	context-level root disposal already proves cancellation reaches the HTTP operation.

## Remaining questions

* Decide whether Step 9.2's durable requirement is only component-disposal cancellation propagation,
	or whether presenter state cleanup must remain in the same fact. The latter cannot use bUnit's
	rendered-fragment eventual assertion after real disposal and should be covered at the presenter
	task boundary.
* The test file is currently untracked rather than an uncommitted modification of a tracked file.
	Confirm that this repository state is intentional before Phase 9 changes are committed.