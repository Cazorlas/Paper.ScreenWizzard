<#
.SYNOPSIS
  Installs, upgrades, downgrades, repairs and uninstalls the Paper.ScreenWizzard .msi for real and checks every line of
  docs/features/release/SPEC.md against what Windows shows afterwards. Exit 0 only when every check passed and at least one ran.

.DESCRIPTION
  Everything happens under a scratch folder and a per-user install: no administrator rights, and the machine is left as it was
  (the scratch install is removed, the Start menu and Uninstall entries are gone, a "run at logon" value that was there is put back).
  It refuses to start while a copy of the app that it did not start is running, because F4 closes the app and that copy would be
  closed with it.

.PARAMETER Msi
  The package under test (its version is V).

.PARAMETER Version
  V, as x.y.z. Read from the package when not given.
#>
param(
    [Parameter(Mandatory = $true)][string]$Msi,
    [string]$Version,
    [string]$Work = (Join-Path ([IO.Path]::GetTempPath()) ('sw-verify-' + [guid]::NewGuid().ToString('N').Substring(0, 8)))
)

$ErrorActionPreference = 'Stop'
$results = New-Object System.Collections.Generic.List[object]
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$appName = 'Paper.ScreenWizzard'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$uninstallRoot = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
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
if (-not (Test-Path -LiteralPath $Msi)) {
    Check 'F0' 'the package file exists' $false "not found: $Msi"
    Fail-Now "the package to verify is missing: $Msi (build it with installer/build-package.ps1)"
}
$Msi = (Resolve-Path -LiteralPath $Msi).Path

# ---- helpers ------------------------------------------------------------------------------------------------------------
function Open-MsiDatabase([string]$path) {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    return @{ Installer = $installer; Database = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @($path, 0)) }
}

function Query-Msi([string]$path, [string]$sql) {
    $db = Open-MsiDatabase $path
    $view = $db.Database.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db.Database, @($sql))
    $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null) | Out-Null
    $rows = @()
    while ($true) {
        $record = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)
        if ($null -eq $record) { break }
        $count = $record.GetType().InvokeMember('FieldCount', 'GetProperty', $null, $record, $null)
        $row = @()
        for ($i = 1; $i -le $count; $i++) { $row += $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, @($i)) }
        $rows += , $row
    }
    $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null) | Out-Null
    return ,$rows
}

function Msi-Property([string]$path, [string]$name) {
    $rows = Query-Msi $path "SELECT Value FROM Property WHERE Property='$name'"
    if ($rows.Count -eq 0) { return $null }
    return $rows[0][0]
}

function Install-Msi([string]$path, [string]$folder, [string]$log) {
    $arguments = @('/i', "`"$path`"", '/qn', '/norestart', '/l*v', "`"$log`"")
    if ($folder) { $arguments += "INSTALLFOLDER=`"$folder`"" }
    return (Start-Process msiexec.exe -ArgumentList $arguments -Wait -PassThru).ExitCode
}

function Uninstall-Msi([string]$path, [string]$log) {
    return (Start-Process msiexec.exe -ArgumentList @('/x', "`"$path`"", '/qn', '/norestart', '/l*v', "`"$log`"") -Wait -PassThru).ExitCode
}

