using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class AppHostCleanupTests
{
    /// <summary>
    /// Requirement DR-01: verifies that a failed application stop does not short-circuit
    /// application or builder disposal, because every owned AppHost resource must be released.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_StopFails_StillDisposesApplicationAndBuilder()
    {
        var operations = new List<string>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AppHostCleanup.DisposeAsync(
                () =>
                {
                    operations.Add("stop");
                    throw new InvalidOperationException("stop failed");
                },
                () =>
                {
                    operations.Add("application");
                    return ValueTask.CompletedTask;
                },
                () =>
                {
                    operations.Add("builder");
                    return ValueTask.CompletedTask;
                }));

        Assert.Equal("stop failed", exception.Message);
        Assert.Equal(["stop", "application", "builder"], operations);
    }

    /// <summary>
    /// Requirement DR-01: verifies that failed application disposal still permits builder disposal.
    /// The expected result is a builder release attempt, protecting DCP resources from teardown short-circuiting.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ApplicationDisposalFails_StillDisposesBuilder()
    {
        var operations = new List<string>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AppHostCleanup.DisposeAsync(
                () =>
                {
                    operations.Add("stop");
                    return Task.CompletedTask;
                },
                () =>
                {
                    operations.Add("application");
                    throw new InvalidOperationException("application failed");
                },
                () =>
                {
                    operations.Add("builder");
                    return ValueTask.CompletedTask;
                }));

        Assert.Equal("application failed", exception.Message);
        Assert.Equal(["stop", "application", "builder"], operations);
    }

    /// <summary>
    /// Requirement DR-01: verifies that all cleanup failures are attempted while the first failure remains primary.
    /// Later failures must be available under CleanupExceptions so operators can diagnose incomplete teardown.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_MultipleOperationsFail_PreservesFirstExceptionAndAttachesLaterFailures()
    {
        var stopException = new InvalidOperationException("stop failed");
        var applicationException = new InvalidOperationException("application failed");
        var builderException = new InvalidOperationException("builder failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AppHostCleanup.DisposeAsync(
                () => Task.FromException(stopException),
                () => ValueTask.FromException(applicationException),
                () => ValueTask.FromException(builderException)));

        Assert.Same(stopException, exception);
        var cleanupExceptions = Assert.IsType<Exception[]>(exception.Data["CleanupExceptions"]);
        Assert.Equal([applicationException, builderException], cleanupExceptions);
    }

    /// <summary>
    /// Requirement DR-01: verifies the normal ownership-release order and exactly-once invocation.
    /// This guards the expected stop, application disposal, and builder disposal lifecycle used in operations.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_AllOperationsSucceed_InvokesEachOperationOnceInOrder()
    {
        var operations = new List<string>();

        await AppHostCleanup.DisposeAsync(
            () =>
            {
                operations.Add("stop");
                return Task.CompletedTask;
            },
            () =>
            {
                operations.Add("application");
                return ValueTask.CompletedTask;
            },
            () =>
            {
                operations.Add("builder");
                return ValueTask.CompletedTask;
            });

        Assert.Equal(["stop", "application", "builder"], operations);
        Assert.Equal(3, operations.Count);
    }
}