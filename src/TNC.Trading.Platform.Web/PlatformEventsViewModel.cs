namespace TNC.Trading.Platform.Web;

internal sealed record PlatformEventsViewModel(
    IReadOnlyList<PlatformEventItemViewModel> Events);
