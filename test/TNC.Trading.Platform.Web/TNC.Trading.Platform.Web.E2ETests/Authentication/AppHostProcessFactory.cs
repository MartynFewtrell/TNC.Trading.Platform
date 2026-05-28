using System.Diagnostics;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

internal static class AppHostProcessFactory
{
    private static readonly string AppHostProjectPath = Path.Combine("src", "TNC.Trading.Platform.AppHost", "TNC.Trading.Platform.AppHost.csproj");

    public static AppHostProcessHandle StartAppHostProcess()
    {
        var existingPlatformProcessIds = AppHostProcessHandle.CapturePlatformProcessIds();
        var existingListeningPorts = AppHostProcessHandle.CaptureListeningPorts();

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{AppHostProjectPath}\" --launch-profile https",
            WorkingDirectory = GetRepositoryRoot(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.Environment["AppHost__EnableInfrastructureContainers"] = bool.TrueString;
        startInfo.Environment["AppHost__UseSyntheticRuntime"] = bool.FalseString;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the AppHost process for the authentication E2E tests.");

        return new AppHostProcessHandle(process, existingPlatformProcessIds, existingListeningPorts);
    }

    public static async Task<Uri> GetWebBaseUriAsync(AppHostProcessHandle appHostProcess)
    {
        var authenticationEntryUri = await appHostProcess.WaitForWebSignInUriAsync(TimeSpan.FromSeconds(120));
        return new Uri(authenticationEntryUri.GetLeftPart(UriPartial.Authority));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, AppHostProjectPath)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root for the authentication E2E tests.");
    }
}