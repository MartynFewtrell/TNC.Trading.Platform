[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$project = Join-Path $repositoryRoot "src\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.csproj"
$outputDirectory = Join-Path $repositoryRoot "artifacts\migrations"
$bundlePath = Join-Path $outputDirectory "tnc-trading-platform-migrations.exe"

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
if (Test-Path $bundlePath) {
    Remove-Item $bundlePath -Force
}

dotnet ef migrations bundle `
    --project $project `
    --configuration $Configuration `
    --output $bundlePath

if ($LASTEXITCODE -ne 0) {
    throw "EF migration bundle build failed with exit code $LASTEXITCODE."
}

Write-Output $bundlePath
