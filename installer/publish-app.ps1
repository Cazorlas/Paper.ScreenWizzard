<#
.SYNOPSIS
  Publishes the app as the one self-contained exe the installer packs: win-x64, one file, not trimmed (WPF cannot be trimmed).

.DESCRIPTION
  Used by installer/build-package.ps1, and by installer/Setup.iss itself when the exe it needs is not there yet (so that opening
  Setup.iss in the Inno Setup compiler and pressing Compile is enough).
  The single file is not compressed by .NET: the installer and the zip compress it, and an uncompressed one starts faster.
#>
param(
    [string]$Version = '0.1.0',
    [Parameter(Mandatory = $true)][string]$OutDir
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be x.y.z, got '$Version'" }
if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }

Push-Location $repo
try {
    dotnet publish src/Paper.ScreenWizzard.App/Paper.ScreenWizzard.App.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false `
        "-p:Version=$Version" -p:IncludeSourceRevisionInInformationalVersion=false `
        -o $OutDir --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}
if (-not (Test-Path (Join-Path $OutDir 'Paper.ScreenWizzard.exe'))) { throw "publish did not produce Paper.ScreenWizzard.exe in $OutDir" }
