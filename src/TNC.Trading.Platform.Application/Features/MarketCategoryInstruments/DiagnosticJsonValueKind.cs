namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Allow-listed JSON shape labels used in sanitized provider diagnostics.</summary>
internal enum DiagnosticJsonValueKind
{
    Missing,
    Null,
    Number,
    String,
    Object,
    Array,
    True,
    False,
    Other
}
