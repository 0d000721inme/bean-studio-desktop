# V5.8.1 checks against synthetic windows only. GUI/WGC suites run sequentially.
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [ValidateSet('settings','performance','overlay','capture','studio','resize','pause','clock','sound','sound-engine','ui','overlay581','drag','capture-status')]
    [string[]]$Suite = @('settings','overlay','overlay581','capture-status','drag','studio','pause','clock','resize','ui')
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $projectRoot 'build.ps1') }
$executablePath = Join-Path $projectRoot 'BeanStudio.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) { throw 'Run build.ps1 first.' }
$reportRoot = Join-Path $projectRoot ('artifacts\tests58\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$checks = @{
    'settings' = @('--settings58-test', '快捷键与界面配置验证.txt', 34)
    'performance' = @('--performance58-test', 'performance-tests.txt', 28)
    'overlay' = @('--overlay58-test', 'overlay58-tests.txt', 54)
    'capture' = @('--capture58-test', 'capture58-tests.txt', 19)
    'studio' = @('--studio58-test', 'studio58-tests.txt', 197)
    'resize' = @('--resize58-test', 'resize58-report.txt', 25)
    'pause' = @('--pause-test', '暂停回归验证.txt', 28)
    'clock' = @('--clock57-test', 'clock57-tracker.txt', 12)
    'sound' = @('--sound-test', '音效事件与配置验证.txt', 33)
    'sound-engine' = @('--sound-engine-test', '音效实际计时链路验证.txt', 12)
    'ui' = @('--ui-test', '界面交互验证.txt', 8)
    'overlay581' = @('--overlay581-test', 'overlay581-tests.txt', 48)
    'drag' = @('--drag581-test', 'drag581-tests.txt', 26)
    'capture-status' = @('--capture-status581-test', 'capture-status581.txt', 4)
}
foreach ($suiteName in $Suite) {
    $check = $checks[$suiteName]
    $reportDirectory = Join-Path $reportRoot $suiteName
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $reportPath = Join-Path $reportDirectory $check[1]
    $parameters = @($check[0], ('"{0}"' -f $reportDirectory))
    if ($suiteName -eq 'capture') { $parameters = @($check[0], ('"{0}"' -f $reportPath)) }
    if ($suiteName -eq 'clock') { $parameters = @($check[0], '--tracker', ('"{0}"' -f $reportPath)) }
    Write-Output "Running $suiteName ..."
    $checkProcess = Start-Process -FilePath $executablePath -WorkingDirectory $projectRoot `
        -ArgumentList $parameters -WindowStyle Hidden -Wait -PassThru
    $checkProcess.Refresh()
    if ($checkProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw "$suiteName failed or did not write its report. See $reportDirectory (exit $($checkProcess.ExitCode))."
    }
    $lines = @(Get-Content -LiteralPath $reportPath -Encoding UTF8)
    $passed = @($lines | Where-Object { $_ -match '^PASS ' }).Count
    if ($passed -ne $check[2] -or @($lines | Where-Object { $_ -match '^FAIL ' }).Count -gt 0 -or
        @($lines | Where-Object { $_ -match '^FAILURES=[1-9]' }).Count -gt 0) {
        throw "$suiteName did not pass its expected $($check[2]) checks. See $reportPath."
    }
    Write-Output "PASS $suiteName : $passed checks. Report: $reportPath"
}
Write-Output "Selected V5.8.1 checks passed. Reports: $reportRoot"
