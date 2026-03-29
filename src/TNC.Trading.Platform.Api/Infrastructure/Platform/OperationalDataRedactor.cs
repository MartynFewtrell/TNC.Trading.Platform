using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TNC.Trading.Platform.Api.Infrastructure.Platform;

internal static class OperationalDataRedactor
{
    internal const string RedactedValue = "[redacted]";

    private static readonly Regex SensitiveAssignmentPattern = new(
        @"(?<key>api[-_ ]?key|identifier|password|secret|token|connection\s*string|protectedvalue)(?<separator>\s*[:=]\s*)(?<value>[^,;\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AuthorizationHeaderPattern = new(
        @"Authorization(?<separator>\s*:\s*)Bearer\s+(?<value>[^\s,;]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BearerTokenPattern = new(
        @"Bearer\s+(?<value>[^\s,;]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

    public static string Serialize(object? value)
    {
        if (value is null)
        {
            return "{}";
        }

        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        var sanitized = RedactElement(element, propertyName: null);
        return JsonSerializer.Serialize(sanitized, SerializerOptions);
    }

    public static string? RedactText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var redactedAuthorization = AuthorizationHeaderPattern.Replace(
            value,
            static match => $"Authorization{match.Groups["separator"].Value}Bearer {RedactedValue}");

        var redacted = SensitiveAssignmentPattern.Replace(
            redactedAuthorization,
            static match => $"{match.Groups["key"].Value}{match.Groups["separator"].Value}{RedactedValue}");

        return BearerTokenPattern.Replace(
            redacted,
            static _ => $"Bearer {RedactedValue}");
    }

    private static object? RedactElement(JsonElement element, string? propertyName)
    {
        if (IsSensitive(propertyName))
        {
            return RedactedValue;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => RedactObject(element),
            JsonValueKind.Array => RedactArray(element, propertyName),
            JsonValueKind.String => RedactText(element.GetString()),
            JsonValueKind.Number => ReadNumber(element),
            JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }

    private static Dictionary<string, object?> RedactObject(JsonElement element)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            values[property.Name] = RedactElement(property.Value, property.Name);
        }

        return values;
    }

    private static List<object?> RedactArray(JsonElement element, string? propertyName)
    {
        var values = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            values.Add(RedactElement(item, propertyName));
        }

        return values;
    }

    private static object ReadNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var int64Value))
        {
            return int64Value;
        }

        if (element.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return element.GetDouble();
    }

    private static bool IsSensitive(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return SensitiveNameFragments.Any(fragment =>
            propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
