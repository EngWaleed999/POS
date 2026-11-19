[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('authz-smoke', 'staff-read', 'cashier-login', 'staff-registration')]
    [string]$Test,

    [ValidateSet('discovery', 'before', 'after')]
    [string]$Build = 'discovery',

    [string]$RunId = (Get-Date -Format 'yyyyMMdd-HHmmss'),

    [switch]$CaptureTimeSeries
)

$ErrorActionPreference = 'Stop'

$K6Root = Split-Path -Parent $PSScriptRoot
$Scenario = Join-Path $K6Root "scenarios/$Test.js"
$ResultsDir = Join-Path $K6Root "results/$Build"

if (-not (Get-Command k6 -ErrorAction SilentlyContinue)) {
    throw 'k6 was not found in PATH. Install it, reopen PowerShell, then run: k6 version'
}
if (-not (Test-Path $Scenario)) {
    throw "Scenario not found: $Scenario"
}

New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

$BaseName = "$Test-$RunId"
$SummaryPath = Join-Path $ResultsDir "$BaseName-summary.json"
$ConsolePath = Join-Path $ResultsDir "$BaseName-console.txt"
$MetadataPath = Join-Path $ResultsDir "$BaseName-metadata.json"
$TimeSeriesPath = Join-Path $ResultsDir "$BaseName-timeseries.json"

$RepoRoot = $null
$GitSha = $null
try {
    $RepoRoot = (& git -C $K6Root rev-parse --show-toplevel 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -eq 0 -and $RepoRoot) {
        $GitSha = (& git -C $RepoRoot rev-parse HEAD 2>$null | Out-String).Trim()
    }
} catch {
    # Git metadata is helpful but not required to execute a test.
}

$Cpu = Get-CimInstance Win32_Processor | Select-Object -First 1 Name, NumberOfCores, NumberOfLogicalProcessors
$Os = Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, BuildNumber, TotalVisibleMemorySize
$K6Version = (& k6 version 2>&1 | Out-String).Trim()

$Metadata = [ordered]@{
    test = $Test
    build = $Build
    runId = $RunId
    startedAtLocal = (Get-Date).ToString('o')
    computerName = $env:COMPUTERNAME
    gitCommit = $GitSha
    repositoryRoot = $RepoRoot
    k6Version = $K6Version
    cpu = $Cpu
    operatingSystem = $Os
    baseUrl = $env:BASE_URL
    staffPath = $env:STAFF_PATH
    preallocatedVUs = $env:PREALLOCATED_VUS
    maxVUs = $env:MAX_VUS
    note = 'Do not add tokens, passwords, PINs, or client secrets to this metadata file.'
}
$Metadata | ConvertTo-Json -Depth 6 | Set-Content -Path $MetadataPath -Encoding utf8

$K6Args = @('run', '--summary-mode=full', '--summary-export', $SummaryPath)
if ($CaptureTimeSeries) {
    $K6Args += @('--out', "json=$TimeSeriesPath")
}
$K6Args += $Scenario

Write-Host "Running $Test ($Build)"
Write-Host "Summary: $SummaryPath"
Write-Host "Console: $ConsolePath"
if ($CaptureTimeSeries) { Write-Host "Time series: $TimeSeriesPath" }

Push-Location $K6Root
try {
    & k6 @K6Args 2>&1 | Tee-Object -FilePath $ConsolePath
    $K6ExitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

$MetadataObject = Get-Content $MetadataPath -Raw | ConvertFrom-Json
$MetadataObject | Add-Member -NotePropertyName finishedAtLocal -NotePropertyValue (Get-Date).ToString('o')
$MetadataObject | Add-Member -NotePropertyName k6ExitCode -NotePropertyValue $K6ExitCode
$MetadataObject | ConvertTo-Json -Depth 6 | Set-Content -Path $MetadataPath -Encoding utf8

if ($K6ExitCode -ne 0) {
    throw "k6 exited with code $K6ExitCode. Review $ConsolePath and $SummaryPath."
}

Write-Host "Completed. Evidence saved under: $ResultsDir"
