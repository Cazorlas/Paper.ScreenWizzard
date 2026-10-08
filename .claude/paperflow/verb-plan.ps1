# The decision half of the flow runner, with no file system and no process: given a project's profile and
# the verb a skill asked for, what should happen.
#
# The seam this file exists for: a skill names a VERB, the project's paper.profile.json names the COMMAND.
# Nothing in a skill may name a script, a configuration or a path, or the skill stops working in the next
# kind of project. Dot-source it; tests/profile.tests.ps1 covers every branch.
#
# Exit codes it plans:
#   0  run Command  (or, when Internal, the kit's own code)
#   2  the request or the profile is wrong - a typo, an unknown host, a lane no host can prove, a blank command
#   5  NOT APPLICABLE - this project does not have that verb or that lane
#
# Why 5 and not 1: a web repo has no `live` lane and a Revit repo has no `e2e` lane. If a missing verb
# counted as a failure, every project would be forced to declare a fake command to keep the gate green,
# and the gate would stop meaning anything.

# Verb -> the lane it proves. $null means the verb is not tied to a lane.
$script:PaperVerbLanes = [ordered]@{
    build   = $null
    test    = 'unit'
    ui      = 'ui'
    e2e     = 'e2e'
    live    = 'live'
    publish = $null
    package = $null
    watch   = $null
}

# Verbs the kit runs itself. They need no command and no profile, because they read markdown (and, for
# api-check and review-files, git - review-files also ocr when the machine has it) only - that is what lets
# the task gate work in a repository where no host is installed at all (spec rule 10). api-check without a
# profile has no host namespace to look for: not applicable.
$script:PaperInternalVerbs = @('tasks', 'status', 'api-check', 'review-files', 'brief')

# Hosts this version ships, and the lanes each one can actually prove. `unit` needs no host: a test that
# never starts the application runs anywhere. An unknown host fails loudly here rather than quietly doing
# nothing.
$script:PaperHostLanes = @{
    # model: building in the user's model (Revit) or drawing (AutoCAD), proved by reading it back - only a
    # host that holds a model can do that.
    revit   = @('live', 'model')
    cad     = @('live', 'model')     # AutoCAD; its WPF palettes prove ui through the desktop host
    web     = @('e2e')
    desktop = @('ui')
    ai      = @('e2e')
    video   = @('ui', 'e2e')
    cli     = @('e2e', 'live')  # a tooling repository such as this kit: e2e on a throwaway project, live on a real one
}

