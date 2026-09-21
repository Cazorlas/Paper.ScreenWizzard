<#
.SYNOPSIS
  Installs, upgrades, downgrades, repairs and uninstalls Paper.ScreenWizzard's Setup.exe for real and checks every line of
  docs/features/release/SPEC.md against what Windows shows afterwards. Exit 0 only when every check passed and at least one ran.

.DESCRIPTION
  Everything happens under a scratch folder and a per-user install: no administrator rights, and the machine is left as it was
  (the scratch install is removed, the Start menu and Apps entries are gone, a "run at logon" value that was there is put back).
  It refuses to start while a copy of the app is running or installed, because F4 closes the app and the upgrade replaces it.

.PARAMETER Setup
  The Setup.exe under test (its version is V).

.PARAMETER Version
  V, as x.y.z. Read from the file when not given.
#>
param(
    [Parameter(Mandatory = $true)][string]$Setup,
    [string]$Version,
    [string]$Work = (Join-Path ([IO.Path]::GetTempPath()) ('sw-verify-' + [guid]::NewGuid().ToString('N').Substring(0, 8)))
)

$ErrorActionPreference = 'Stop'
$results = New-Object System.Collections.Generic.List[object]
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$appName = 'Paper.ScreenWizzard'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$appKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{5C0E8A3B-7D14-4E6F-A2B9-3F8D1C6A9E40}_is1'
$dataDir = Join-Path $env:APPDATA 'Paper\ScreenWizzard'

function Check([string]$code, [string]$what, [bool]$ok, $value) {
    $script:results.Add([pscustomobject]@{ Code = $code; Check = $what; Pass = $ok; Value = "$value" })
    $mark = if ($ok) { 'pass' } else { 'FAIL' }
    Write-Host ("[{0}] {1}  {2}  -> {3}" -f $mark, $code, $what, $value)
}

function Fail-Now([string]$message) {
    Write-Host "verify-installer: $message"
    exit 1
}

# ---- 0. the package is there --------------------------------------------------------------------------------------------
if (-not (Test-Path -LiteralPath $Setup)) {
    Check 'F0' 'the package file exists' $false "not found: $Setup"
    Fail-Now "the package to verify is missing: $Setup (build it with installer/build-package.ps1)"
}
$Setup = (Resolve-Path -LiteralPath $Setup).Path

# ---- helpers ------------------------------------------------------------------------------------------------------------
# Waits for THIS process only (not for whatever it started), and gives up after a while: a stuck installer must fail the check, not the run.
function Wait-Exit([string]$file, [string[]]$arguments, [int]$seconds = 180) {
    $process = Start-Process $file -ArgumentList $arguments -PassThru
    if (-not $process.WaitForExit($seconds * 1000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Write-Host "[timeout] $file did not finish in $seconds seconds"
        return -999
    }
    return $process.ExitCode
}

function Run-Setup([string]$path, [string]$folder, [string]$log, [string]$language) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$log`"")
    if ($folder) { $arguments += "/DIR=`"$folder`"" }
    if ($language) { $arguments += "/LANG=$language" }
    Write-Host "  ... $(Split-Path -Leaf $path) $language"
    return (Wait-Exit $path $arguments)
}

function Run-Uninstall([string]$folder) {
    $unins = Join-Path $folder 'unins000.exe'
    if (-not (Test-Path $unins)) { return -1 }
    Write-Host '  ... unins000.exe'
    $code = Wait-Exit $unins @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
    # The uninstaller hands the work to a copy of itself in the temp folder and returns at once: wait for the folder and that copy to go.
    for ($i = 0; $i -lt 240 -and ((Test-Path $folder) -or (Get-Process -Name 'unins*' -ErrorAction SilentlyContinue)); $i++) { Start-Sleep -Milliseconds 250 }
    return $code
}

function App-Entry { return @(Get-Item -Path $appKey -ErrorAction SilentlyContinue | ForEach-Object { Get-ItemProperty $_.PSPath }) }

function Machine-Entries {
    $roots = @('HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')
    return @($roots | Where-Object { Test-Path $_ } | ForEach-Object { Get-ChildItem $_ } | ForEach-Object { Get-ItemProperty $_.PSPath } | Where-Object { $_.DisplayName -like "$appName*" })
}

