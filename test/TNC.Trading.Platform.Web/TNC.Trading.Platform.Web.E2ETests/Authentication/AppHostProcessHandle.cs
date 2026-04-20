using System.Diagnostics;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

internal sealed class AppHostProcessHandle : IAsyncDisposable
{
    private readonly Process process;
    private readonly int[] existingPlatformProcessIds;

    private static readonly string[] PlatformProcessNames =
    [
        "TNC.Trading.Platform.Web",
        "TNC.Trading.Platform.Api",
        "TNC.Trading.Platform.AppHost",
        "dotnet"
    ];

    public AppHostProcessHandle(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        this.process = process;
        existingPlatformProcessIds = CapturePlatformProcesses()
            .Select(candidate => candidate.Id)
            .ToArray();
    }

    public Process Process => process;

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                await process.WaitForExitAsync().ConfigureAwait(false);
            }

            KillSpawnedPlatformProcesses();
        }
        finally
        {
            process.Dispose();
        }
    }

    private void KillSpawnedPlatformProcesses()
    {
        var priorProcesses = existingPlatformProcessIds.ToHashSet();
        foreach (var candidate in CapturePlatformProcesses())
        {
            if (priorProcesses.Contains(candidate.Id))
            {
                candidate.Dispose();
                continue;
            }

            try
            {
                candidate.Kill(entireProcessTree: true);
                candidate.WaitForExit();
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                candidate.Dispose();
            }
        }
    }

    private static IEnumerable<Process> CapturePlatformProcesses() =>
        Process.GetProcesses()
            .Where(candidate => PlatformProcessNames.Contains(candidate.ProcessName, StringComparer.Ordinal));
}
