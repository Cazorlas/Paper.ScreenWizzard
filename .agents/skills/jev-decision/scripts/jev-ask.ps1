# Classifies a batch of items with Jev: one call per item, one JSON line per answer.
#
#   jev-ask.ps1 -Questions <questions.json> -Items <file> [-DryRun] [-Model <id>]
#
# -Items is a JSONL file ({"id","input"} per line) or a plain text file (each non-empty line is one item,
# id = its line number). With -DryRun nothing leaves the machine: it prints "item <id>: request <n> chars"
# per item. Otherwise each item is one call on the same route as jev-eval (Get-JevRoute); the switch and the
# keys come from this run's environment only and the key is never printed. The switch is checked before any
# call, so an off value (0, false, off, no) or no key exits 5 without a call. One JSON line per item, then
# "jev-ask: <n> items, <k> answered".
# Exit: 0 done | 1 a call failed | 2 bad arguments or a bad -Items line | 5 Jev off or no key.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.
param(
    [string] $Questions = '',
    [string] $Items = '',
    [switch] $DryRun,
    [string] $Model = ''
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'jev-eval-plan.ps1')

function Read-JsonFile([string] $Path) {
    return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
}

if (-not $Questions) { [Console]::Error.WriteLine('jev-ask: -Questions is required'); exit 2 }
if (-not $Items) { [Console]::Error.WriteLine('jev-ask: -Items is required'); exit 2 }
if (-not (Test-Path -LiteralPath $Questions)) { [Console]::Error.WriteLine("jev-ask: -Questions not found: $Questions"); exit 2 }
if (-not (Test-Path -LiteralPath $Items)) { [Console]::Error.WriteLine("jev-ask: -Items not found: $Items"); exit 2 }

# $questionSet, not $questions: PowerShell names are case-insensitive, so $questions IS the [string] parameter $Questions and would turn the parsed object back into a string.
try { $questionSet = Read-JsonFile $Questions }
catch { [Console]::Error.WriteLine("jev-ask: cannot read -Questions: $($_.Exception.Message)"); exit 2 }

# A .jsonl file is one JSON object per line, each with an "input"; a text file is one item per non-empty
# line whose id is its line number. A JSONL line that is not JSON is a bad argument, naming the line.
$isJsonl = $Items.EndsWith('.jsonl', [StringComparison]::OrdinalIgnoreCase)
$list = New-Object System.Collections.Generic.List[object]
$lines = @(Get-Content -LiteralPath $Items -Encoding UTF8)
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if (-not $line.Trim()) { continue }
    if ($isJsonl) {
        try { $obj = $line | ConvertFrom-Json }
        catch { [Console]::Error.WriteLine("jev-ask: -Items line $($i + 1) is not JSON"); exit 2 }
        $list.Add([pscustomobject]@{ Id = [string]$obj.id; Input = [string]$obj.input })
    } else {
        $list.Add([pscustomobject]@{ Id = [string]($i + 1); Input = $line })
    }
}

if ($DryRun) {
    $previewModel = 'typesafe/jev-1.13'
    if ($Model) { $previewModel = $Model }
    foreach ($it in $list) {
        $body = New-JevRequestBody ([string]$it.Input) $questionSet $previewModel | ConvertTo-Json -Depth 20
        Write-Output ("item {0}: request {1} chars" -f $it.Id, $body.Length)
    }
    exit 0
}

$route = Get-JevRoute -Switch $env:PAPER_JEV -TypeSafeKey $env:TYPESAFE_API_KEY -OpenRouterKey $env:OPENROUTER_API_KEY
if ($route.Outcome -eq 'off') { Write-Output ("jev-ask: switched off - PAPER_JEV is '{0}', an off value (0, false, off, no); Jev is on by default" -f $env:PAPER_JEV); exit 5 }
if ($route.Outcome -eq 'no-key') { Write-Output "jev-ask: on, but no OPENROUTER_API_KEY (nor TYPESAFE_API_KEY) in this run's environment"; exit 5 }
$endpoint = $route.Endpoint
$key = $route.Key
if (-not $Model) { $Model = $route.Model }

# Windows PowerShell 5.1 on .NET Framework may not offer TLS 1.2 by default; add it, keep what is there.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$answered = 0
$failed = 0
$firstFailure = ''
foreach ($it in $list) {
    $body = New-JevRequestBody ([string]$it.Input) $questionSet $Model | ConvertTo-Json -Depth 20
    $answer = $null
    try {
        $resp = Invoke-RestMethod -Method Post -Uri $endpoint -Headers @{ Authorization = "Bearer $key" } `
            -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 30
        $why = Get-JevResponseFailure $resp
        if ($why) { throw $why }
        $answer = Read-JevAnswer $resp $questionSet
    } catch {
        $failed++
        if (-not $firstFailure) { $firstFailure = ([string]$_.Exception.Message).Replace($key, '<key>') }
    }
    $row = [ordered]@{ id = [string]$it.Id }
    $got = $false
    foreach ($q in $questionSet.PSObject.Properties) {
        $a = $null
        if ($answer) { $a = $answer[$q.Name] }
        if ($null -ne $a) {
            $got = $true
            $row[$q.Name] = [ordered]@{ answer = [string]$a.Choice; confidence = [double]$a.Confidence; reason = [string]$a.Source }
        } else {
            $row[$q.Name] = [ordered]@{ answer = $null; confidence = $null; reason = 'no-answer' }
        }
    }
    if ($got) { $answered++ }
    Write-Output ($row | ConvertTo-Json -Compress -Depth 10)
}

Write-Output ("jev-ask: {0} items, {1} answered" -f $list.Count, $answered)
if ($failed -gt 0) {
    Write-Output ("jev-ask: {0} of {1} calls failed; first failure: {2}" -f $failed, $list.Count, $firstFailure)
    exit 1
}
exit 0
