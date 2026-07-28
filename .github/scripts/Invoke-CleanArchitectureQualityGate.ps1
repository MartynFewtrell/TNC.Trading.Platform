[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string] $GateId
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $repositoryRoot

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$evidenceRoot = Join-Path $repositoryRoot "artifacts/quality-gates/$GateId/$timestamp"
$resultsDirectory = Join-Path $evidenceRoot 'trx'
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

$logPath = Join-Path $evidenceRoot 'quality-gate.log'
Start-Transcript -Path $logPath -Force | Out-Null

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $CommandName,
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $startedAt = Get-Date
    Write-Host "[$($startedAt.ToString('o'))] Starting $CommandName"
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $commandOutput = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    $commandOutput | ForEach-Object { Write-Host $_ }
    Write-Host "[$((Get-Date).ToString('o'))] Finished $CommandName with exit code $exitCode"
    return [int] $exitCode
}

$buildExitCode = 0
$testExitCode = 0
try {
    $buildExitCode = Invoke-DotNetCommand -CommandName 'dotnet build' -Arguments @(
        'build',
        'TNC.Trading.Platform.slnx'
    )

    if ($buildExitCode -ne 0) {
        throw "dotnet build failed with exit code $buildExitCode."
    }

    $testExitCode = Invoke-DotNetCommand -CommandName 'dotnet test' -Arguments @(
        'test',
        'TNC.Trading.Platform.slnx',
        '--results-directory',
        $resultsDirectory,
        '--logger',
        'trx'
    )

    if ($testExitCode -ne 0) {
        throw "dotnet test failed with exit code $testExitCode."
    }
}
finally {
    $trxFiles = @(Get-ChildItem -Path $resultsDirectory -Filter '*.trx' -File -Recurse -ErrorAction SilentlyContinue)
    $passed = 0
    $failed = 0
    $skipped = 0

    foreach ($trxFile in $trxFiles) {
        [xml] $trx = [System.IO.File]::ReadAllText($trxFile.FullName)
        foreach ($result in @($trx.TestRun.Results.UnitTestResult)) {
            switch ($result.outcome) {
                'Passed' { $passed++ }
                'Failed' { $failed++ }
                'Skipped' { $skipped++ }
                'NotExecuted' { $skipped++ }
            }
        }
    }

    Write-Host "[$((Get-Date).ToString('o'))] GateId: $GateId"
    Write-Host "[$((Get-Date).ToString('o'))] TRX files: $($trxFiles.Count)"
    Write-Host "[$((Get-Date).ToString('o'))] Passed: $passed; Failed: $failed; Skipped: $skipped"
    Write-Host "[$((Get-Date).ToString('o'))] Evidence: $evidenceRoot"
    Stop-Transcript | Out-Null
}

if ($buildExitCode -ne 0) {
    exit $buildExitCode
}

if ($testExitCode -ne 0) {
    exit $testExitCode
}

exit 0