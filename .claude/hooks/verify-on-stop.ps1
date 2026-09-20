# Stop: the claim "done" is made at Stop, so that is where build and test run.
#
# Not PostToolUse: a build and a suite cost minutes, and running them per edit would stall every change.
# When a watched file was written this session, this runs the project's `build` and then `test` verb -
# the commands .claude/paper.profile.json declares, planned by paperflow/verb-plan.ps1 exactly as the flow
# runner plans them - from the project root:
#   - build exits non-zero                        -> exit 2 with its error lines
#   - test names failures outside knownFailures   -> exit 2 with those names
#   - test exits non-zero and names no failure    -> exit 2: a run that proved nothing is not a pass
#   - only known failures, or all green           -> exit 0, and this file state is remembered so the next
#                                                    stop with nothing new changed does not build again
#   - a verb the project does not declare         -> not applicable, skipped (never a failure)
#
# Watched extensions: verify.watch in the profile (default .cs .xaml .csproj .props .targets .ps1 .py .ts).
# knownFailures: a file, relative to the project root, one test name per line, # for comments. A {config}
# placeholder is resolved to verify.configuration, else PAPERFLOW_CONFIGURATION.
#
# stop_hook_active exits 0, so a test that stays red cannot loop the agent. PAPER_SKIP_VERIFY=1 turns it
# off. Unreadable input, no stamp, a broken profile, a missing verb-plan.ps1, any error: exit 0.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'

# Runs one project command exactly as paperflow.ps1 does - through paperflow/project-command.ps1, from the
# project root - collected rather than shown, within the time left. Code is $null when it ran out of time.
# Blank lines are dropped: the verdicts below read the lines that say something.
function Invoke-PaperVerbCommand([string] $Root, [string] $Command, [int] $TimeoutSeconds, [scriptblock] $OnStarted) {
    $run = Invoke-PaperProjectCommand -Directory $Root -Command $Command -TimeoutSeconds $TimeoutSeconds -OnStarted $OnStarted
    return [pscustomobject]@{ Code = $run.Code; Lines = @($run.Lines | Where-Object { $_ -ne '' }) }
}

# True when a failed build named errors and every one of them is a held output file (the compiler's own
# "cannot write", or MSBuild's copy failing), never when one real compile error is mixed in.
# MSBuild's closing "    2 Error(s)" count is not an error line: it names no file, and counting it made this
# answer False for every held-file failure (measured 2026-09-18, with the host holding a built DLL).
# The terminal logger (dotnet build -tl:on) closes each project with "<name> <tfm> failed with N error(s)"
# and the build with "Build failed with N error(s) ... in 1.0s" - summaries too. A built project's line
# ("<name> -> <path>", or "<name> <tfm> succeeded ... <arrow> <path>") is a success even when the project's
# name holds the word Error.
function Test-PaperLockOnlyBuildFailure([string[]] $Lines) {
    $errors = @($Lines | Where-Object {
            $_ -match '(?i)\berror\b' -and
            $_ -notmatch '(?i)^\s*\d+\s+Error\(s\)\s*$' -and
            $_ -notmatch '(?i)\bfailed with \d+ error\(s\)' -and
            $_ -notmatch '^\s*[\w.\-]+ -> \S' -and
            $_ -notmatch '(?i)^\s*[\w.\-]+(?:\s+[\w.\-]+)?\s+succeeded\b'
        })
    if ($errors.Count -eq 0) { return $false }
    foreach ($line in $errors) {
        if ($line -notmatch '(?i)CS2012|MSB3021|MSB3026|MSB3027|being used by another process|locked by') { return $false }
    }
    return $true
}

