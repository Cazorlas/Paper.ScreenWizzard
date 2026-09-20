# Shared session half of the kit's Stop hooks: when this session began, which files it changed, which
# CODEMAP.md covers a file, what a plan's status line says, which tests a runner reported red.
# Dot-sourced; declares no param() block.
#
# Why a stamp and modification times, not `git status`: the dirty working tree holds whatever was sitting
# uncommitted when the session opened, and a Stop hook that reads it re-fires on every stop of a session
# that changed nothing - which is how a reminder teaches people to stop reading it. And not the transcript's
# Edit/Write calls either: an edit made through a shell command leaves no tool_use entry.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI. Vietnamese is matched through \u escapes
# or by stripping diacritics first.

# Folders that hold build output, caches or other tools' state - never source someone edited by hand.
$script:PaperPrunedDirs = @('.git', '.vs', '.idea', 'bin', 'obj', 'node_modules', 'packages', 'TestResults', '__pycache__', '.venv', 'venv')

function Get-PaperSessionStampPath([string] $SessionId) {
    $id = ([string] $SessionId) -replace '[^A-Za-z0-9\-_]', ''
    if (-not $id) { return $null }
    $temp = $env:TEMP
    if (-not $temp) { $temp = [System.IO.Path]::GetTempPath() }
    return Join-Path $temp "paper-kit-session-$id.stamp"
}

# When this session began, or $null. With -Create a missing stamp is written now and $null is still
# returned: with no stamp there is no telling this session's edits from what was already there, and a
# missed reminder costs less than one that fires on work the session never touched.
function Get-PaperSessionStart([string] $SessionId, [switch] $Create) {
    $stamp = Get-PaperSessionStampPath $SessionId
    if (-not $stamp) { return $null }
    if (Test-Path -LiteralPath $stamp -PathType Leaf) { return (Get-Item -LiteralPath $stamp).LastWriteTime }
    if ($Create) {
        try { [System.IO.File]::WriteAllText($stamp, (Get-Date).ToString('o')) } catch { }
    }
    return $null
}

# A reminder is said once per state. stop_hook_active guards only the retry of one stop; the next stop with
# nothing changed fired the same reminder again on every stop of a session, and a real project's user asked
# for once. The key is the reminder's text (plus whatever the caller adds, such as the newest code write):
# the same key as the last one this hook showed in this session is a repeat, any change is news again.
# True = repeat, say nothing; false = recorded, say it. No session id, or state that cannot be written: false.
function Test-PaperNudgeRepeated([string] $SessionId, [string] $Hook, [string] $Key) {
    $stamp = Get-PaperSessionStampPath $SessionId
    if (-not $stamp) { return $false }
    $state = $stamp -replace '\.stamp$', ".$Hook.last"
    $normal = ([string] $Key).Replace("`r", '').Trim()
    try {
        if ((Test-Path -LiteralPath $state -PathType Leaf) -and ([System.IO.File]::ReadAllText($state).Replace("`r", '').Trim() -eq $normal)) { return $true }
        [System.IO.File]::WriteAllText($state, $normal)
    } catch { }
    return $false
}

function ConvertTo-PaperExtensions($Value, [string[]] $Default) {
    $list = @($Value | Where-Object { $_ } | ForEach-Object { $e = ([string] $_).Trim().ToLowerInvariant(); if (-not $e.StartsWith('.')) { $e = '.' + $e }; $e })
    if ($list.Count -eq 0) { return $Default }
    return $list
}

# What git says is yours right now: the files dirty against HEAD. Returns $null when this is not a git
# repo, git is not on PATH, or git cannot read the folder - and the caller then falls back to mtime.
#
# It exists because mtime does not mean "you changed this" (KIT-001). Git rewrites a file's
# LastWriteTime whenever it puts it on disk - merge, pull, rebase, checkout, stash pop - so after
# bringing somebody else's branch into main, every file they ever touched reads as changed by you. The
# hook then names a plan from another session and tells you to tick its tasks.
#
# WHY ONLY DIRTY FILES, AND NOT ALSO "COMMITS MADE SINCE THE SESSION BEGAN". That was the first fix, and
# it was still wrong. A session lasts as long as it lasts - many hours - and `git log --since` over that
# window picks up every commit ANOTHER session pushed to main in the same hours. Measured: after the
# first fix shipped, this hook still named nine maps and two plans, none of them this session's work.
# `--first-parent` drops what arrives through a merge commit but not what was pushed straight to main,
# so it does not close the hole either. Closing it properly needs the session's starting commit recorded
# at SessionStart, which is state this hook does not have.
#
# So it reports uncommitted work only. That is a real loss - work you commit as you go stops being
# reminded about - and it is the trade this file already states above: a missed reminder costs less than
# one that fires on work the session never touched. The gate that catches an unticked task after a
# commit is /task-verify, which reads the plan rather than the clock.
function Get-PaperGitTouchedFiles([string] $Root) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root '.git'))) { return $null }

    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

    # A hook runs under whatever preference its caller set. Under Stop, a single line git writes to
    # stderr becomes a terminating error, the catch below returns $null, and the reminder quietly goes
    # back to trusting mtime - the bug this function exists to fix, back without a sound.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        # Dirty or untracked. Porcelain v1 puts the path last, and a rename carries "old -> new".
        $global:LASTEXITCODE = 0
        $status = @(& git --no-pager -C $Root status --porcelain --untracked-files=all 2>$null)

        # A failed git and a clean tree both print nothing, and treating the first as the second would
        # hide every file in the project. A folder can hold a .git that git cannot read - a fixture
        # builds one, and so does a half-finished clone.
        if ($LASTEXITCODE -ne 0) { return $null }

        foreach ($line in $status) {
            if (-not $line -or $line.Length -lt 4) { continue }
            $path = $line.Substring(3).Trim()
            $arrow = $path.IndexOf(' -> ')
            if ($arrow -ge 0) { $path = $path.Substring($arrow + 4) }
            [void] $set.Add($path.Trim('"').Replace([char] 92, [char] 47))
        }
    }
    catch { return $null }
    finally { $ErrorActionPreference = $previous }

    # Comma-wrapped, like every other collection returned in this kit: PowerShell unrolls a collection on
    # return, so an EMPTY set comes back as $null - and $null is this function's signal for "no git here",
    # which sends the caller straight back to trusting mtime. A clean tree is exactly when that matters.
    return , $set
}

