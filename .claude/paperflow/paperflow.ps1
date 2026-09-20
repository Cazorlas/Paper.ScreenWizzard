# The flow runner: the one entry point a skill calls, for every kind of project.
#
#   paperflow.ps1 <verb> [-Repo <project root>] [-Path <file>] [-- <extra args appended to the command>]
#
# It holds no knowledge of any host. It reads .claude/paper.profile.json, asks Get-PaperVerbPlan what to
# do, and either runs the project's own command or runs one of the kit's own verbs. Every decision that
# reads the profile lives in verb-plan.ps1, which is pure and fully tested; this file only reads files and
# starts processes. One route is taken here, before the profile is read: review-files, which never reads
# the profile (see below); its own decisions live in review-files-plan.ps1.
#
# Exit codes:
#   0  done
#   1  the command ran and failed
#   2  invalid request, broken profile, or the command could not be started
#   3  reserved: the project's command says the change needs the host restarted
#   4  reserved: built, but nothing verified it (a plan that is not approved uses this too); also: another
#      run of this project holds it, so no command was run
#   5  NOT APPLICABLE - this project has no such verb or lane. Never treat it as a failure.
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string] $Verb = 'help',
    [string] $Repo = (Get-Location).Path,
    [string] $Path,
    # tasks only, and only for a task file written before paper-kit 0.3.0: print the hash of its part 1
    # (the Spec) instead of judging the file. A plan needs none - its requirement lives in SPEC.md, and
    # what sits above its Tasks heading (Context, Decisions) grows while the work runs.
    [switch] $SpecHash,
    # api-check and review-files: the branch this task branched from. Default: the base worktree create recorded, then
    # main, master, origin/main, origin/master; none of them exits 2 (not verifiable).
    [string] $Base,
    [Parameter(ValueFromRemainingArguments = $true)][string[]] $Rest
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'verb-plan.ps1')

$ProfileRel = '.claude/paper.profile.json'

function Show-Help {
    Write-Output @'
paperflow - one entry point for every Paper project

  paperflow.ps1 <verb> [-Repo <project root>] [-Path <file>]

Verbs from the project profile: build test ui e2e live publish package watch
Verbs of the kit itself:        tasks (-Path <plan file>) status help
                                api-check (-Path <plan file> [-Base <branch>]): new code that uses a
                                host namespace needs a row in the plan's API table
                                review-files ([-Base <branch>]): the list of files every reviewer
                                reads, with the reason for each file left out; ends "total: N"

The project declares what each verb runs in .claude/paper.profile.json. A verb the profile does not
declare exits 5 = not applicable, which is a valid verdict and never a failure.

Exit: 0 done | 1 failed | 2 invalid request or broken profile | 3 host restart needed
      4 nothing verified it | 5 not applicable
'@
}

# ConvertFrom-Json returns PSCustomObject on PowerShell 5.1 (-AsHashtable arrived in 6.0), and the plan
# indexes its input like a map. Convert once, here, so the plan stays pure and testable with literals.
function ConvertTo-PaperMap($Value) {
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Collections.IDictionary]) { return $Value }
    if ($Value -isnot [psobject]) { return $Value }
    $map = @{}
    foreach ($prop in $Value.PSObject.Properties) {
        if ($prop.Name.StartsWith('$')) { continue }   # $comment and friends are documentation
        $map[$prop.Name] = ConvertTo-PaperMap $prop.Value
    }
    return $map
}

