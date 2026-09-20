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
$script:PaperInternalVerbs = @('tasks', 'status', 'api-check', 'review-files')

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

function Get-PaperTestRunVerdict {
    <#
    .SYNOPSIS
    The verdict for one test run, from its exit code and what it printed (F5).
    .DESCRIPTION
    A runner that ran nothing has proved nothing: a filter that matched no test, an assembly that was not
    built, a host that was not up all exit 0 on some runners. So the total is read from the output -
    "total: N" (Microsoft.Testing.Platform), "Total tests: N" and "Total: N" (dotnet test / VSTest), the
    last one printed wins - and:
      total > 0, exit 0              -> 0 pass
      total > 0, exit non-zero       -> 1 fail
      total 0 or no total, exit 0    -> 4 not verifiable
      total 0, exit non-zero         -> 4 not verifiable (Microsoft.Testing.Platform exits 8 for zero tests)
      no total, exit non-zero        -> 1 fail (the run broke before counting - a build error, a crash)
    #>
    param([int] $ExitCode, $Output)

    $text = (@($Output) | ForEach-Object { [string] $_ }) -join "`n"
    $total = $null
    foreach ($m in [regex]::Matches($text, '(?im)\btotal(?:\s+tests)?\s*:\s*(\d+)')) { $total = [int] $m.Groups[1].Value }

    if ($null -ne $total -and $total -gt 0) {
        if ($ExitCode -eq 0) { return [pscustomobject]@{ ExitCode = 0; Verdict = 'pass'; Total = $total; Reason = "$total test(s) ran and passed" } }
        return [pscustomobject]@{ ExitCode = 1; Verdict = 'fail'; Total = $total; Reason = "$total test(s) ran, exit $ExitCode" }
    }
    if ($ExitCode -ne 0 -and $null -eq $total) {
        return [pscustomobject]@{ ExitCode = 1; Verdict = 'fail'; Total = $null; Reason = "the run exited $ExitCode before reporting a test total" }
    }
    $said = 'no test total in the output'
    if ($null -ne $total) { $said = 'total: 0' }
    return [pscustomobject]@{ ExitCode = 4; Verdict = 'not verifiable'; Total = $total; Reason = "$said (exit $ExitCode): zero tests ran, which proves nothing - run once more, then record the environment" }
}
