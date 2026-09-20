# One project, one long verb at a time: the claim a build, a test or a publish holds while it runs, and
# the rules for reading someone else's.
#
# Why it exists: two processes building the same project at the same moment do not fail honestly. The
# second one dies on an output file the first still holds, and that gets reported as "the build is red"
# for code that compiles perfectly. So every run that can take minutes takes the claim first, and whoever
# finds it taken stands down - runs nothing, and says it verified nothing - instead of building on top.
#
# Two halves, on purpose:
#   - the decisions: Get-PaperRunLockVerdict (claim text in, verdict out - including whether the file may be
#     deleted), Test-PaperRunLockOwned ("is this claim mine") and Get-PaperRunLockContendedVerdict (every
#     attempt to take it failed: held, or the folder refused). No file system, no process, no clock: the caller
#     passes the time and a function that says what a pid's start time is now.
#   - the shell: where the claim lives, taking it, recording the command's process, giving it back.
#
# The claim: %LOCALAPPDATA%\paper-kit\run-locks\<folder>-<hash>.json, or TEMP when there is no
# LOCALAPPDATA. One file per project folder, holding who (pid and the start time of that process), what
# (verb), since when, the branch, and - once it has started - the process that runs the command. So a
# reader can say WHO is busy and SINCE WHEN, which a named mutex could never do.
#
# Taking it is atomic: the file is created with FileMode.CreateNew, so of two takers at the same instant
# exactly one gets it (measured before: two builds 1.5 s apart both ran). The loser reads the claim to say
# who holds it.
#
# Alive means: the process that took the claim, OR the process running its command, is alive AND started at
# the recorded time. A pid alone is not enough - pids are handed out again, and a claim once read as held
# because its pid had come to belong to an unrelated desktop process. The command's process counts because
# killing the runner does not kill the build it started, and that orphan still holds the output files.
#
# A claim nobody keeps must never block anyone: nobody alive, older than the maximum age, or a file that
# cannot be parsed counts as no claim, is deleted, and the reader carries on. A file that cannot be READ holds
# nobody either, but is not deleted: it may be a claim being written this instant. The age limit is the
# safety net for anything the start-time check cannot see.
#
# Dot-sourced by the flow runner and by the Stop hook; declares no param() block and starts no process.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

# The verbs that can run long enough for two of them to collide. Anything else needs no claim.
$script:PaperRunLockVerbs = @('build', 'test', 'publish')

# Past this many minutes a claim is assumed abandoned, whatever its processes say.
$script:PaperRunLockMaxMinutes = 30

# How many times a taker tries again when the claim file is there but turns out to hold nobody (it was
# deleted as stale, or was being written at the moment it was read).
$script:PaperRunLockAttempts = 5

function Get-PaperRunLockDirectory {
    $base = $env:LOCALAPPDATA
    if ([string]::IsNullOrWhiteSpace($base)) { $base = $env:TEMP }
    if ([string]::IsNullOrWhiteSpace($base)) { $base = [System.IO.Path]::GetTempPath() }
    return (Join-Path $base 'paper-kit\run-locks')
}

