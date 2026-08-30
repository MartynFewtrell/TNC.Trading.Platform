namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record GetTrailingStopsPreferenceObservationsRequest(int PageSize = 25, string? Cursor = null);