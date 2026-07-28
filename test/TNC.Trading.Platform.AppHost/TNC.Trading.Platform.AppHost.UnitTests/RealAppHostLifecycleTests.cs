using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.AppHost.UnitTests;

public sealed class RealAppHostLifecycleTests
{
    /// <summary>
    /// Trace: Keycloak testing improvements Phase 2, Step 2.2.
    /// Verifies: handle disposal attempts application, builder, and environment cleanup after an earlier failure.
    /// Expected: all stages run in order, the first failure is rethrown, and later failures are attached as cleanup data.
    /// Why: partial managed startup must not leak Aspire resources or environment overrides when one disposal stage fails.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ShouldAttemptAllCleanupStages_WhenApplicationDisposeFails()
    {
        var events = new List<string>();
        var application = new AsyncDisposableProbe("application", events, shouldThrow: true);
        var builder = new AsyncDisposableProbe("builder", events, shouldThrow: true);
        var environment = new DisposableProbe("environment", events, shouldThrow: true);
        var handle = new AppHostProcessHandle(
            process: null,
            existingPlatformProcessIds: [],
            existingLocalListeningPorts: [],
            launchCommand: "unit-test",
            launchEnvironmentOverrides: new Dictionary<string, string>(),
            application: application,
            applicationBuilder: builder,
            environmentScopes: [environment]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handle.DisposeAsync().AsTask());

        Assert.Equal(["application", "builder", "environment"], events);
        Assert.Same(application.Exception, exception);
        Assert.True(exception.Data.Contains("CleanupExceptions"));
    }

    private sealed class AsyncDisposableProbe(string name, List<string> events, bool shouldThrow) : IAsyncDisposable
    {
        public InvalidOperationException Exception { get; } = new($"{name} disposal failed.");

        public ValueTask DisposeAsync()
        {
            events.Add(name);
            if (shouldThrow)
            {
                throw Exception;
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class DisposableProbe(string name, List<string> events, bool shouldThrow) : IDisposable
    {
        public void Dispose()
        {
            events.Add(name);
            if (shouldThrow)
            {
                throw new InvalidOperationException($"{name} disposal failed.");
            }
        }
    }
}