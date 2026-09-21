<#
.SYNOPSIS
  Puts the Inno Setup compiler in .tools/inno (once) and prints the path of ISCC.exe.

.DESCRIPTION
  The project's tools live in the project, not on the machine: the compiler is installed for the current user into .tools/inno
  (ignored by git), and the entries the Inno installer adds to the machine (an "Apps" entry, the .iss file association) are taken
  off again, so nothing outside the repository changes. The download is pinned by version and SHA-256, and its signature must be valid.
#>
param(
    [string]$Version = '6.7.3',
    [string]$Sha256 = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$tools = Join-Path $repo '.tools'
$inno = Join-Path $tools 'inno'
$iscc = Join-Path $inno 'ISCC.exe'

if (-not (Test-Path $iscc)) {
    New-Item -ItemType Directory -Force -Path $tools | Out-Null
    $installer = Join-Path $tools "innosetup-$Version.exe"
    if (-not (Test-Path $installer)) {
        $url = "https://github.com/jrsoftware/issrc/releases/download/is-$($Version.Replace('.', '_'))/innosetup-$Version.exe"
        # GitHub's release downloads answer 504 now and then; a build must not fail for that.
        for ($attempt = 1; ; $attempt++) {
            try { Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $installer; break }
            catch { if ($attempt -ge 5) { throw }; Write-Host "download failed ($($_.Exception.Message)); attempt $attempt of 5"; Start-Sleep -Seconds (10 * $attempt) }
        }
    }
    $actual = (Get-FileHash -Algorithm SHA256 $installer).Hash.ToLower()
    if ($actual -ne $Sha256) { Remove-Item $installer -Force; throw "innosetup-$Version.exe has SHA-256 $actual, expected $Sha256" }
    $signature = Get-AuthenticodeSignature $installer
    if ($signature.Status -ne 'Valid') { throw "the Inno Setup installer's signature is $($signature.Status)" }

    $existingIss = Test-Path 'HKCU:\Software\Classes\.iss'
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', "/DIR=$inno", '/NOICONS',
        '/MERGETASKS="!associatefileextension,!addcontextmenufiles,!addcontextmenufolders"')
    $process = Start-Process $installer -Wait -PassThru -ArgumentList $arguments
    if ($process.ExitCode -ne 0 -or -not (Test-Path $iscc)) { throw "installing Inno Setup failed (exit $($process.ExitCode))" }

    # This copy is a tool of the repository: it must not appear in Apps or take over .iss files.
    Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue |
        Where-Object { $_.PSChildName -like 'Inno Setup*_is1' } | ForEach-Object { Remove-Item $_.PSPath -Recurse -Force }
    if (-not $existingIss -and (Test-Path 'HKCU:\Software\Classes\.iss')) {
        Remove-Item 'HKCU:\Software\Classes\.iss' -Recurse -Force
        Remove-Item 'HKCU:\Software\Classes\InnoSetupScriptFile' -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$iscc
