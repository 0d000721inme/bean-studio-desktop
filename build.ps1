# Build with the Windows .NET Framework compiler; no Visual Studio is required.
[CmdletBinding()]
param([switch]$SkipDependencies)

$ErrorActionPreference = 'Stop'
if (-not $env:WINDIR -or -not [Environment]::Is64BitOperatingSystem) {
    throw 'Bean Studio requires 64-bit Windows 10 or later.'
}
if (-not $SkipDependencies) {
    & (Join-Path $PSScriptRoot 'scripts\fetch-dependencies.ps1')
}

$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkPath 'csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath -PathType Leaf)) {
    throw '.NET Framework 4.8 is required. The Windows C# compiler was not found.'
}
$dependencyPaths = @(
    'Tesseract.dll',
    'x64\tesseract50.dll',
    'x64\leptonica-1.82.0.dll',
    'tessdata\eng.traineddata'
)
foreach ($relativePath in $dependencyPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $relativePath) -PathType Leaf)) {
        throw "Missing $relativePath. Run scripts\fetch-dependencies.ps1 first."
    }
}

$sourcePaths = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File |
    Sort-Object Name | ForEach-Object { $_.FullName })
$outputPath = Join-Path $PSScriptRoot 'BeanStudio.exe'
$winReferences = @(Get-ChildItem (Join-Path $env:WINDIR 'System32\WinMetadata\*.winmd') |
    ForEach-Object { '/r:' + $_.FullName })
$facadeReferences = @('System.Runtime', 'System.Runtime.InteropServices.WindowsRuntime', 'System.Threading.Tasks') |
    ForEach-Object {
        Get-ChildItem (Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_MSIL\$_\v*\*.dll") |
            ForEach-Object { '/r:' + $_.FullName }
    }
if ($winReferences.Count -eq 0) {
    throw 'Windows WinRT metadata was not found. Windows 10 or later is required.'
}

& $compilerPath $winReferences $facadeReferences "/r:$(Join-Path $frameworkPath 'System.Runtime.WindowsRuntime.dll')" `
    /nologo /target:winexe /main:MuMuBeans.Program /optimize+ /platform:x64 `
    "/reference:$(Join-Path $PSScriptRoot 'Tesseract.dll')" /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "/out:$outputPath" $sourcePaths
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed (exit code $LASTEXITCODE)."
}
Write-Output "Built $outputPath"