# Apps lists what is under any of these. A per-user package registers under HKLM on this machine's Windows Installer, so all are searched.
function Arp-Entries {
    $roots = @($uninstallRoot, 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')
    return @($roots | Where-Object { Test-Path $_ } | ForEach-Object { Get-ChildItem $_ } | ForEach-Object { Get-ItemProperty $_.PSPath } | Where-Object { $_.DisplayName -eq $appName })
}

function Is-Elevated {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Start-Menu-Shortcut {
    $programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    return @(Get-ChildItem $programs -Recurse -Filter "$appName*.lnk" -ErrorAction SilentlyContinue)
}

function Exe-Version([string]$exe) { return (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion }

# The nearest version below (Windows Installer compares the first three numbers) and the one just above.
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
$packageVersion = Msi-Property $Msi 'ProductVersion'
if (-not $Version) { $Version = ($packageVersion -split '\.')[0..2] -join '.' }
Check 'Outputs' 'the package carries a version' ([bool]$packageVersion) $packageVersion
$allUsers = Msi-Property $Msi 'ALLUSERS'
Check 'Assumptions' 'installs per user, not per machine (no ALLUSERS)' ([string]::IsNullOrEmpty($allUsers)) "ALLUSERS='$allUsers'"
$launch = Query-Msi $Msi 'SELECT Condition, Description FROM LaunchCondition'
$windowsCondition = @($launch | Where-Object { $_[0] -match 'WINBUILD' -and $_[0] -match '18362' })
Check 'F1' 'a launch condition stops Windows older than 10 version 1903 (build 18362) and says so' ($windowsCondition.Count -eq 1 -and $windowsCondition[0][1] -match '1903') (($launch | ForEach-Object { $_ -join ' => ' }) -join ' | ')
$newer = @($launch | Where-Object { $_[0] -match 'DOWNGRADE' -and $_[1] -match 'newer' })
Check 'F2' 'a launch condition blocks an older package over a newer install and says a newer version is there' ($newer.Count -eq 1) (($newer | ForEach-Object { $_ -join ' => ' }) -join ' | ')

# ---- 1b. the other two outputs sit beside the package and say the truth ---------------------------------------------------------
$packageFolder = Split-Path -Parent $Msi
$zipFile = Join-Path $packageFolder ((Split-Path -Leaf $Msi) -replace '\.msi$', '.zip')
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
    Check 'What the user does 6' 'the checksums match the .msi and the .zip' ($lines.Count -eq 2 -and $bad.Count -eq 0) "$($lines.Count) lines, mismatched: $($bad -join ', ')"
}

# ---- 2. never close a copy that is not ours ----------------------------------------------------------------------------
$foreign = @(Get-Process -Name $appName -ErrorAction SilentlyContinue)
if ($foreign.Count -gt 0) {
    Check 'Harness' 'no copy of the app that this run did not start is running' $false ("process " + (($foreign | ForEach-Object Id) -join ', '))
    Fail-Now 'a copy of the app is running; F4 closes it, so close it yourself first'
}

# ---- 3. remember what is on the machine so it can be put back ---------------------------------------------------------
$runBefore = (Get-ItemProperty -Path $runKey -Name $appName -ErrorAction SilentlyContinue).$appName
$arpBefore = @(Arp-Entries).Count
$marker = Join-Path $dataDir ('installer-verify-' + [guid]::NewGuid().ToString('N') + '.txt')
New-Item -ItemType Directory -Force -Path $Work | Out-Null
$installDir = Join-Path $Work 'app'
$lowerMsi = $null
$higherMsi = $null
$launched = $null

try {
    if ($arpBefore -gt 0) {
        Check 'Harness' 'no copy of the app is installed already' $false "$arpBefore entry/entries in Apps"
        Fail-Now 'the app is installed on this machine; uninstall it first, this script would replace it'
    }

    # the two neighbours of V, built from the same published files so the run stays short
    $lower = Lower-Version $Version
    $higher = Higher-Version $Version
    $lowerMsi = Join-Path $Work "lower-$lower.msi"
    $higherMsi = Join-Path $Work "higher-$higher.msi"
    & (Join-Path $here 'build-package.ps1') -Lite -Version $lower -OutDir $Work -MsiName "lower-$lower.msi" | Out-Null
    & (Join-Path $here 'build-package.ps1') -Lite -Version $higher -OutDir $Work -MsiName "higher-$higher.msi" | Out-Null
    Check 'Setup' 'the older and newer neighbours of the package were built' ((Test-Path $lowerMsi) -and (Test-Path $higherMsi)) "$lower and $higher"

    # ---- the portable zip runs from any folder, and writes only where the app writes ---------------------------------------------------
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

    # ---- install V ----------------------------------------------------------------------------------------------------
    $code = Install-Msi $Msi $installDir (Join-Path $Work 'install.log')
    $exe = Join-Path $installDir "$appName.exe"
    Check 'What the user does 1' 'installing succeeds without a restart request' ($code -eq 0) "exit $code"
    Check 'What the user does 1' 'the exe is in the chosen folder' (Test-Path $exe) $exe
    Check 'Outputs' 'the exe is the version of the package' ((Test-Path $exe) -and (Exe-Version $exe) -like "$Version*") $(if (Test-Path $exe) { Exe-Version $exe } else { 'no exe' })
    Check 'What the user does 2' 'a Start menu shortcut exists' (@(Start-Menu-Shortcut).Count -ge 1) ((Start-Menu-Shortcut | ForEach-Object FullName) -join ', ')
    $arp = @(Arp-Entries)
    Check 'Outputs' 'Apps lists one entry with the name and the version' ($arp.Count -eq 1 -and $arp[0].DisplayVersion -like "$Version*") ($arp | ForEach-Object { "$($_.DisplayName) $($_.DisplayVersion) at $($_.InstallLocation)" })
    $productIcons = @(Get-ChildItem 'HKCU:\Software\Microsoft\Installer\Products' -ErrorAction SilentlyContinue | ForEach-Object { Get-ItemProperty $_.PSPath } | Where-Object { $_.ProductName -eq $appName -and $_.ProductIcon })
    Check 'Outputs' 'the installer registered the product icon and the icon file is there' ($productIcons.Count -eq 1 -and (Test-Path $productIcons[0].ProductIcon)) ($productIcons | ForEach-Object ProductIcon)
    if (Is-Elevated) { Write-Host '[note] Assumptions  this run is elevated, so it does not prove that no administrator rights are needed (run it from a normal window to prove that)' }
    else { Check 'Assumptions' 'the install worked from a normal (not elevated) process: no administrator rights needed' ($code -eq 0) "elevated: no, exit $code" }

    # ---- the app runs, and F4: the running app does not block an upgrade ------------------------------------------
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

    $code = Install-Msi $higherMsi $installDir (Join-Path $Work 'upgrade.log')
    Start-Sleep -Seconds 2
    Check 'F4' 'upgrading while the app runs closes it and does not ask for a restart' ($code -eq 0 -and ($launched.HasExited -or -not (Get-Process -Id $launched.Id -ErrorAction SilentlyContinue))) "exit $code, app running after: $(-not $launched.HasExited)"

    # ---- upgrade: one entry, the new version ----------------------------------------------------------------------
    $arp = @(Arp-Entries)
    Check 'Upgrade' 'after installing the newer file there is exactly one entry' ($arp.Count -eq 1) "$($arp.Count) entries"
    Check 'Upgrade' 'and its version is the newer one' ($arp.Count -eq 1 -and $arp[0].DisplayVersion -like "$higher*") ($arp | ForEach-Object DisplayVersion)
    Check 'Upgrade' 'and the exe is the newer one' ((Test-Path $exe) -and (Exe-Version $exe) -like "$higher*") $(if (Test-Path $exe) { Exe-Version $exe } else { 'no exe' })

    # ---- downgrade refused (F2) -----------------------------------------------------------------------------------
    $code = Install-Msi $lowerMsi $installDir (Join-Path $Work 'downgrade.log')
    Check 'F2' 'installing an older file over a newer one is refused' ($code -ne 0) "exit $code"
    Check 'F2' 'and nothing changed (still the newer version)' (@(Arp-Entries).Count -eq 1 -and @(Arp-Entries)[0].DisplayVersion -like "$higher*") ((@(Arp-Entries) | ForEach-Object DisplayVersion))

    # ---- same file again: still one entry -------------------------------------------------------------------------
    $code = Install-Msi $higherMsi $installDir (Join-Path $Work 'repair.log')
    Check 'Upgrade' 'installing the same file again succeeds and leaves one entry' ($code -eq 0 -and @(Arp-Entries).Count -eq 1) "exit $code, $(@(Arp-Entries).Count) entries"

    # ---- uninstall ------------------------------------------------------------------------------------------------
    $again = Start-Process $exe -ArgumentList '--autostart' -PassThru
    Start-Sleep -Seconds 3
    $code = Uninstall-Msi $higherMsi (Join-Path $Work 'uninstall.log')
    Start-Sleep -Seconds 2
    Check 'F4' 'uninstalling while the app runs closes it and does not ask for a restart' ($code -eq 0 -and -not (Get-Process -Id $again.Id -ErrorAction SilentlyContinue)) "exit $code"
    Check 'Uninstall' 'the install folder is gone' (-not (Test-Path $installDir)) $installDir
    Check 'Uninstall' 'the Start menu shortcut is gone' (@(Start-Menu-Shortcut).Count -eq 0) (@(Start-Menu-Shortcut).Count)
    Check 'Uninstall' 'Apps no longer lists it' (@(Arp-Entries).Count -eq 0) (@(Arp-Entries).Count)
    $runAfter = (Get-ItemProperty -Path $runKey -Name $appName -ErrorAction SilentlyContinue).$appName
    Check 'Uninstall' 'the "run at logon" entry is gone' ($null -eq $runAfter) "value: $runAfter"
    Check 'Uninstall' 'the settings folder of the app and the user files are kept' (Test-Path $marker) $marker

    # ---- F3: a folder that cannot be written ---------------------------------------------------------------------
    # A folder that cannot be made: its parent is an ordinary file. (An ACL that denies the folder does not stop the installer service,
    # which writes as the system, so it would not test anything.)
    $notAFolder = Join-Path $Work 'a-file'
    Set-Content -LiteralPath $notAFolder -Value 'not a folder'
    $code = Install-Msi $Msi (Join-Path $notAFolder 'app') (Join-Path $Work 'unwritable.log')
    Check 'F3' 'a folder that cannot be made makes the install fail' ($code -ne 0) "exit $code"
    Check 'F3' 'and leaves nothing installed' (@(Arp-Entries).Count -eq 0 -and @(Start-Menu-Shortcut).Count -eq 0) "$(@(Arp-Entries).Count) entries, $(@(Start-Menu-Shortcut).Count) shortcuts"
}
catch {
    Check 'Harness' 'the run itself did not throw' $false $_.Exception.Message
}
finally {
    if ($launched -and -not $launched.HasExited) { Stop-Process -Id $launched.Id -Force -ErrorAction SilentlyContinue }
    Get-Process -Name $appName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    foreach ($leftover in @($higherMsi, $lowerMsi, $Msi)) {
        if ($leftover -and (Test-Path $leftover) -and @(Arp-Entries).Count -gt 0) { Uninstall-Msi $leftover (Join-Path $Work 'cleanup.log') | Out-Null }
    }
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
