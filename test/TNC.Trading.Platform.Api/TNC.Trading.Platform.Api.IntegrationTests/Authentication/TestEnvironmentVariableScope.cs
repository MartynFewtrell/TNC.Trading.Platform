namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

internal sealed class TestEnvironmentVariableScope : IDisposable
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
