using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry.Ports;

internal sealed record ManualAuthRetryCommitIntent(
    PlatformConfigurationSnapshot Configuration,
    PlatformRuntimeState State,
    PlatformRetryCycle RetryCycle,
    PlatformEventRecord Event,
    IgLoginSnapshot? LoginSnapshot = null,
    IgProofDataSnapshot? ProofData = null);