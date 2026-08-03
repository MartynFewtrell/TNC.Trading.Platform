using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent;

internal sealed record RecordAuthAuditEventIntent(PlatformEventRecord Event);