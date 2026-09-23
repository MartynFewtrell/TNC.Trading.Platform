[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Test", "Live")]
    [string]$PlatformEnvironment,

    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [string]$BundlePath
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if ([string]::IsNullOrWhiteSpace($BundlePath)) {
    $BundlePath = Join-Path $repositoryRoot "artifacts\migrations\tnc-trading-platform-migrations.exe"
}

if (-not (Test-Path $BundlePath -PathType Leaf)) {
    throw "Migration bundle was not found at '$BundlePath'. Build it before running deployment migrations."
}

# The connection is deliberately mandatory and passed only by the deployment caller.
# Do not fall back to application configuration or invent credentials here.
& $BundlePath --connection $ConnectionString
if ($LASTEXITCODE -ne 0) {
    throw "EF migration bundle execution failed for $PlatformEnvironment with exit code $LASTEXITCODE."
}
