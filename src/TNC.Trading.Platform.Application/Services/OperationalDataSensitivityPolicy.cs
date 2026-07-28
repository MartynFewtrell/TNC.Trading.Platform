namespace TNC.Trading.Platform.Application.Services;

internal static class OperationalDataSensitivityPolicy
{
    private static readonly string[] SensitiveNameFragments =
    [
        "apikey",
        "identifier",
        "password",
        "secret",
        "token",
        "authorization",
        "connectionstring",
        "protectedvalue"
    ];

    public static OperationalDataSensitivity Classify(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return OperationalDataSensitivity.NonSensitive;
        }

        return SensitiveNameFragments.Any(fragment =>
            fieldName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                ? OperationalDataSensitivity.Sensitive
                : OperationalDataSensitivity.NonSensitive;
    }
}