try {
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'session-state.ps1')

    if ($env:PAPER_SKIP_VERIFY -eq '1') { exit 0 }
    $payload = Read-PaperHookPayload
    if ($null -eq $payload -or $payload.stop_hook_active) { exit 0 }

    $session = [string] $payload.session_id
    $since = Get-PaperSessionStart $session -Create
    if ($null -eq $since) { exit 0 }

    $root = Get-PaperHookProjectDir $payload
    if (-not $root) { exit 0 }
    $profileMap = Read-PaperProfile $root
    if ($null -eq $profileMap) { exit 0 }

    $watch = ConvertTo-PaperExtensions (Get-PaperProfileValue $profileMap @('verify', 'watch')) @('.cs', '.xaml', '.csproj', '.props', '.targets', '.ps1', '.py', '.ts')
    $changed = @(Get-PaperChangedFiles $root $since $watch | Sort-Object FullName)
    if ($changed.Count -eq 0) { exit 0 }

    # Content-derived signature of what changed: a stop with nothing new since the last green run is free.
    $parts = ($changed | ForEach-Object { (Get-PaperRelative $root $_.FullName) + '|' + $_.Length + '|' + $_.LastWriteTimeUtc.Ticks }) -join "`n"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $signature = [BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($root + "`n" + $parts))).Replace('-', '')
    $cache = (Get-PaperSessionStampPath $session) -replace '\.stamp$', '.verified'
    if ((Test-Path -LiteralPath $cache -PathType Leaf) -and ([System.IO.File]::ReadAllText($cache).Trim() -eq $signature)) { exit 0 }

    $planner = Join-Path $PSScriptRoot '..\paperflow\verb-plan.ps1'
    if (-not (Test-Path -LiteralPath $planner -PathType Leaf)) {
        Write-PaperHookText -ToError 'verify-on-stop: paperflow/verb-plan.ps1 is not installed; nothing verified (rerun /paper-kit:setup).'
        exit 0
    }
    . $planner
    $commandScript = Join-Path $PSScriptRoot '..\paperflow\project-command.ps1'
    if (-not (Test-Path -LiteralPath $commandScript -PathType Leaf)) {
        Write-PaperHookText -ToError 'verify-on-stop: paperflow/project-command.ps1 is not installed; nothing verified (rerun /paper-kit:setup).'
        exit 0
    }
    . $commandScript

    # Never build on top of a build, and never let one start on top of ours. This hook takes the project's
    # run claim for as long as it builds and tests, exactly as paperflow does for its verbs. While another
    # process holds it, a second build dies on the output files the first one still holds, and that failure
    # reads exactly like red code - the most expensive false alarm this hook can raise. Exit 2, not 0: 0
    # means verified, and nothing here was. The next stop carries stop_hook_active, so this cannot loop.
    $lockPath = $null
    $lockScript = Join-Path $PSScriptRoot '..\paperflow\run-lock.ps1'
    if (Test-Path -LiteralPath $lockScript -PathType Leaf) {
        . $lockScript
        $claim = Enter-PaperRunLock -RepoRoot $root -Verb 'build+test (stop check)'
        if ($null -ne $claim.Holder) {
            $held = $claim.Holder
            $who = if ($held.ProcessId -gt 0) { "pid $($held.ProcessId)" } else { 'pid unknown' }
            $what = if ($held.Verb) { $held.Verb } else { 'unknown' }
            $when = if ($held.StartedAt) { ([datetime] $held.StartedAt).ToString('yyyy-MM-dd HH:mm:ss') } else { 'an unknown time' }
            Write-PaperHookText -ToError ("verify-on-stop: NOT VERIFIED - another run of this project is in progress ($who, verb $what, started $when).`n`nNo command was run: building over it fails on the output files it holds, and that failure looks like red code. Let that run finish, then stop again.")
            exit 2
        }
        $lockPath = $claim.Path
    }
    $onStarted = $null
    if ($lockPath) { $onStarted = { param($ChildId) Set-PaperRunLockChild -Path $lockPath -ChildProcessId $ChildId } }

    try {
        $deadline = (Get-Date).AddSeconds(570)
        $newFailures = @()
        $knownRed = @()
        foreach ($verb in @('build', 'test')) {
            $plan = Get-PaperVerbPlan -ProjectProfile $profileMap -Verb $verb
            if ($plan.ExitCode -eq 5 -or $plan.Internal) { continue }
            if ($plan.ExitCode -ne 0) {
                Write-PaperHookText -ToError "verify-on-stop: $verb not verified - $($plan.Reason)"
                exit 0
            }

            $left = [int] ($deadline - (Get-Date)).TotalSeconds
            if ($left -le 5) { Write-PaperHookText -ToError "verify-on-stop: no time left to run $verb; nothing verified."; exit 0 }
            $run = Invoke-PaperVerbCommand $root $plan.Command $left $onStarted
            if ($null -eq $run.Code) { Write-PaperHookText -ToError "verify-on-stop: $verb did not finish in time; nothing verified."; exit 0 }

            if ($verb -eq 'build') {
                if ($run.Code -eq 0) { continue }
                $errors = @($run.Lines | Where-Object { $_ -match '(?i)\berror\b' } | Select-Object -First 12)
                if ($errors.Count -eq 0) { $errors = @($run.Lines | Select-Object -Last 12) }

                # A build whose every error is a held output file says nothing about the code: a test run, a
                # harness or an IDE still owns the file. Reported as "does not build" it sends the agent
                # hunting for a compile error that is not there - measured in a real project as 7 of 17
                # blocking stops. Still exit 2: nothing was verified either way.
                if (Test-PaperLockOnlyBuildFailure $run.Lines) {
                    Write-PaperHookText -ToError ("verify-on-stop: NOT VERIFIED - another process still holds the build output; this is not a compile error.`n`n  " + (@($errors | Select-Object -First 3) -join "`n  ") + "`n`nStop whatever holds them (a UI test run, a harness, a test host, an IDE build), then let this run again.")
                    exit 2
                }
                Write-PaperHookText -ToError ("verify-on-stop: the build verb failed (exit $($run.Code)) - this work does not build yet.`n`n  " + ($errors -join "`n  ") + "`n`n  command: $($plan.Command)`nFix the build before reporting this work as done.")
                exit 2
            }

            # F5: the same verdict the flow runner and the worktree baseline read. A run that executed no test
            # proved nothing, whatever it exited, and is never remembered as verified.
            $runVerdict = Get-PaperTestRunVerdict -ExitCode $run.Code -Output $run.Lines
            if ($runVerdict.ExitCode -eq 4) {
                Write-PaperHookText -ToError ("verify-on-stop: NOT VERIFIED (verdict: not verifiable) - $($runVerdict.Reason).`n`n  " + (@($run.Lines | Select-Object -Last 10) -join "`n  ") + "`n`n  command: $($plan.Command)")
                exit 2
            }
            $failed = Get-PaperFailedTestNames $run.Lines
            if ($run.Code -ne 0 -and $failed.Count -eq 0) {
                Write-PaperHookText -ToError ("verify-on-stop: NOT VERIFIED - the test verb exited $($run.Code) without naming a failing test.`n`n  " + (@($run.Lines | Select-Object -Last 10) -join "`n  ") + "`n`n  command: $($plan.Command)")
                exit 2
            }
            # A {config} baseline is one file per build configuration; which one is the configuration the verbs
            # build, named by verify.configuration, else PAPERFLOW_CONFIGURATION. Reading the template as a file
            # name found no baseline, so every known failure blocked the stop.
            $configuration = [string] (Get-PaperProfileValue $profileMap @('verify', 'configuration'))
            if ([string]::IsNullOrWhiteSpace($configuration)) { $configuration = [string] $env:PAPERFLOW_CONFIGURATION }
            $baselineRel = Get-PaperKnownFailuresPath -ProjectProfile $profileMap -Configuration $configuration
            $template = [string] (Get-PaperProfileValue $profileMap @('knownFailures'))
            $baseline = @()
            if ($baselineRel) { $baseline = @(Read-PaperKnownFailures (Join-Path $root ($baselineRel.Replace('/', '\')))) }
            $newFailures = @($failed | Where-Object { $baseline -notcontains $_ })
            $knownRed = @($failed | Where-Object { $baseline -contains $_ })
            if ($newFailures.Count -gt 0) {
                $baselineNote = if ($baselineRel) { "not in $baselineRel" }
                    elseif ($template) { 'no baseline read: knownFailures is per configuration and neither verify.configuration nor PAPERFLOW_CONFIGURATION names one' }
                    else { 'the profile declares no knownFailures' }
                Write-PaperHookText -ToError ("verify-on-stop: $($newFailures.Count) test(s) red ($baselineNote):`n`n  " + ($newFailures -join "`n  ") + "`n`n  command: $($plan.Command)`nFix them, or show they were red before this session, before reporting this work as done.")
                exit 2
            }
            $fixed = @($baseline | Where-Object { $failed -notcontains $_ })
            if ($run.Code -eq 0 -and $fixed.Count -gt 0 -and $fixed.Count -lt 50) {
                Write-PaperHookText -ToError ("verify-on-stop: on the known-failure list and now passing - prune ${baselineRel}:`n  " + ($fixed -join "`n  "))
            }
        }

        try { [System.IO.File]::WriteAllText($cache, $signature) } catch { }
        exit 0
    }
    finally {
        # In finally: a red, a timeout or a throw gives the claim back just the same.
        if ($lockPath) { Exit-PaperRunLock $lockPath }
    }
}
catch {
    exit 0
}