function Get-PaperVerbPlan {
    <#
    .SYNOPSIS
    Plans one verb call against one project profile.
    .PARAMETER ProjectProfile
    The parsed .claude/paper.profile.json, as a hashtable. $null or empty when the project has none.
    .PARAMETER Verb
    What the skill asked for.
    #>
    param(
        $ProjectProfile,
        [Parameter(Mandatory = $true)][string] $Verb
    )

    function New-Plan([int] $code, [string] $command, [string] $reason, [bool] $internal) {
        return [pscustomobject]@{ ExitCode = $code; Command = $command; Reason = $reason; Internal = $internal }
    }

    $verb = $Verb.Trim()

    # The kit's own verbs first: they must answer before any profile check, or the gate would need a
    # profile to tell you that you have no profile.
    if ($script:PaperInternalVerbs -contains $verb) {
        return New-Plan 0 '' "verb '$verb' is the kit's own" $true
    }

    if (-not $script:PaperVerbLanes.Contains($verb)) {
        $known = (@($script:PaperVerbLanes.Keys) + $script:PaperInternalVerbs) -join ', '
        return New-Plan 2 '' "unknown verb '$verb'. Verbs: $known" $false
    }

    $hosts = @()
    $lanes = @()
    $verbs = $null
    if ($null -ne $ProjectProfile) {
        $hosts = @($ProjectProfile['hosts'])  | Where-Object { $_ }
        $lanes = @($ProjectProfile['lanes'])  | Where-Object { $_ }
        $verbs = $ProjectProfile['verbs']
    }
    if ($hosts.Count -eq 0 -and $lanes.Count -eq 0 -and $null -eq $verbs) {
        return New-Plan 2 '' "no .claude/paper.profile.json in this project: write one, then run '$verb' again" $false
    }

    # A host the kit does not ship leaves the project half-configured: its skills were never vendored, so
    # refuse instead of running the part that happens to work.
    foreach ($h in $hosts) {
        if (-not $script:PaperHostLanes.ContainsKey([string] $h)) {
            $shipped = ($script:PaperHostLanes.Keys | Sort-Object) -join ', '
            return New-Plan 2 '' "unknown host '$h'. Hosts this kit ships: $shipped" $false
        }
    }

    # A lane nothing in this project can prove is a profile someone wrote by copying another project.
    $provable = @('unit')
    foreach ($h in $hosts) { $provable += $script:PaperHostLanes[[string] $h] }
    foreach ($lane in $lanes) {
        if ($provable -notcontains [string] $lane) {
            return New-Plan 2 '' "lane '$lane' cannot be proved by any declared host ($($hosts -join ', '))" $false
        }
    }

    # Lane before verb: when both are missing, the lane is the real reason - the project does not prove
    # things that way at all, so no command would have helped.
    $lane = $script:PaperVerbLanes[$verb]
    if ($lane -and ($lanes -notcontains [string] $lane)) {
        return New-Plan 5 '' "lane '$lane' is not configured for this project, so verb '$verb' is not applicable" $false
    }

    $command = $null
    if ($verbs) { $command = $verbs[$verb] }
    if ($null -eq $command) {
        return New-Plan 5 '' "verb '$verb' is not configured in .claude/paper.profile.json" $false
    }
    if ([string]::IsNullOrWhiteSpace([string] $command)) {
        return New-Plan 2 '' "verb '$verb' is declared with a blank command in .claude/paper.profile.json" $false
    }

    return New-Plan 0 ([string] $command) "verb '$verb' from the project profile" $false
}

