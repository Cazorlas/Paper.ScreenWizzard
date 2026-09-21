<#
.SYNOPSIS
  Builds the release files of Paper.ScreenWizzard into artifacts/ (or -OutDir): Setup.exe, the portable .zip and SHA256SUMS.txt.

.DESCRIPTION
  1. dotnet publish: win-x64, self-contained, one file, not trimmed (WPF cannot be trimmed), version = -Version.
  2. the portable zip: the exe and the licence.
  3. Setup.exe: installer/Setup.iss built with the Inno Setup compiler that installer/get-inno.ps1 keeps in .tools/inno.
  4. SHA256SUMS.txt for the Setup.exe and the .zip.

  -Lite builds only a Setup.exe of the given version (no zip, no sums): installer/verify-installer.ps1 uses it to get an older and a
  newer neighbour of the package under test.

.PARAMETER Version
  x.y.z. In a release build it is the tag without the leading "v".
#>
param(
    [string]$Version = '0.1.0',
    [string]$OutDir,
    [switch]$Lite
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repo 'artifacts' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be x.y.z, got '$Version'" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

function Run([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed with exit code $LASTEXITCODE" }
}

Push-Location $repo
try {
    # ---- 1. publish -----------------------------------------------------------------------------------------------
    # The single file is not compressed by .NET: Setup.exe and the zip compress it, and an uncompressed one starts faster.
    $publishDir = Join-Path $OutDir "publish-$Version"
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
    Run 'dotnet publish' {
        dotnet publish src/Paper.ScreenWizzard.App/Paper.ScreenWizzard.App.csproj -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false `
            "-p:Version=$Version" -p:IncludeSourceRevisionInInformationalVersion=false `
            -o $publishDir --nologo -v minimal
    }
    $exe = Join-Path $publishDir 'Paper.ScreenWizzard.exe'
    if (-not (Test-Path $exe)) { throw "publish did not produce $exe" }

    # ---- 2. Setup.exe -----------------------------------------------------------------------------------------------
    $iscc = (& (Join-Path $PSScriptRoot 'get-inno.ps1') | Select-Object -Last 1)
    $setup = Join-Path $OutDir "Paper.ScreenWizzard-$Version-win-x64-Setup.exe"
    if (Test-Path $setup) { Remove-Item -Force $setup }
    Run 'iscc' {
        & $iscc "/DVersion=$Version" "/DPublishDir=$publishDir" "/DIconFile=$(Join-Path $repo 'src\Paper.ScreenWizzard.App\app.ico')" `
            "/DLicenseFile=$(Join-Path $repo 'LICENSE')" "/DOutDir=$OutDir" /Q (Join-Path $PSScriptRoot 'Setup.iss')
    }
    if (-not (Test-Path $setup)) { throw "iscc did not produce $setup" }

    if ($Lite) {
        Remove-Item -Recurse -Force $publishDir
        Write-Host "built $setup (lite)"
        return
    }

    # ---- 3. portable zip and checksums ------------------------------------------------------------------------------
    $zip = Join-Path $OutDir "Paper.ScreenWizzard-$Version-win-x64.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    $stage = Join-Path $OutDir "zip-$Version"
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
    New-Item -ItemType Directory -Path $stage | Out-Null
    Copy-Item $exe $stage
    Copy-Item (Join-Path $repo 'LICENSE') $stage
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Remove-Item -Recurse -Force $stage

    $sums = foreach ($file in @($setup, $zip)) {
        '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 $file).Hash.ToLower(), (Split-Path -Leaf $file)
    }
    $sumsFile = Join-Path $OutDir 'SHA256SUMS.txt'
    [IO.File]::WriteAllLines($sumsFile, $sums)

    Write-Host 'built:'
    foreach ($file in @($setup, $zip, $sumsFile)) { '  {0}  ({1:N1} MB)' -f $file, ((Get-Item $file).Length / 1MB) | Write-Host }
}
finally {
    Pop-Location
}
