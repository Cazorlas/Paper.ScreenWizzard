<#
.SYNOPSIS
  Builds the release files of Paper.ScreenWizzard into artifacts/ (or -OutDir): the .msi, the portable .zip and SHA256SUMS.txt.

.DESCRIPTION
  1. dotnet publish: win-x64, self-contained, one file, not trimmed (WPF cannot be trimmed), version = -Version.
  2. the portable zip: the exe and the licence.
  3. the .msi: installer/Package.wxs built with the WiX tool pinned in dotnet-tools.json.
  4. SHA256SUMS.txt for the .msi and the .zip.

  -Lite builds only an .msi of the given version (no zip, no sums): installer/verify-installer.ps1 uses it to get an older and a
  newer neighbour of the package under test.

.PARAMETER Version
  x.y.z. In a release build it is the tag without the leading "v".
#>
param(
    [string]$Version = '0.1.0',
    [string]$OutDir,
    [string]$MsiName,
    [switch]$Lite
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repo 'artifacts' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be x.y.z, got '$Version'" }
if (-not $MsiName) { $MsiName = "Paper.ScreenWizzard-$Version-win-x64.msi" }
$wixVersion = '6.0.2'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

function Run([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed with exit code $LASTEXITCODE" }
}

Push-Location $repo
try {
    # ---- 1. publish -----------------------------------------------------------------------------------------------
    $publishDir = Join-Path $OutDir "publish-$Version"
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
    Run 'dotnet publish' {
        dotnet publish src/Paper.ScreenWizzard.App/Paper.ScreenWizzard.App.csproj -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
            -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false `
            "-p:Version=$Version" -p:IncludeSourceRevisionInInformationalVersion=false `
            -o $publishDir --nologo -v minimal
    }
    $exe = Join-Path $publishDir 'Paper.ScreenWizzard.exe'
    if (-not (Test-Path $exe)) { throw "publish did not produce $exe" }

    # ---- 2. licence as RTF for the licence page ---------------------------------------------------------------------
    $licenseText = [IO.File]::ReadAllText((Join-Path $repo 'LICENSE'))
    $escaped = $licenseText.Replace('\', '\\').Replace('{', '\{').Replace('}', '\}') -replace "`r?`n", "\line `n"
    $rtf = Join-Path $publishDir 'license.rtf'
    [IO.File]::WriteAllText($rtf, "{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\fs18 $escaped}", [Text.Encoding]::ASCII)

    # ---- 3. the .msi ------------------------------------------------------------------------------------------------
    Run 'dotnet tool restore' { dotnet tool restore --tool-manifest (Join-Path $repo 'dotnet-tools.json') | Out-Null }
    $extensions = (dotnet wix extension list -g) -join "`n"
    if ($extensions -notmatch "WixToolset.UI.wixext\s+$([regex]::Escape($wixVersion))") {
        Run 'wix extension add UI' { dotnet wix extension add -g "WixToolset.UI.wixext/$wixVersion" | Out-Null }
    }
    if ($extensions -notmatch "WixToolset.Util.wixext\s+$([regex]::Escape($wixVersion))") {
        Run 'wix extension add Util' { dotnet wix extension add -g "WixToolset.Util.wixext/$wixVersion" | Out-Null }
    }
    $msi = Join-Path $OutDir $MsiName
    if (Test-Path $msi) { Remove-Item -Force $msi }
    Run 'wix build' {
        dotnet wix build installer/Package.wxs -arch x64 -culture en-US `
            -ext "WixToolset.UI.wixext/$wixVersion" -ext "WixToolset.Util.wixext/$wixVersion" `
            -d "Version=$Version" -d "PublishDir=$publishDir" -d "IconFile=$(Join-Path $repo 'src\Paper.ScreenWizzard.App\app.ico')" `
            -d "LicenseRtf=$rtf" -o $msi
    }
    if (-not (Test-Path $msi)) { throw "wix build did not produce $msi" }

    if ($Lite) {
        Remove-Item -Recurse -Force $publishDir
        Write-Host "built $msi (lite)"
        return
    }

    # ---- 4. portable zip and checksums ------------------------------------------------------------------------------
    $zip = Join-Path $OutDir "Paper.ScreenWizzard-$Version-win-x64.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    $stage = Join-Path $OutDir "zip-$Version"
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
    New-Item -ItemType Directory -Path $stage | Out-Null
    Copy-Item $exe $stage
    Copy-Item (Join-Path $repo 'LICENSE') $stage
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Remove-Item -Recurse -Force $stage

    $sums = foreach ($file in @($msi, $zip)) {
        '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 $file).Hash.ToLower(), (Split-Path -Leaf $file)
    }
    $sumsFile = Join-Path $OutDir 'SHA256SUMS.txt'
    [IO.File]::WriteAllLines($sumsFile, $sums)

    Write-Host "built:"
    foreach ($file in @($msi, $zip, $sumsFile)) { '  {0}  ({1:N1} MB)' -f $file, ((Get-Item $file).Length / 1MB) | Write-Host }
}
finally {
    Pop-Location
}
