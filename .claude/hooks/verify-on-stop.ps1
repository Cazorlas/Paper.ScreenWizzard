# Stop: the claim "done" is made at Stop, so that is where build runs.
#
# Not PostToolUse: a build and a suite cost minutes, and running them per edit would stall every change.
# When a watched file was written this session, this runs the project's `build` verb, then `test` only
# when verify.onStopTests is true -
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
# What a finished run means is decided by Get-PaperStopVerdict in paperflow/verb-plan.ps1 (pure, tested in
# tests/stop-verdict.tests.ps1); this file reads the profile and the baseline, runs the verbs and writes.
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
        foreach ($verb in (Get-PaperStopVerbs -Profile $profileMap)) {
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

            # What the run means - held build output, red build, F5, new versus known failures, the prune
            # notice - is Get-PaperStopVerdict in verb-plan.ps1, pure and tested. Here: the reads it needs.
            $failed = @()
            $baseline = @()
            $baselineRel = ''
            $template = ''
            if ($verb -eq 'test') {
                $failed = @(Get-PaperFailedTestNames $run.Lines)
                # A {config} baseline is one file per build configuration; which one is the configuration the
                # verbs build, named by verify.configuration, else PAPERFLOW_CONFIGURATION.
                $configuration = [string] (Get-PaperProfileValue $profileMap @('verify', 'configuration'))
                if ([string]::IsNullOrWhiteSpace($configuration)) { $configuration = [string] $env:PAPERFLOW_CONFIGURATION }
                $baselineRel = [string] (Get-PaperKnownFailuresPath -ProjectProfile $profileMap -Configuration $configuration)
                $template = [string] (Get-PaperProfileValue $profileMap @('knownFailures'))
                if ($baselineRel) { $baseline = @(Read-PaperKnownFailures (Join-Path $root ($baselineRel.Replace('/', '\')))) }
            }
            $stop = Get-PaperStopVerdict -Verb $verb -ExitCode $run.Code -Lines $run.Lines -Command $plan.Command -Failed $failed -Baseline $baseline -BaselinePath $baselineRel -KnownFailures $template
            if ($stop.Message) { Write-PaperHookText -ToError "verify-on-stop: $($stop.Message)" }
            if ($stop.ExitCode -ne 0) { exit $stop.ExitCode }
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
