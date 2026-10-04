# Scores one jev-decision recipe on its own fixture: keyword rules always (offline, free), Jev only when asked.
#
#   jev-eval.ps1 -Recipe <recipes\<slug> folder> [-Jev] [-Model <id>]
#
# Without -Jev nothing leaves the machine. With -Jev, Jev is on by default (ADR-0042): the run refuses (exit 5)
# only when PAPER_JEV holds an off value (0, false, off, no - trimmed, any case) or no key exists. The key comes
# from this run's environment only (never the registry, never a file) and is never printed: OPENROUTER_API_KEY
# first (base https://openrouter.ai/api + /v1/systemone - NOT /api/v1, which answers 404 - model
# typesafe/jev-1.13), else TYPESAFE_API_KEY (api.typesafe.ai, model jev-latest). Get-JevRoute decides.
# Only fixture rows marked synthetic are ever sent - the fixture ships into every ai project and its numbers
# must re-run; the fixture check runs before the switch and refuses any other row.
# A Jev answer that is missing, outside the options or with a confidence outside 0-1 falls back to the rule's
# answer for that question; no consistency rule of the recipe is applied after Jev.
# With no failed call it prints the jev line to copy verbatim into recipe.md (Format-JevLine, today's date).
#
# Exit: 0 scored | 1 a Jev call failed - nothing to record | 2 bad arguments or the recipe's parts disagree
#       | 5 Jev asked for but switched off or no key
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.
param(
    [Parameter(Mandatory = $true)][string] $Recipe,
    [switch] $Jev,
    [string] $Model = ''
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'jev-eval-plan.ps1')

function Read-JsonFile([string] $Path) {
    return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
}

try {
    foreach ($f in 'questions.json', 'rules.json', 'fixture.jsonl') {
        if (-not (Test-Path -LiteralPath (Join-Path $Recipe $f))) { [Console]::Error.WriteLine("jev-eval: $Recipe has no $f"); exit 2 }
    }
    $questions = Read-JsonFile (Join-Path $Recipe 'questions.json')
    $rules = Read-JsonFile (Join-Path $Recipe 'rules.json')
    $rows = @(Get-Content -LiteralPath (Join-Path $Recipe 'fixture.jsonl') -Encoding UTF8 | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
} catch {
    [Console]::Error.WriteLine("jev-eval: cannot read recipe: $($_.Exception.Message)"); exit 2
}

$problems = Test-JevRecipeParts $questions $rules $rows
if ($problems.Count -gt 0) { $problems | ForEach-Object { [Console]::Error.WriteLine("jev-eval: $_") }; exit 2 }

$rulePred = @{}
foreach ($row in $rows) { $rulePred[[string]$row.id] = Invoke-JevRules ([string]$row.input) $rules }
$ruleScore = Get-JevScore $rows $rulePred

function Write-Score([string] $Label, $Score) {
    $parts = foreach ($k in $Score.PerQuestion.Keys) { '{0} {1}/{2}' -f $k, $Score.PerQuestion[$k].Right, $Score.PerQuestion[$k].Total }
    Write-Output ('{0}: {1}/{2} rows, hard {3}/{4} | {5}' -f $Label, $Score.RowsRight, $Score.Rows, $Score.HardRight, $Score.HardRows, ($parts -join ' | '))
}

Write-Score 'rules' $ruleScore
if (-not $Jev) { exit 0 }

$route = Get-JevRoute -Switch $env:PAPER_JEV -TypeSafeKey $env:TYPESAFE_API_KEY -OpenRouterKey $env:OPENROUTER_API_KEY
if ($route.Outcome -eq 'off') { Write-Output ("jev: switched off - PAPER_JEV is '{0}', an off value (0, false, off, no); Jev is on by default" -f $env:PAPER_JEV); exit 5 }
if ($route.Outcome -eq 'no-key') { Write-Output "jev: on, but no OPENROUTER_API_KEY (nor TYPESAFE_API_KEY) in this run's environment"; exit 5 }
$endpoint = $route.Endpoint
$key = $route.Key
if (-not $Model) { $Model = $route.Model }

# Windows PowerShell 5.1 on .NET Framework may not offer TLS 1.2 by default; add it, keep what is there.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$jevPred = @{}
$tokens = 0
$failed = 0
$firstFailure = ''
$watch = [Diagnostics.Stopwatch]::StartNew()
foreach ($row in $rows) {
    $body = New-JevRequestBody ([string]$row.input) $questions $Model | ConvertTo-Json -Depth 20
    $answer = $null
    try {
        $resp = Invoke-RestMethod -Method Post -Uri $endpoint -Headers @{ Authorization = "Bearer $key" } `
            -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 30
        $why = Get-JevResponseFailure $resp
        if ($why) { throw $why }
        $answer = Read-JevAnswer $resp $questions
        if ($resp.usage -and $resp.usage.input_tokens) { $tokens += [int]$resp.usage.input_tokens }
    } catch {
        $failed++
        if (-not $firstFailure) { $firstFailure = ([string]$_.Exception.Message).Replace($key, '<key>') }
    }
    $jevPred[[string]$row.id] = Merge-JevPrediction -JevAnswer $answer -RuleAnswer $rulePred[[string]$row.id] -Questions $questions
}
$seconds = $watch.Elapsed.TotalSeconds
if ($failed -gt 0) {
    Write-Output ('jev: {0} of {1} calls failed - nothing to record; first failure: {2}' -f $failed, $rows.Count, $firstFailure)
    exit 1
}
$jevScore = Get-JevScore $rows $jevPred
Write-Output (Format-JevLine -Score $jevScore -Model $Model -Date (Get-Date -Format 'yyyy-MM-dd'))
Write-Score 'jev per question (rules fill gaps)' $jevScore
$still = @($jevScore.HardWrong)
if ($still.Count -eq 0) { Write-Output 'jev hard still wrong: none' } else { Write-Output ('jev hard still wrong: ' + ($still -join ', ')) }
Write-Output ('jev: {0} calls, 0 failed, {1} input tokens, {2} s' -f $rows.Count, $tokens, $seconds.ToString('0.0', [Globalization.CultureInfo]::InvariantCulture))
exit 0