function Read-PaperProfile([string] $RepoRoot) {
    $file = Join-Path $RepoRoot ($ProfileRel -replace '/', '\')
    if (-not (Test-Path -LiteralPath $file)) { return $null }
    try {
        return ConvertTo-PaperMap (Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json)
    }
    catch {
        [Console]::Error.WriteLine("paperflow: $ProfileRel is not valid JSON - $($_.Exception.Message)")
        exit 2
    }
}

# Writes with [Console]::Out, not Write-Output: this is called as a statement whose caller then exits,
# and anything a PowerShell function writes to the success stream becomes part of its RETURN VALUE. The
# first version used Write-Output inside `exit (Show-Status ...)` and printed nothing at all - the lines
# were collected into the value handed to exit.
function Show-Status($ProjectProfile, [string] $RepoRoot) {
    $out = { param($line) [Console]::Out.WriteLine($line) }
    & $out "repo   $RepoRoot"
    if ($null -eq $ProjectProfile) {
        & $out "profile  MISSING ($ProfileRel) - no verb of a project can run here"
        return
    }
    & $out ("hosts  " + (@($ProjectProfile['hosts']) -join ', '))
    & $out ("lanes  " + (@($ProjectProfile['lanes']) -join ', '))
    & $out ''
    & $out 'verb      state'
    foreach ($v in @($script:PaperVerbLanes.Keys)) {
        $plan = Get-PaperVerbPlan -ProjectProfile $ProjectProfile -Verb $v
        $state = switch ($plan.ExitCode) {
            0 { $plan.Command }
            5 { 'not applicable' }
            default { "INVALID - $($plan.Reason)" }
        }
        & $out ("{0,-9} {1}" -f $v, $state)
    }
}

if ($Verb -in @('help', '-h', '--help', '/?')) { Show-Help; exit 0 }

if (-not (Test-Path -LiteralPath $Repo -PathType Container)) {
    [Console]::Error.WriteLine("paperflow: project folder not found: $Repo")
    exit 2
}
$repoRoot = (Resolve-Path -LiteralPath $Repo).ProviderPath.TrimEnd('\')

# review-files reads git and, when the machine has it, ocr - never the profile - so it answers before the
# profile is read: a broken profile does not stop the list every reviewer needs. Whatever goes wrong is
# exit 2 with the reason, never 1 (1 would read as "the review failed").
if ($Verb -eq 'review-files') {
    $review = Join-Path $PSScriptRoot 'review-files.ps1'
    if (-not (Test-Path -LiteralPath $review)) {
        [Console]::Error.WriteLine('paperflow: review-files not installed with this kit')
        exit 2
    }
    try {
        . $review
        $code = Invoke-PaperReviewFiles -RepoRoot $repoRoot -Base $Base
    }
    catch {
        [Console]::Error.WriteLine("review-files: not verifiable: $($_.Exception.Message)")
        $code = 2
    }
    exit $code
}

$projectProfile = Read-PaperProfile $repoRoot

$plan = Get-PaperVerbPlan -ProjectProfile $projectProfile -Verb $Verb
if ($plan.ExitCode -ne 0) {
    # 5 goes to stdout: it is a verdict the caller is expected to read and record, not an error.
    if ($plan.ExitCode -eq 5) { Write-Output "paperflow: $($plan.Reason)" }
    else { [Console]::Error.WriteLine("paperflow: $($plan.Reason)") }
    exit $plan.ExitCode
}

if ($plan.Internal) {
    switch ($Verb) {
        'status' { Show-Status $projectProfile $repoRoot; exit 0 }
        'tasks' {
            $gate = Join-Path $PSScriptRoot 'tasks-gate.ps1'
            if (-not (Test-Path -LiteralPath $gate)) {
                [Console]::Error.WriteLine('paperflow: tasks gate not installed with this kit')
                exit 2
            }
            . $gate
            if (-not $Path) {
                [Console]::Error.WriteLine('paperflow: tasks needs -Path <plan file> (docs/features/<slug>/YYYY-MM-DD-<task>-plan.md)')
                exit 2
            }
            if ($SpecHash) {
                if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
                    [Console]::Error.WriteLine("paperflow: no such file: $Path")
                    exit 2
                }
                # -Encoding UTF8: 5.1's Get-Content defaults to the ANSI codepage, and a Spec written in
                # Vietnamese would hash differently every time the codepage changed.
                $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8)
                [Console]::Out.WriteLine((Get-PaperSpecHash -Lines $lines))
                exit 0
            }
            exit (Invoke-PaperTasksGate -Path $Path -ProjectProfile $projectProfile)
        }
        'api-check' {
            $check = Join-Path $PSScriptRoot 'api-check.ps1'
            if (-not (Test-Path -LiteralPath $check)) {
                [Console]::Error.WriteLine('paperflow: api-check not installed with this kit')
                exit 2
            }
            if (-not $Path) {
                [Console]::Error.WriteLine('paperflow: api-check needs -Path <plan file> (docs/features/<slug>/YYYY-MM-DD-<task>-plan.md)')
                exit 2
            }
            . $check
            exit (Invoke-PaperApiCheck -Path $Path -RepoRoot $repoRoot -ProjectProfile $projectProfile -Base $Base)
        }
    }
}

$command = $plan.Command
if ($Rest) { $command = $command + ' ' + ($Rest -join ' ') }

# A verb that can run for minutes takes a claim on this project while it runs. Whoever finds the claim
# taken - another agent, or the check at the end of a turn - runs NOTHING and exits 4: a second build of
# the same project dies on the output files the first one holds, and that failure reads exactly like red
# code. 4 is "nothing verified it", which the test loop already knows to run again once. The rules live in
# run-lock.ps1; a kit installed before that file existed simply runs with no claim.
$lockPath = $null
$lockScript = Join-Path $PSScriptRoot 'run-lock.ps1'
if (Test-Path -LiteralPath $lockScript -PathType Leaf) {
    . $lockScript
    if ($script:PaperRunLockVerbs -contains $Verb) {
        $claim = Enter-PaperRunLock -RepoRoot $repoRoot -Verb $Verb
        if ($null -ne $claim.Holder) {
            $h = $claim.Holder
            $who = if ($h.ProcessId -gt 0) { "pid $($h.ProcessId)" } else { 'pid unknown' }
            if ($h.ChildProcessId -gt 0) { $who += ", command pid $($h.ChildProcessId)" }
            $what = if ($h.Verb) { "verb $($h.Verb)" } else { 'verb unknown' }
            $since = if ($h.StartedAt) { 'since ' + ([datetime] $h.StartedAt).ToString('yyyy-MM-dd HH:mm:ss') } else { 'since an unknown time' }
            Write-Output "paperflow: not verified: another run holds $repoRoot ($who, $what, $since) - $($h.Reason). No command was run; let that run finish, then run $Verb again."
            exit 4
        }
        $lockPath = $claim.Path
    }
}

Write-Output "paperflow: $Verb -> $command"

# The command is the project's own line, run from the project root through project-command.ps1, the one way
# the kit runs a project's line (it keeps double quotes and long lines whole). Its output is shown as it
# comes; its exit code is passed through untouched: 3 and 4 mean something to the caller and must not be
# flattened into 1. The command's own process goes into the claim as soon as it starts, so killing this
# runner does not free the project while the build it started is still running.
try {
    . (Join-Path $PSScriptRoot 'project-command.ps1')
    $onStarted = $null
    if ($lockPath) { $onStarted = { param($ChildId) Set-PaperRunLockChild -Path $lockPath -ChildProcessId $ChildId } }
    $run = Invoke-PaperProjectCommand -Directory $repoRoot -Command $command -Echo -OnStarted $onStarted
    $code = $run.Code
}
catch {
    [Console]::Error.WriteLine("paperflow: could not start the command for verb '$Verb' - $($_.Exception.Message)")
    $code = 2
}
finally {
    # In finally: a verb that failed, or threw, leaves nothing behind for the next run to wait on.
    if ($lockPath) { Exit-PaperRunLock $lockPath }
}

exit $code
