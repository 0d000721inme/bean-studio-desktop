# Run the existing twelve V5.7 tracker checks without screenshots or an emulator.
[CmdletBinding()]
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) {
    & (Join-Path $projectRoot 'build.ps1')
}
$executablePath = Join-Path $projectRoot 'BeanStudio.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw 'BeanStudio.exe is missing. Run build.ps1 first.'
}
$reportDirectory = Join-Path $projectRoot 'artifacts\tests'
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$reportPath = Join-Path $reportDirectory 'clock57-tracker.txt'
if (Test-Path -LiteralPath $reportPath) {
    Remove-Item -LiteralPath $reportPath
}
$process = Start-Process -FilePath $executablePath -WorkingDirectory $projectRoot `
    -ArgumentList @('--clock57-test', '--tracker', ('"{0}"' -f $reportPath)) `
    -WindowStyle Hidden -Wait -PassThru
$process.Refresh()
if ($process.ExitCode -ne 0) {
    throw "Tracker checks failed (exit code $($process.ExitCode)). See $reportPath and any .error.txt report."
}
if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw "Tracker checks did not produce $reportPath."
}
$reportLines = @(Get-Content -LiteralPath $reportPath -Encoding UTF8)
$passedChecks = @($reportLines | Where-Object { $_ -match '^PASS ' }).Count
if ($passedChecks -ne 12 -or $reportLines -notcontains 'FAILURES=0' -or
    @($reportLines | Where-Object { $_ -match '^FAIL ' }).Count -ne 0) {
    throw "Expected twelve passing tracker checks. See $reportPath."
}
Write-Output "PASS: all 12 V5.7 tracker checks. Report: $reportPath"