# One name per project folder: a readable piece of the folder name, plus a hash of the whole path so two
# folders with the same name never share a claim. Case and a trailing separator are the same project.
function Get-PaperRunLockKey([string] $RepoRoot) {
    $normal = ([string] $RepoRoot).Trim().Replace('/', '\').TrimEnd('\').ToLowerInvariant()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $hash = ([BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($normal))).Replace('-', '')).Substring(0, 12).ToLowerInvariant()
    $leaf = [string] (Split-Path -Leaf $normal)
    $leaf = $leaf -replace '[^a-z0-9\-_]', '-'
    if ([string]::IsNullOrWhiteSpace($leaf)) { $leaf = 'project' }
    if ($leaf.Length -gt 24) { $leaf = $leaf.Substring(0, 24) }
    return ($leaf + '-' + $hash)
}

function Get-PaperRunLockPath([string] $RepoRoot) {
    return (Join-Path (Get-PaperRunLockDirectory) ((Get-PaperRunLockKey $RepoRoot) + '.json'))
}

# ---- the decisions (pure) ------------------------------------------------------------------------------

function Get-PaperClaimField($Claim, [string] $Name) {
    if ($null -eq $Claim) { return $null }
    $prop = $Claim.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

# The claim text as an object, or $null when it is not a JSON object.
function ConvertFrom-PaperClaimText($Text) {
    if ([string]::IsNullOrWhiteSpace([string] $Text)) { return $null }
    $claim = $null
    try { $claim = [string] $Text | ConvertFrom-Json } catch { return $null }
    if ($null -eq $claim -or $claim -isnot [System.Management.Automation.PSCustomObject]) { return $null }
    return $claim
}

# A positive whole number out of a claim field, or 0.
function Get-PaperClaimNumber($Claim, [string] $Name) {
    $n = [long] 0
    if (-not [long]::TryParse([string] (Get-PaperClaimField $Claim $Name), [ref] $n)) { return [long] 0 }
    if ($n -le 0) { return [long] 0 }
    return $n
}

# Remove: the claim file may be deleted by whoever read it. Every stale claim may, except one that could not be
# read ($Removable false): that may be a claim being written this instant, and deleting it would free a held lock.
function New-PaperRunLockVerdict([bool] $Held, [bool] $Stale, [string] $Reason, [int] $ProcessId, [string] $Verb, $StartedAt, [string] $Branch, $AgeMinutes, [int] $ChildProcessId = 0, [bool] $Removable = $true) {
    return [pscustomobject]@{
        Held           = $Held
        Stale          = $Stale
        Remove         = ($Stale -and $Removable)
        Reason         = $Reason
        ProcessId      = $ProcessId
        ChildProcessId = $ChildProcessId
        Verb           = $Verb
        StartedAt      = $StartedAt
        Branch         = $Branch
        AgeMinutes     = $AgeMinutes
    }
}

# True when pid is running now AND started at the recorded time. The lookup is the caller's.
function Test-PaperRunLockProcessIs([int] $ProcessId, [long] $StartTicks, [scriptblock] $StartTimeOf) {
    $current = $null
    try { $current = & $StartTimeOf $ProcessId } catch { $current = $null }
    if ($null -eq $current) { return $false }
    $ticks = [long] 0
    if (-not [long]::TryParse([string] $current, [ref] $ticks)) { return $false }
    return ($ticks -eq $StartTicks)
}

# The decision. Held = someone is really in there and the reader must stand down. Stale = there is a file
# but it holds nobody, so the reader deletes it and carries on. Neither = there was no claim to begin with
# ($null text: no file).
#   -Text         the claim file's content; $null when there is no file
#   -Unreadable   the file is there but could not be read (held open, no access)
#   -Now          the time to measure the claim's age against
#   -StartTimeOf  { param($ProcessId) ... } -> the start time of that pid now, as UTC ticks, or $null when
#                 no such process is running
function Get-PaperRunLockVerdict {
    param(
        $Text,
        [switch] $Unreadable,
        [Parameter(Mandatory = $true)][datetime] $Now,
        [Parameter(Mandatory = $true)][scriptblock] $StartTimeOf
    )
    if ($Unreadable) {
        return (New-PaperRunLockVerdict $false $true 'the claim file is there but cannot be read' 0 '' $null '' $null 0 $false)
    }
    if ($null -eq $Text) {
        return (New-PaperRunLockVerdict $false $false 'no claim' 0 '' $null '' $null)
    }
    if ([string]::IsNullOrWhiteSpace([string] $Text)) {
        return (New-PaperRunLockVerdict $false $true 'the claim file is empty' 0 '' $null '' $null)
    }

    $claim = ConvertFrom-PaperClaimText $Text
    if ($null -eq $claim) {
        return (New-PaperRunLockVerdict $false $true 'the claim cannot be parsed' 0 '' $null '' $null)
    }

    $verb = [string] (Get-PaperClaimField $claim 'verb')
    $branch = [string] (Get-PaperClaimField $claim 'branch')

    $processId = [int] (Get-PaperClaimNumber $claim 'pid')
    if ($processId -le 0) {
        return (New-PaperRunLockVerdict $false $true 'the claim names no process' 0 $verb $null $branch $null)
    }

    # An unreadable start time means an unknowable age, and a claim whose age cannot be checked must not be
    # able to block anyone for ever.
    $startedAt = [datetime]::MinValue
    $startedText = [string] (Get-PaperClaimField $claim 'started')
    $styles = [System.Globalization.DateTimeStyles]::RoundtripKind -bor [System.Globalization.DateTimeStyles]::AllowWhiteSpaces
    if (-not [datetime]::TryParse($startedText, [System.Globalization.CultureInfo]::InvariantCulture, $styles, [ref] $startedAt)) {
        return (New-PaperRunLockVerdict $false $true 'the claim does not say when it started' $processId $verb $null $branch $null)
    }
    $age = ($Now - $startedAt).TotalMinutes
    if ($age -gt $script:PaperRunLockMaxMinutes) {
        return (New-PaperRunLockVerdict $false $true "the claim is $([int] $age) minutes old" $processId $verb $startedAt $branch $age)
    }

    # Without the start time of its process a claim cannot tell its own process from a later one that was
    # given the same pid.
    $processStart = Get-PaperClaimNumber $claim 'pidStart'
    if ($processStart -le 0) {
        return (New-PaperRunLockVerdict $false $true 'the claim does not say when its process started' $processId $verb $startedAt $branch $age)
    }

    $childId = [int] (Get-PaperClaimNumber $claim 'child')
    $childStart = Get-PaperClaimNumber $claim 'childStart'

    if (Test-PaperRunLockProcessIs $processId $processStart $StartTimeOf) {
        return (New-PaperRunLockVerdict $true $false 'another run of this project is in progress' $processId $verb $startedAt $branch $age $childId)
    }
    if ($childId -gt 0 -and $childStart -gt 0 -and (Test-PaperRunLockProcessIs $childId $childStart $StartTimeOf)) {
        return (New-PaperRunLockVerdict $true $false 'the run that made the claim is gone, but the command it started is still running' $processId $verb $startedAt $branch $age $childId)
    }
    return (New-PaperRunLockVerdict $false $true 'no process of the claim is alive with the start time it recorded' $processId $verb $startedAt $branch $age $childId)
}

# "Is this claim mine": the decision a holder makes before it rewrites or deletes the file. Mine = my pid
# AND my start time; anything that cannot be parsed is not mine, and is left to the stale rule.
function Test-PaperRunLockOwned {
    param(
        $Text,
        [int] $ProcessId,
        [long] $StartTime
    )
    $claim = ConvertFrom-PaperClaimText $Text
    if ($null -eq $claim -or $StartTime -le 0) { return $false }
    return ((Get-PaperClaimNumber $claim 'pid') -eq $ProcessId -and (Get-PaperClaimNumber $claim 'pidStart') -eq $StartTime)
}

# Every attempt to take the claim failed. $SawFile false: CreateNew failed with no file there to read - the
# folder refused, not another run - so $null, and the caller runs without a claim as a kit without this file
# would. $SawFile true: the file was there each time and was never cleared as nobody's, so someone is taking
# or rewriting it right now. That is a live claim, even if it cannot yet say whose.
function Get-PaperRunLockContendedVerdict([bool] $SawFile) {
    if (-not $SawFile) { return $null }
    return (New-PaperRunLockVerdict $true $false 'another run is taking the claim at this moment' 0 '' $null '' $null)
}

# ---- the shell -----------------------------------------------------------------------------------------

# The start time of a running process as UTC ticks, or $null when there is no such process (or it cannot be
# asked, which for a claim is the same answer).
function Get-PaperProcessStartTicks([int] $ProcessId) {
    if ($ProcessId -le 0) { return $null }
    try {
        $p = Get-Process -Id $ProcessId -ErrorAction Stop
        if ($p.HasExited) { return $null }
        return [long] $p.StartTime.ToUniversalTime().Ticks
    }
    catch { return $null }
}

function Get-PaperRunLockNowVerdict($Text, [bool] $Unreadable) {
    return (Get-PaperRunLockVerdict -Text $Text -Unreadable:$Unreadable -Now (Get-Date) -StartTimeOf { param($ProcessId) Get-PaperProcessStartTicks $ProcessId })
}

# The file's text, or $null when there is no file; Unreadable when it is there but could not be read.
function Read-PaperRunLockFile([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return [pscustomobject]@{ Text = $null; Unreadable = $false } }
    try { return [pscustomobject]@{ Text = [System.IO.File]::ReadAllText($Path); Unreadable = $false } }
    catch {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return [pscustomobject]@{ Text = $null; Unreadable = $false } }
        return [pscustomobject]@{ Text = $null; Unreadable = $true }
    }
}

# Reads a claim through a stream this process already holds, leaving the stream open.
function Read-PaperRunLockStream([System.IO.FileStream] $Stream) {
    $reader = New-Object System.IO.StreamReader($Stream, [System.Text.Encoding]::UTF8, $true, 4096, $true)
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

# Writes a whole claim into a stream this process holds alone.
function Write-PaperRunLockStream([System.IO.FileStream] $Stream, $Claim) {
    $bytes = (New-Object System.Text.UTF8Encoding $false).GetBytes(($Claim | ConvertTo-Json -Compress))
    $Stream.Position = 0
    $Stream.SetLength(0)
    $Stream.Write($bytes, 0, $bytes.Length)
    $Stream.Flush()
}

# Deletes the claim file only if it still holds the text that was judged stale: between reading it and
# deleting it, somebody else may have cleared it and taken a fresh claim. The file is opened sharing only
# Delete, so nobody can write it between the check and the delete.
function Remove-PaperRunLockIfUnchanged([string] $Path, $JudgedText) {
    $fs = $null
    try {
        $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Delete)
        $current = Read-PaperRunLockStream $fs
        if ([string] $current -eq [string] $JudgedText) { [System.IO.File]::Delete($Path) }
    }
    catch { }
    finally { if ($null -ne $fs) { $fs.Dispose() } }
}

# The branch, read out of the repository's own files rather than by starting git: this runs inside a hook,
# where a process costs more than the answer is worth. A worktree keeps .git as a file pointing elsewhere.
function Get-PaperRunLockBranch([string] $RepoRoot) {
    try {
        $git = Join-Path $RepoRoot '.git'
        if (Test-Path -LiteralPath $git -PathType Leaf) {
            $pointer = ([System.IO.File]::ReadAllText($git)).Trim()
            if ($pointer -notmatch '(?im)^gitdir:\s*(.+)$') { return '' }
            $dir = $Matches[1].Trim()
            if (-not [System.IO.Path]::IsPathRooted($dir)) { $dir = Join-Path $RepoRoot $dir }
            $git = $dir
        }
        $head = Join-Path $git 'HEAD'
        if (-not (Test-Path -LiteralPath $head -PathType Leaf)) { return '' }
        $text = ([System.IO.File]::ReadAllText($head)).Trim()
        if ($text -match '(?im)^ref:\s*refs/heads/(.+)$') { return $Matches[1].Trim() }
        if ($text.Length -gt 12) { return $text.Substring(0, 12) }   # detached: the commit it sits on
        return $text
    }
    catch { return '' }
}

# The claim on this project that is really being kept right now, or $null. A claim that holds nobody is
# deleted here, so the next reader does not have to think about it again.
function Get-PaperHeldRunLock([string] $RepoRoot) {
    try {
        $path = Get-PaperRunLockPath $RepoRoot
        $file = Read-PaperRunLockFile $path
        $verdict = Get-PaperRunLockNowVerdict $file.Text $file.Unreadable
        if ($verdict.Held) { return $verdict }
        if ($verdict.Remove) { Remove-PaperRunLockIfUnchanged $path $file.Text }
        return $null
    }
    catch { return $null }
}

# Takes the claim for this process. Returns Path (to give back; $null when nothing was taken) and Holder
# (the verdict naming whoever holds it; $null when nobody does). Both $null: the claim could not be written
# at all - no folder, no access - and the caller runs without one, as a kit without this file would.
function Enter-PaperRunLock([string] $RepoRoot, [string] $Verb) {
    $none = [pscustomobject]@{ Path = $null; Holder = $null }
    try {
        $path = Get-PaperRunLockPath $RepoRoot
        $dir = Split-Path -Parent $path
        if (-not (Test-Path -LiteralPath $dir -PathType Container)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
        $claim = [ordered]@{
            pid      = $PID
            pidStart = [string] (Get-PaperProcessStartTicks $PID)
            verb     = [string] $Verb
            started  = (Get-Date).ToString('o')
            branch   = (Get-PaperRunLockBranch $RepoRoot)
            root     = [string] $RepoRoot
        }
    }
    catch { return $none }

    $sawFile = $false
    for ($attempt = 0; $attempt -lt $script:PaperRunLockAttempts; $attempt++) {
        if ($attempt -gt 0) { Start-Sleep -Milliseconds (50 * $attempt) }
        $fs = $null
        try {
            # CreateNew is the whole lock: it fails when the file exists, and only one creator wins. Shared
            # with nobody while it is written, so no reader sees half a claim.
            $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            Write-PaperRunLockStream $fs $claim
            return [pscustomobject]@{ Path = $path; Holder = $null }
        }
        catch [System.IO.IOException] {
            # The file is there (or was, a moment ago): read it to learn whether anyone is in it.
            $file = Read-PaperRunLockFile $path
            if ($null -ne $file.Text -or $file.Unreadable) { $sawFile = $true }
            $verdict = Get-PaperRunLockNowVerdict $file.Text $file.Unreadable
            if ($verdict.Held) { return [pscustomobject]@{ Path = $null; Holder = $verdict } }
            if ($verdict.Remove) { Remove-PaperRunLockIfUnchanged $path $file.Text }
        }
        catch { return $none }   # no access to the folder: nothing to wait on
        finally { if ($null -ne $fs) { $fs.Dispose() } }
    }

    return [pscustomobject]@{ Path = $null; Holder = (Get-PaperRunLockContendedVerdict $sawFile) }
}

# Records the process that runs the command, once it has started: if this process dies, the claim stays
# held for as long as that one runs. Only ever on our own claim.
function Set-PaperRunLockChild([string] $Path, [int] $ChildProcessId) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    $fs = $null
    try {
        $childStart = Get-PaperProcessStartTicks $ChildProcessId
        if ($null -eq $childStart) { return }
        $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $text = Read-PaperRunLockStream $fs
        if (-not (Test-PaperRunLockOwned -Text $text -ProcessId $PID -StartTime ([long] (Get-PaperProcessStartTicks $PID)))) { return }
        $claim = [ordered]@{}
        foreach ($prop in (ConvertFrom-PaperClaimText $text).PSObject.Properties) { $claim[$prop.Name] = $prop.Value }
        $claim['child'] = $ChildProcessId
        $claim['childStart'] = [string] $childStart
        Write-PaperRunLockStream $fs $claim
    }
    catch { }
    finally { if ($null -ne $fs) { $fs.Dispose() } }
}

# Gives the claim back, and only ever our own: between taking it and giving it back the file may have been
# cleaned up as stale and retaken by somebody else.
function Exit-PaperRunLock([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    $fs = $null
    try {
        $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Delete)
        $text = Read-PaperRunLockStream $fs
        if (Test-PaperRunLockOwned -Text $text -ProcessId $PID -StartTime ([long] (Get-PaperProcessStartTicks $PID))) {
            [System.IO.File]::Delete($Path)
        }
    }
    catch { }
    finally { if ($null -ne $fs) { $fs.Dispose() } }
}