function Get-PaperKnownFailuresPath {
    <#
    .SYNOPSIS
    The red-baseline file for one build configuration, relative to the project root, or $null.
    .DESCRIPTION
    A project that builds several configurations keeps one baseline per configuration, so the profile's
    knownFailures may carry a {config} placeholder (".claude/hooks/known-failures.{config}.txt"). With the
    placeholder and no configuration there is no file to name: $null, never a path with "{config}" in it.
    #>
    param($ProjectProfile, [string] $Configuration)

    if ($null -eq $ProjectProfile) { return $null }
    $template = [string] $ProjectProfile['knownFailures']
    if ([string]::IsNullOrWhiteSpace($template)) { return $null }
    if ($template.IndexOf('{config}', [System.StringComparison]::OrdinalIgnoreCase) -lt 0) { return $template }
    if ([string]::IsNullOrWhiteSpace($Configuration)) { return $null }
    return [regex]::Replace($template, '\{config\}', $Configuration.Trim(), [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
}

# The verbs whose whole output goes to a log file and whose summary goes to the session (ADR-0023). tasks,
# status, api-check, review-files and brief print as before: their output IS the result.
$script:PaperSummaryVerbs = @('build', 'test', 'ui', 'e2e', 'publish')

function Get-PaperVerbSummary {
    <#
    .SYNOPSIS
    What a long-running verb prints into the session: exit code, where the full log is, the test total, up to
    MaxErrors error or failure lines, and the last Tail lines. An output of 40 lines or fewer is printed whole.
    .DESCRIPTION
    An error line says error, fail/failed/failure or exception as a word, and is not a zero count ("0 Error(s)",
    "0 failed"). A line that states a test total (Get-PaperTestTotal) is always kept, so whoever reads the
    summary counts the tests the same way the verdict does.
    #>
    param(
        [string] $Verb,
        $Code,
        [AllowEmptyString()][string[]] $Lines = @(),
        [string] $LogPath,
        [int] $MaxErrors = 30,
        [int] $Tail = 10
    )
    $all = @($Lines)
    $out = New-Object System.Collections.Generic.List[string]
    $codeText = if ($null -eq $Code) { 'none (stopped)' } else { [string] $Code }
    $out.Add("paperflow: $Verb exit $codeText - $($all.Count) lines, full log: $LogPath")
    if ($all.Count -le 40) {
        foreach ($l in $all) { $out.Add($l) }
        return $out.ToArray()
    }

    $totals = @($all | Where-Object { $null -ne (Get-PaperTestTotal $_) })
    $errors = @($all | Where-Object {
            $_ -match '(?i)\b(errors?|fail|failed|failures?|exception)\b' -and
            $_ -notmatch '(?i)\b0\s+(errors?|failed|failures?)\b' -and
            $totals -notcontains $_
        })
    if ($totals.Count -gt 0) {
        $out.Add('-- test total')
        foreach ($l in $totals) { $out.Add($l) }
    }
    if ($errors.Count -gt 0) {
        $shown = [Math]::Min($MaxErrors, $errors.Count)
        $out.Add("-- $shown of $($errors.Count) error lines")
        foreach ($l in @($errors | Select-Object -First $MaxErrors)) { $out.Add($l) }
    }
    $out.Add("-- last $Tail lines")
    foreach ($l in @($all | Select-Object -Last $Tail)) { $out.Add($l) }
    return $out.ToArray()
}

function Get-PaperTestTotal {
    <#
    .SYNOPSIS
    How many tests one line of a runner's output says ran, or $null when the line states no total.
    .DESCRIPTION
    The one parser of "how many tests ran": the verdict below and the worktree baseline's count line both
    read it, so a runner the verdict counts is never a runner the baseline shows and then calls unverifiable.
      "total: N"              Microsoft.Testing.Platform
      "Total tests: N"        dotnet test / VSTest ("Total: N" on its one-line summary)
      "Tests run: N"          JUnit, Maven, NUnit console
      "Tests: ... N total"    Jest, and a bare "N total" - but never Jest's "Test Suites:" or "Snapshots:"
                              line, which count files and snapshots, not tests (Snapshots comes after Tests,
                              so reading it made every Jest run "total 0")
      "N passed, M failed"    pytest: passed + failed + errors ran; skipped, deselected and xfailed did not.
                              A pytest run where every test failed prints no "passed" at all, and still ran.
    MSBuild's "0 Error(s)" is not pytest's "1 error": a count followed by "(" is never read.
    A line that states an explicit total is read as that total and nothing else: the kit's own runner prints
    "2 run, 0 failed; total: 2", and "0 failed" is not the count.
    #>
    param([AllowEmptyString()][string] $Line)

    if ([string]::IsNullOrWhiteSpace($Line)) { return $null }
    $m = [regex]::Match($Line, '(?i)\b(?:total(?:\s+tests)?|tests\s+run)\s*:\s*(\d+)')
    if ($m.Success) { return [int] $m.Groups[1].Value }
    if ($Line -notmatch '(?i)^\s*(?:test\s+suites|snapshots)\s*:') {
        $m = [regex]::Match($Line, '(?i)\b(\d+)\s+total\b')
        if ($m.Success) { return [int] $m.Groups[1].Value }
    }
    $ran = $null
    foreach ($m in [regex]::Matches($Line, '(?i)\b(\d+)\s+(?:passed|failed|errors?)\b(?!\()')) { $ran = [int] $ran + [int] $m.Groups[1].Value }
    return $ran
}

function Get-PaperTestRunVerdict {
    <#
    .SYNOPSIS
    The verdict for one test run, from its exit code and what it printed (F5).
    .DESCRIPTION
    A runner that ran nothing has proved nothing: a filter that matched no test, an assembly that was not
    built, a host that was not up all exit 0 on some runners. So the total is read from the output, line by
    line through Get-PaperTestTotal - the last line that states one wins - and:
      total > 0, exit 0              -> 0 pass
      total > 0, exit non-zero       -> 1 fail
      total 0 or no total, exit 0    -> 4 not verifiable
      total 0, exit non-zero         -> 4 not verifiable (Microsoft.Testing.Platform exits 8 for zero tests)
      no total, exit non-zero        -> 1 fail (the run broke before counting - a build error, a crash)
    Line is the line the total was read from ('' when none), what the worktree baseline prints as its count.
    #>
    param([int] $ExitCode, $Output)

    $total = $null
    $line = ''
    foreach ($text in @($Output)) {
        foreach ($one in ([string] $text -split "`r?`n")) {
            $n = Get-PaperTestTotal $one
            if ($null -ne $n) { $total = $n; $line = $one.Trim() }
        }
    }

    function New-Verdict([int] $code, [string] $verdict, [string] $reason) {
        return [pscustomobject]@{ ExitCode = $code; Verdict = $verdict; Total = $total; Line = $line; Reason = $reason }
    }
    if ($null -ne $total -and $total -gt 0) {
        if ($ExitCode -eq 0) { return New-Verdict 0 'pass' "$total test(s) ran and passed" }
        return New-Verdict 1 'fail' "$total test(s) ran, exit $ExitCode"
    }
    if ($ExitCode -ne 0 -and $null -eq $total) {
        return New-Verdict 1 'fail' "the run exited $ExitCode before reporting a test total"
    }
    $said = 'no test total in the output'
    if ($null -ne $total) { $said = 'total: 0' }
    return New-Verdict 4 'not verifiable' "$said (exit $ExitCode): zero tests ran, which proves nothing - run once more, then record the environment"
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

# Stop builds by default; a test suite runs only when the profile explicitly asks for it.
function Get-PaperStopVerbs {
    param($Profile)

    'build'
    if ($null -ne $Profile -and $null -ne $Profile['verify']) {
        $onStopTests = $Profile['verify']['onStopTests']
        if ($onStopTests -is [bool] -and $onStopTests) { 'test' }
    }
}

function Get-PaperStopVerdict {
    <#
    .SYNOPSIS
    What the Stop hook (hooks/verify-on-stop.ps1) does after one of its verbs ran: go on or block the stop,
    and the text it shows either way.
    .DESCRIPTION
    ExitCode 0 goes on to the next verb (and, after the last, the hook remembers this file state as
    verified); 2 blocks the stop. Message is '' or the text for stderr without the hook's "verify-on-stop: "
    prefix - a 0 with a Message is the prune notice, said without blocking.
      build  exit 0                                   -> 0
             every error a held output file           -> 2 "another process still holds the build output"
             any other failure                        -> 2 its error lines (12 at most, else its last 12)
      test   the run counted no test (F5)             -> 2 not verifiable, whatever it exited
             exit non-zero, no failing test named     -> 2 a run that proved nothing is not a pass
             a failure not in the baseline            -> 2 those names, and why the baseline did not cover them
             only known failures, or green            -> 0; on a green run the known failures that now pass
                                                         are named to prune (fewer than 50)
    .PARAMETER Failed
    The failing test names the hook read from Lines (Get-PaperFailedTestNames in hooks/session-state.ps1).
    .PARAMETER Baseline
    The names in the known-failures file the hook read, empty when it read none.
    .PARAMETER BaselinePath
    That file relative to the project root (Get-PaperKnownFailuresPath), '' when none resolved.
    .PARAMETER KnownFailures
    The profile's knownFailures as written, {config} and all: it tells "no baseline declared" apart from "a
    baseline per configuration, and no configuration named".
    #>
    param(
        [Parameter(Mandatory = $true)][ValidateSet('build', 'test')][string] $Verb,
        [int] $ExitCode,
        [AllowEmptyCollection()][AllowEmptyString()][string[]] $Lines = @(),
        [string] $Command,
        [AllowEmptyCollection()][string[]] $Failed = @(),
        [AllowEmptyCollection()][string[]] $Baseline = @(),
        [string] $BaselinePath,
        [string] $KnownFailures
    )

    function New-StopVerdict([int] $code, [string] $message) { return [pscustomobject]@{ ExitCode = $code; Message = $message } }
    $Lines = @($Lines | Where-Object { $null -ne $_ })
    $Failed = @($Failed | Where-Object { $_ })
    $Baseline = @($Baseline | Where-Object { $_ })

    if ($Verb -eq 'build') {
        if ($ExitCode -eq 0) { return New-StopVerdict 0 '' }
        $errors = @($Lines | Where-Object { $_ -match '(?i)\berror\b' } | Select-Object -First 12)
        if ($errors.Count -eq 0) { $errors = @($Lines | Select-Object -Last 12) }
        # A build whose every error is a held output file says nothing about the code: a test run, a harness
        # or an IDE still owns the file. Reported as "does not build" it sends the agent hunting for a
        # compile error that is not there - measured in a real project as 7 of 17 blocking stops. Still 2:
        # nothing was verified either way.
        if (Test-PaperLockOnlyBuildFailure $Lines) {
            return New-StopVerdict 2 ("NOT VERIFIED - another process still holds the build output; this is not a compile error.`n`n  " + (@($errors | Select-Object -First 3) -join "`n  ") + "`n`nStop whatever holds them (a UI test run, a harness, a test host, an IDE build), then let this run again.")
        }
        return New-StopVerdict 2 ("the build verb failed (exit $ExitCode) - this work does not build yet.`n`n  " + ($errors -join "`n  ") + "`n`n  command: $Command`nFix the build before reporting this work as done.")
    }

    # F5: the same verdict worktree.ps1 reads. A run that executed no test proved nothing, whatever it
    # exited, and is never remembered as verified.
    $tail = @($Lines | Select-Object -Last 10) -join "`n  "
    $run = Get-PaperTestRunVerdict -ExitCode $ExitCode -Output $Lines
    if ($run.ExitCode -eq 4) {
        return New-StopVerdict 2 ("NOT VERIFIED (verdict: not verifiable) - $($run.Reason).`n`n  " + $tail + "`n`n  command: $Command")
    }
    if ($ExitCode -ne 0 -and $Failed.Count -eq 0) {
        return New-StopVerdict 2 ("NOT VERIFIED - the test verb exited $ExitCode without naming a failing test.`n`n  " + $tail + "`n`n  command: $Command")
    }
    $new = @($Failed | Where-Object { $Baseline -notcontains $_ })
    if ($new.Count -gt 0) {
        # Why the baseline did not cover them. A {config} template with no configuration named was once read
        # as a file name, found no baseline, and blocked every known failure without saying why.
        $note = if ($BaselinePath) { "not in $BaselinePath" }
            elseif ($KnownFailures) { 'no baseline read: knownFailures is per configuration and neither verify.configuration nor PAPERFLOW_CONFIGURATION names one' }
            else { 'the profile declares no knownFailures' }
        return New-StopVerdict 2 ("$($new.Count) test(s) red ($note):`n`n  " + ($new -join "`n  ") + "`n`n  command: $Command`nFix them, or show they were red before this session, before reporting this work as done.")
    }
    $fixed = @($Baseline | Where-Object { $Failed -notcontains $_ })
    if ($ExitCode -eq 0 -and $fixed.Count -gt 0 -and $fixed.Count -lt 50) {
        return New-StopVerdict 0 ("on the known-failure list and now passing - prune ${BaselinePath}:`n  " + ($fixed -join "`n  "))
    }
    return New-StopVerdict 0 ''
}
