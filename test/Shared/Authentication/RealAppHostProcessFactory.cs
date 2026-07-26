using System.Diagnostics;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.TestShared.Authentication;

internal static class RealAppHostProcessFactory
{
    private static readonly string AppHostProjectPath = Path.Combine("src", "TNC.Trading.Platform.AppHost", "TNC.Trading.Platform.AppHost.csproj");

    public static AppHostProcessHandle StartAppHostProcess(bool enableInteractiveSignIn = false)
    {
        var existingPlatformProcessIds = AppHostProcessHandle.CapturePlatformProcessIds();
        var existingListeningPorts = AppHostProcessHandle.CaptureListeningPorts();
        var environmentOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AppHost__EnableInfrastructureContainers"] = bool.TrueString,
            ["AppHost__UseSyntheticRuntime"] = bool.FalseString,
            ["AppHost__UsePersistentKeycloakState"] = bool.FalseString,
            ["Authentication__Test__EnableInteractiveSignIn"] = enableInteractiveSignIn.ToString()
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{AppHostProjectPath}\" --launch-profile https",
            WorkingDirectory = GetRepositoryRoot(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var environmentOverride in environmentOverrides)
        {
            startInfo.Environment[environmentOverride.Key] = environmentOverride.Value;
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the AppHost process for the authentication test harness.");

        var launchCommand = $"{startInfo.FileName} {startInfo.Arguments}";
        return new AppHostProcessHandle(process, existingPlatformProcessIds, existingListeningPorts, launchCommand, environmentOverrides);
    }

    public static async Task<AppHostProcessHandle> StartManagedAppHostProcessAsync(bool enableInteractiveSignIn = false)
    {
        var existingPlatformProcessIds = AppHostProcessHandle.CapturePlatformProcessIds();
        var existingListeningPorts = AppHostProcessHandle.CaptureListeningPorts();
        var environmentOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AppHost__EnableInfrastructureContainers"] = bool.TrueString,
            ["AppHost__UseSyntheticRuntime"] = bool.FalseString,
            ["AppHost__UsePersistentKeycloakState"] = bool.FalseString,
            ["Authentication__Test__EnableInteractiveSignIn"] = enableInteractiveSignIn.ToString()
        };

        var environmentScopes = environmentOverrides
            .Select(static environmentOverride => new TestEnvironmentVariableScope(environmentOverride.Key, environmentOverride.Value))
            .Cast<IDisposable>()
            .ToArray();

        try
        {
            var applicationBuilder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.TNC_Trading_Platform_AppHost>(["DcpPublisher:RandomizePorts=false"])
                .ConfigureAwait(false);
            var application = await applicationBuilder.BuildAsync().ConfigureAwait(false);
            await application.StartAsync().ConfigureAwait(false);

            return new AppHostProcessHandle(
                process: null,
                existingPlatformProcessIds,
                existingListeningPorts,
                launchCommand: "Aspire.Hosting.Testing DistributedApplicationTestingBuilder<CreateAsync<Projects.TNC_Trading_Platform_AppHost>>()",
                launchEnvironmentOverrides: environmentOverrides,
                application: application,
                applicationBuilder: applicationBuilder,
                environmentScopes: environmentScopes,
                preferredWebBaseUri: null);
        }
        catch
        {
            for (var index = environmentScopes.Length - 1; index >= 0; index--)
            {
                environmentScopes[index].Dispose();
            }

            throw;
        }
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

        throw new InvalidOperationException("Could not locate the repository root for the authentication test harness.");
    }

    private sealed class TestEnvironmentVariableScope : IDisposable
    {
        private readonly string name;
        private readonly string? originalValue;

        public TestEnvironmentVariableScope(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Environment variable name is required.", nameof(name));
            }

            this.name = name;
            originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(name, originalValue);
        }
    }
}