function Is-Elevated {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Start-Menu-Shortcut {
    $programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    return @(Get-ChildItem $programs -Recurse -Filter "$appName*.lnk" -ErrorAction SilentlyContinue)
}

function Exe-Version([string]$exe) { return (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion }

# The nearest version below and the one just above (the installer compares the first three numbers).
function Lower-Version([string]$v) {
    $p = $v.Split('.') | ForEach-Object { [int]$_ }
    if ($p[2] -gt 0) { return ('{0}.{1}.{2}' -f $p[0], $p[1], ($p[2] - 1)) }
    if ($p[1] -gt 0) { return ('{0}.{1}.{2}' -f $p[0], ($p[1] - 1), 99) }
    if ($p[0] -gt 0) { return ('{0}.{1}.{2}' -f ($p[0] - 1), 99, 99) }
    throw "there is no version below $v"
}
function Higher-Version([string]$v) {
    $p = $v.Split('.') | ForEach-Object { [int]$_ }
    return ('{0}.{1}.{2}' -f $p[0], $p[1], ($p[2] + 1))
}

# ---- 1. the package says what the SPEC says ---------------------------------------------------------------------------
$fileVersion = (Get-Item -LiteralPath $Setup).VersionInfo.FileVersion.Trim()
if (-not $Version) { $Version = (($fileVersion -split '\.')[0..2]) -join '.' }
Check 'Outputs' 'the Setup.exe carries the version' ($fileVersion -like "$Version*") $fileVersion
$script = Get-Content -Raw (Join-Path $here 'Setup.iss')
Check 'F1' 'the installer refuses Windows older than 10 version 1903 (build 18362): MinVersion is set (read from the script; there is no old Windows here to run it on)' ($script -match '(?m)^MinVersion=10\.0\.18362\s*$') 'MinVersion=10.0.18362'
Check 'What the user does 1' 'the installer carries English and Vietnamese' (($script -match '(?m)^Name: "en"') -and ($script -match '(?m)^Name: "vi"') -and (Test-Path (Join-Path $here 'Languages\Vietnamese.isl'))) 'en, vi'
Check 'Assumptions' 'installs per user: no administrator rights asked (PrivilegesRequired=lowest)' ($script -match '(?m)^PrivilegesRequired=lowest\s*$') 'PrivilegesRequired=lowest'

# ---- 1b. the other two outputs sit beside the package and say the truth ---------------------------------------------------------
$packageFolder = Split-Path -Parent $Setup
$zipFile = Join-Path $packageFolder ((Split-Path -Leaf $Setup) -replace '-Setup\.exe$', '.zip')
$sumsFile = Join-Path $packageFolder 'SHA256SUMS.txt'
Check 'What the user does 5' 'the portable zip is beside the package' (Test-Path $zipFile) $zipFile
Check 'What the user does 6' 'SHA256SUMS.txt is beside the package' (Test-Path $sumsFile) $sumsFile
if (Test-Path $sumsFile) {
    $lines = @(Get-Content $sumsFile | Where-Object { $_ })
    $bad = @()
    foreach ($line in $lines) {
        $hash, $name = $line -split '\s+', 2
        $file = Join-Path $packageFolder $name.Trim()
        if (-not (Test-Path $file) -or (Get-FileHash -Algorithm SHA256 $file).Hash.ToLower() -ne $hash) { $bad += $name }
    }
    Check 'What the user does 6' 'the checksums match the Setup.exe and the .zip' ($lines.Count -eq 2 -and $bad.Count -eq 0) "$($lines.Count) lines, mismatched: $($bad -join ', ')"
}

# ---- 2. never close or replace a copy that is not ours -----------------------------------------------------------------
$foreign = @(Get-Process -Name $appName -ErrorAction SilentlyContinue)
if ($foreign.Count -gt 0) {
    Check 'Harness' 'no copy of the app that this run did not start is running' $false ("process " + (($foreign | ForEach-Object Id) -join ', '))
    Fail-Now 'a copy of the app is running; F4 closes it, so close it yourself first'
}
if (@(App-Entry).Count -gt 0) {
    Check 'Harness' 'no copy of the app is installed already' $false 'an entry exists in Apps'
    Fail-Now 'the app is installed on this machine; uninstall it first, this script would replace it'
}

# ---- 3. remember what is on the machine so it can be put back ---------------------------------------------------------
$runBefore = (Get-ItemProperty -Path $runKey -Name $appName -ErrorAction SilentlyContinue).$appName
$marker = Join-Path $dataDir ('installer-verify-' + [guid]::NewGuid().ToString('N') + '.txt')
New-Item -ItemType Directory -Force -Path $Work | Out-Null
$installDir = Join-Path $Work 'app'
$launched = $null

try {
    # the two neighbours of V, built from the same source so the run stays short
    $lower = Lower-Version $Version
    $higher = Higher-Version $Version
    & (Join-Path $here 'build-package.ps1') -Lite -Version $lower -OutDir $Work | Out-Null
    & (Join-Path $here 'build-package.ps1') -Lite -Version $higher -OutDir $Work | Out-Null
    $lowerSetup = Join-Path $Work "Paper.ScreenWizzard-$lower-win-x64-Setup.exe"
    $higherSetup = Join-Path $Work "Paper.ScreenWizzard-$higher-win-x64-Setup.exe"
    Check 'Setup' 'the older and newer neighbours of the package were built' ((Test-Path $lowerSetup) -and (Test-Path $higherSetup)) "$lower and $higher"

    # ---- the portable zip runs from any folder, and writes only where the app writes ---------------------------------
    if (Test-Path $zipFile) {
        $portable = Join-Path $Work 'portable'
        Expand-Archive -LiteralPath $zipFile -DestinationPath $portable
        $portableExe = Join-Path $portable "$appName.exe"
        $env:PAPER_SCREENWIZZARD_DATA = Join-Path $Work 'portable-data'
        $env:PAPER_SCREENWIZZARD_INSTANCE = 'Paper.ScreenWizzard.Portable.' + [guid]::NewGuid().ToString('N')
        $portableRun = Start-Process $portableExe -ArgumentList '--autostart' -PassThru
        Start-Sleep -Seconds 4
        $alive = -not $portableRun.HasExited
        if ($alive) { Stop-Process -Id $portableRun.Id -Force }
        $extra = @(Get-ChildItem $portable -Recurse -File | Where-Object { $_.Name -notin @("$appName.exe", 'LICENSE') })
        Check 'What the user does 5' 'the unzipped exe runs from any folder' $alive $portableExe
        Check 'What the user does 5' 'the zip holds only the exe and the licence, and running it wrote nothing beside itself' ($extra.Count -eq 0) (($extra | ForEach-Object Name) -join ', ')
    }

    # ---- install V (in Vietnamese, to prove the second language installs too) ---------------------------------------
    $log = Join-Path $Work 'install.log'
    $code = Run-Setup $Setup $installDir $log 'vi'
    $exe = Join-Path $installDir "$appName.exe"
    Check 'What the user does 1' 'installing succeeds' ($code -eq 0) "exit $code"
    $language = (Get-ItemProperty -Path $appKey -Name 'Inno Setup: Language' -ErrorAction SilentlyContinue).'Inno Setup: Language'
    Check 'What the user does 1' 'asked for Vietnamese, the installer ran in Vietnamese (the language it recorded)' ($language -eq 'vi') "language: $language"
    Check 'What the user does 1' 'the exe is in the chosen folder' (Test-Path $exe) $exe
    Check 'Outputs' 'the exe is the version of the package' ((Test-Path $exe) -and (Exe-Version $exe) -like "$Version*") $(if (Test-Path $exe) { Exe-Version $exe } else { 'no exe' })
    Check 'What the user does 2' 'a Start menu shortcut exists' (@(Start-Menu-Shortcut).Count -ge 1) ((@(Start-Menu-Shortcut) | ForEach-Object FullName) -join ', ')
    $entry = @(App-Entry)
    Check 'Outputs' 'Apps lists one entry with the name, the version, the icon and the folder' ($entry.Count -eq 1 -and $entry[0].DisplayName -like "$appName*" -and $entry[0].DisplayVersion -like "$Version*" -and $entry[0].DisplayIcon -and (Test-Path ($entry[0].DisplayIcon -replace '"', '' -replace ',\d+$', ''))) ($entry | ForEach-Object { "$($_.DisplayName) | $($_.DisplayVersion) | $($_.DisplayIcon) | $($_.InstallLocation)" })
    Check 'Assumptions' 'the entry is under the user''s own registry, nothing was written for all users' ($entry.Count -eq 1 -and @(Machine-Entries).Count -eq 0) "machine entries: $(@(Machine-Entries).Count)"
    if (Is-Elevated) { Write-Host '[note] Assumptions  this run is elevated, so it does not prove that no administrator rights are needed (run it from a normal window to prove that)' }
    else { Check 'Assumptions' 'the install worked from a normal (not elevated) process: no administrator rights needed' ($code -eq 0) "elevated: no, exit $code" }

    # ---- the app runs, and F4: the running app does not block an upgrade -------------------------------------------
    $env:PAPER_SCREENWIZZARD_DATA = Join-Path $Work 'data'
    $env:PAPER_SCREENWIZZARD_INSTANCE = 'Paper.ScreenWizzard.Verify.' + [guid]::NewGuid().ToString('N')
    $launched = Start-Process $exe -ArgumentList '--autostart' -PassThru
    Start-Sleep -Seconds 4
    Check 'What the user does 2' 'the installed app starts and stays running' (-not $launched.HasExited) "pid $($launched.Id)"

    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    Set-Content -LiteralPath $marker -Value 'the user data must survive an uninstall'
    # A fresh profile (a CI runner) may not have the Run key at all.
    if (-not (Test-Path $runKey)) { New-Item -Path $runKey -Force | Out-Null }
    Set-ItemProperty -Path $runKey -Name $appName -Value "`"$exe`" --autostart"

    $code = Run-Setup $higherSetup $installDir (Join-Path $Work 'upgrade.log') 'en'
    Start-Sleep -Seconds 2
    Check 'F4' 'upgrading while the app runs closes it and does not ask for a restart' ($code -eq 0 -and -not (Get-Process -Id $launched.Id -ErrorAction SilentlyContinue)) "exit $code, app still running: $([bool](Get-Process -Id $launched.Id -ErrorAction SilentlyContinue))"

    $language = (Get-ItemProperty -Path $appKey -Name 'Inno Setup: Language' -ErrorAction SilentlyContinue).'Inno Setup: Language'
    Check 'What the user does 1' 'asked for English, the installer ran in English' ($language -eq 'en') "language: $language"

    # ---- upgrade: one entry, the new version -----------------------------------------------------------------------
    $entry = @(App-Entry)
    Check 'Upgrade' 'after installing the newer file there is exactly one entry' ($entry.Count -eq 1) "$($entry.Count) entries"
    Check 'Upgrade' 'and its version is the newer one' ($entry.Count -eq 1 -and $entry[0].DisplayVersion -like "$higher*") ($entry | ForEach-Object DisplayVersion)
    Check 'Upgrade' 'and the exe is the newer one' ((Test-Path $exe) -and (Exe-Version $exe) -like "$higher*") $(if (Test-Path $exe) { Exe-Version $exe } else { 'no exe' })

    # ---- downgrade refused (F2) -----------------------------------------------------------------------------------
    $code = Run-Setup $lowerSetup $installDir (Join-Path $Work 'downgrade.log') 'en'
    Check 'F2' 'installing an older file over a newer one is refused' ($code -ne 0) "exit $code"
    Check 'F2' 'and nothing changed (still the newer version)' (@(App-Entry).Count -eq 1 -and (@(App-Entry))[0].DisplayVersion -like "$higher*" -and (Exe-Version $exe) -like "$higher*") ((@(App-Entry)) | ForEach-Object DisplayVersion)

    # ---- same file again: still one entry -------------------------------------------------------------------------
    $code = Run-Setup $higherSetup $installDir (Join-Path $Work 'repair.log') ''
    Check 'Upgrade' 'installing the same file again succeeds and leaves one entry' ($code -eq 0 -and @(App-Entry).Count -eq 1) "exit $code, $(@(App-Entry).Count) entries"
    # No language asked for: the wizard follows the Windows display language (Vietnamese on a Vietnamese Windows, English otherwise).
    $expected = if ([System.Globalization.CultureInfo]::CurrentUICulture.TwoLetterISOLanguageName -eq 'vi') { 'vi' } else { 'en' }
    $language = (Get-ItemProperty -Path $appKey -Name 'Inno Setup: Language' -ErrorAction SilentlyContinue).'Inno Setup: Language'
    Check 'What the user does 1' 'with no language asked for, the installer follows the Windows display language' ($language -eq $expected) "Windows display language $([System.Globalization.CultureInfo]::CurrentUICulture.Name) -> $language"

    # ---- uninstall, with the app running ----------------------------------------------------------------------------
    $again = Start-Process $exe -ArgumentList '--autostart' -PassThru
    Start-Sleep -Seconds 3
    $code = Run-Uninstall $installDir
    Check 'F4' 'uninstalling while the app runs closes it and does not ask for a restart' ($code -eq 0 -and -not (Get-Process -Id $again.Id -ErrorAction SilentlyContinue)) "exit $code"
    Check 'Uninstall' 'the install folder is gone' (-not (Test-Path $installDir)) $installDir
    Check 'Uninstall' 'the Start menu shortcut is gone' (@(Start-Menu-Shortcut).Count -eq 0) (@(Start-Menu-Shortcut).Count)
    Check 'Uninstall' 'Apps no longer lists it' (@(App-Entry).Count -eq 0) (@(App-Entry).Count)
    $runAfter = (Get-ItemProperty -Path $runKey -Name $appName -ErrorAction SilentlyContinue).$appName
    Check 'Uninstall' 'the "run at logon" entry is gone' ($null -eq $runAfter) "value: $runAfter"
    Check 'Uninstall' 'the settings folder of the app and the user files are kept' (Test-Path $marker) $marker

    # ---- F3: a folder that cannot be made -------------------------------------------------------------------------
    # Its parent is an ordinary file. (An ACL that denies the folder proves nothing when the installer writes as the system.)
    $notAFolder = Join-Path $Work 'a-file'
    Set-Content -LiteralPath $notAFolder -Value 'not a folder'
    $code = Run-Setup $Setup (Join-Path $notAFolder 'app') (Join-Path $Work 'unwritable.log') 'en'
    Check 'F3' 'a folder that cannot be made makes the install fail' ($code -ne 0) "exit $code"
    Check 'F3' 'and leaves nothing installed' (@(App-Entry).Count -eq 0 -and @(Start-Menu-Shortcut).Count -eq 0) "$(@(App-Entry).Count) entries, $(@(Start-Menu-Shortcut).Count) shortcuts"
}
catch {
    Check 'Harness' 'the run itself did not throw' $false $_.Exception.Message
}
finally {
    if ($launched -and -not $launched.HasExited) { Stop-Process -Id $launched.Id -Force -ErrorAction SilentlyContinue }
    Get-Process -Name $appName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (@(App-Entry).Count -gt 0) { Run-Uninstall $installDir | Out-Null }
    Get-ChildItem "$env:APPDATA\Microsoft\Windows\Start Menu\Programs" -Filter "$appName*.lnk" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $marker -Force -ErrorAction SilentlyContinue
    if ($null -ne $runBefore) { Set-ItemProperty -Path $runKey -Name $appName -Value $runBefore } else { Remove-ItemProperty -Path $runKey -Name $appName -ErrorAction SilentlyContinue }
    Remove-Item Env:\PAPER_SCREENWIZZARD_DATA, Env:\PAPER_SCREENWIZZARD_INSTANCE -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $Work -Recurse -Force -ErrorAction SilentlyContinue
}

$failed = @($results | Where-Object { -not $_.Pass })
Write-Host ''
Write-Host ("verify-installer: {0} checks, {1} passed, {2} failed" -f $results.Count, ($results.Count - $failed.Count), $failed.Count)
if ($results.Count -eq 0) { Write-Host 'no check ran: that is not a pass'; exit 1 }
if ($failed.Count -gt 0) { exit 1 }
exit 0
