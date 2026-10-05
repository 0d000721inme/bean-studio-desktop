# Restore only the x64 OCR files used by Bean Studio, from pinned official sources.
[CmdletBinding()]
param(
    [string]$CacheDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) '.cache'),
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectRoot = Split-Path -Parent $PSScriptRoot
$CacheDirectory = [System.IO.Path]::GetFullPath($CacheDirectory)
New-Item -ItemType Directory -Force -Path $CacheDirectory | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

function Get-VerifiedDownload {
    param([string]$Uri, [string]$Destination, [string]$Sha256)

    if (-not (Test-Path -LiteralPath $Destination -PathType Leaf)) {
        if ($Offline) {
            throw "Offline cache file is missing: $Destination"
        }
        $downloadPath = $Destination + '.download'
        Invoke-WebRequest -Uri $Uri -OutFile $downloadPath -UseBasicParsing
        $downloadHash = (Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash
        if ($downloadHash -ne $Sha256) {
            throw "SHA256 mismatch for $Uri. Expected $Sha256; received $downloadHash."
        }
        Move-Item -LiteralPath $downloadPath -Destination $Destination -Force
    }
    $cachedHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
    if ($cachedHash -ne $Sha256) {
        throw "SHA256 mismatch in cache: $Destination. Expected $Sha256; received $cachedHash."
    }
}

$packagePath = Join-Path $CacheDirectory 'tesseract.5.2.0.nupkg'
Get-VerifiedDownload `
    -Uri 'https://api.nuget.org/v3-flatcontainer/tesseract/5.2.0/tesseract.5.2.0.nupkg' `
    -Destination $packagePath `
    -Sha256 '202D82FC7C7D8384DF7DA57206D5E1F456CCDABD648C46E67CDFAA3A911D4795'

# tessdata_fast 4.1.0, pinned to the commit behind the official tag.
$modelPath = Join-Path $CacheDirectory 'eng.traineddata'
Get-VerifiedDownload `
    -Uri 'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/65727574dfcd264acbb0c3e07860e4e9e9b22185/eng.traineddata' `
    -Destination $modelPath `
    -Sha256 '7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2'

Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'x64'), (Join-Path $projectRoot 'tessdata') | Out-Null
$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    # Extract named entries only; no package path can choose an output directory.
    $files = @(
        @{ Entry = 'lib/net48/Tesseract.dll'; Output = 'Tesseract.dll' },
        @{ Entry = 'x64/tesseract50.dll'; Output = 'x64\tesseract50.dll' },
        @{ Entry = 'x64/leptonica-1.82.0.dll'; Output = 'x64\leptonica-1.82.0.dll' }
    )
    foreach ($file in $files) {
        $entry = $archive.GetEntry($file.Entry)
        if ($null -eq $entry) {
            throw "The verified Tesseract package is missing $($file.Entry)."
        }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $projectRoot $file.Output), $true)
    }
}
finally {
    $archive.Dispose()
}
Copy-Item -LiteralPath $modelPath -Destination (Join-Path $projectRoot 'tessdata\eng.traineddata') -Force
Write-Output 'Restored verified Tesseract 5.2.0 (x64) and tessdata_fast 4.1.0 (eng).'