# Files under Root with one of Extensions, written after Since. A plain walk that prunes build output,
# because .NET enumeration is Unicode end to end (a Vietnamese folder is read, not skipped) and it needs no
# git. It gives up after MaxSeconds and returns what it has, so a huge tree cannot eat the hook's timeout.
function Get-PaperChangedFiles([string] $Root, [datetime] $Since, [string[]] $Extensions, [int] $MaxSeconds = 6) {
    $touched = Get-PaperGitTouchedFiles $Root
    $found = New-Object System.Collections.ArrayList
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $stack = New-Object System.Collections.Stack
    $stack.Push((New-Object System.IO.DirectoryInfo $Root))
    while ($stack.Count -gt 0) {
        if ($watch.Elapsed.TotalSeconds -gt $MaxSeconds) { break }
        $dir = $stack.Pop()
        try {
            foreach ($sub in $dir.GetDirectories()) {
                if ($script:PaperPrunedDirs -contains $sub.Name) { continue }
                if ($sub.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { continue }
                $stack.Push($sub)
            }
            foreach ($file in $dir.GetFiles()) {
                if ($Extensions -notcontains $file.Extension.ToLowerInvariant()) { continue }
                if ($file.LastWriteTime -le $Since) { continue }
                # mtime says "written since"; git says "written BY THIS SESSION". Both, when git can say.
                if ($null -ne $touched -and -not $touched.Contains((Get-PaperRelative $Root $file.FullName))) { continue }
                # mtime says "written since"; git says "written BY THIS SESSION". Both, when git can say.
                [void] $found.Add($file)
            }
        }
        catch { continue }
    }
    return @($found)
}

function Get-PaperRelative([string] $Root, [string] $Path) {
    $r = $Root.TrimEnd('\', '/') + '\'
    if ($Path.StartsWith($r, [StringComparison]::OrdinalIgnoreCase)) { return $Path.Substring($r.Length).Replace('\', '/') }
    return $Path.Replace('\', '/')
}

# The CODEMAP.md nearest above a file, never above Root. $null when no folder on the way has one.
function Get-PaperNearestCodeMap([string] $Root, [string] $FilePath) {
    $rootFull = $Root.TrimEnd('\', '/')
    $dir = Split-Path -Parent $FilePath
    while ($dir -and $dir.Length -ge $rootFull.Length -and $dir.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        $candidate = Join-Path $dir 'CODEMAP.md'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        if ($dir.Length -eq $rootFull.Length) { break }
        $dir = Split-Path -Parent $dir
    }
    return $null
}

function Get-PaperPlainStatus([string] $Text) {
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $Text.Normalize([System.Text.NormalizationForm]::FormD).ToCharArray()) {
        if ([System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch) -ne [System.Globalization.UnicodeCategory]::NonSpacingMark) { [void] $sb.Append($ch) }
    }
    # d with stroke has no decomposition.
    return $sb.ToString().Replace([char]0x0111, 'd').Replace([char]0x0110, 'D').ToLowerInvariant()
}

# Approved | Done | Pending | $null for a plan's status line, read the way the task gate reads it.
function Get-PaperPlanStatus([string] $PlanPath) {
    $lines = @(Get-Content -LiteralPath $PlanPath -Encoding UTF8 -TotalCount 15)
    foreach ($line in $lines) {
        $plain = Get-PaperPlainStatus $line
        if ($plain -match '^\*\*(trang thai|status):?\*\*:?\s*(.*)$') {
            $s = $Matches[2].Trim()
            if ($s -match '^(xong|done)\b') { return 'Done' }
            if ($s -match '^(da duyet|approved)\b') { return 'Approved' }
            return 'Pending'
        }
    }
    return $null
}

# Test names a runner printed as failed: "failed Name" (Microsoft.Testing.Platform), "  Failed Name [3 ms]"
# (VSTest), "FAILED path::name" (pytest), "FAIL name". A summary such as "Failed: 3" names nothing.
function Get-PaperFailedTestNames([string[]] $Lines) {
    $names = New-Object System.Collections.Generic.List[string]
    foreach ($line in $Lines) {
        if ([string] $line -match '(?i)^\s*(?:failed|fail)\s+([A-Za-z_][^\s]*)') {
            if (-not $names.Contains($Matches[1])) { $names.Add($Matches[1]) }
        }
    }
    return , $names.ToArray()
}

function Read-PaperKnownFailures([string] $Path) {
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return @() }
    return @(Get-Content -LiteralPath $Path -Encoding UTF8 | ForEach-Object { ([string] $_).Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
}
