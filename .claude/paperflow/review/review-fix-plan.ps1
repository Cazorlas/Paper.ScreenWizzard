# Pure decisions and Markdown for the /review-ui fix handoff. ASCII, PowerShell 5.1.
function Get-PaperReviewFixPlan {
    param($Findings, [string[]] $Ids)
    $requested = @($Ids | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $selected = @()
    if ($requested.Count -eq 0) { return [pscustomobject]@{ Error = 'fix needs -Ids <ids|all>'; Groups = @() } }
    foreach ($id in $requested) {
        if ($id -ieq 'all') {
            $selected += @($Findings | Where-Object { $_.Lane -eq 'ui' -and $_.Rule.Trim() -ine 'overall' })
            continue
        }
        $found = @($Findings | Where-Object { $_.Id -ieq $id })
        if ($found.Count -eq 0) { return [pscustomobject]@{ Error = "unknown finding id: $id"; Groups = @() } }
        if ($found[0].Lane -ne 'ui') { return [pscustomobject]@{ Error = "$id belongs to another lane; fix is a /review-ui step"; Groups = @() } }
        $selected += $found[0]
    }
    $groups = @(); $seen = @{}; $folders = @{}
    foreach ($f in $selected) {
        if ($seen.ContainsKey($f.Id)) { continue }; $seen[$f.Id] = $true
        $file = ("$($f.Where)" -replace ':\d+$', '').Replace('\', '/')
        if (-not $file -or $file -match '(^/|^[A-Za-z]:|(^|/)\.\.(/|$))') { return [pscustomobject]@{ Error = "invalid WHERE for $($f.Id)"; Groups = @() } }
        $cut = $file.LastIndexOf('/')
        $folder = if ($cut -lt 0) { '.' } else { $file.Substring(0, $cut) }
        if (-not $folders.ContainsKey($folder)) {
            $group = [pscustomobject]@{ Folder = $folder; Files = @(); Findings = @() }
            $folders[$folder] = $group; $groups += $group
        }
        $group = $folders[$folder]
        if ($group.Files -notcontains $file) { $group.Files += $file }
        $group.Findings += $f
    }
    return [pscustomobject]@{ Error = ''; Groups = $groups }
}

function Get-PaperReviewFixDoneWhen {
    param($Group)
    $lines = @('- All selected findings below are fixed within the allowed files; preserve unrelated work.')
    $extensions = @($Group.Files | ForEach-Object { [IO.Path]::GetExtension($_).ToLowerInvariant() })
    if (@($extensions | Where-Object { $_ -in @('.html','.htm','.css','.scss','.sass','.less','.js','.jsx','.ts','.tsx','.vue','.svelte','.astro') }).Count) {
        $rules = @($Group.Findings | Where-Object { [IO.Path]::GetExtension(($_.Where -replace ':\d+$','')) -in @('.html','.htm','.css','.scss','.sass','.less','.js','.jsx','.ts','.tsx','.vue','.svelte','.astro') } | ForEach-Object { $_.Rule } | Select-Object -Unique) -join ', '
        $lines += "- review.ps1 detect on the fixed files shows none of the chosen rules: $rules."
        $lines += '- Provide a Playwright capture of the affected screen and inspect the result.'
    }
    if (@($extensions | Where-Object { $_ -in @('.xaml','.axaml','.dcl') }).Count) {
        $lines += '- Provide a drive-wpf capture in both themes (light and dark), checked against the project WPF style skill.'
    }
    if (@($extensions | Where-Object { $_ -notin @('.html','.htm','.css','.scss','.sass','.less','.js','.jsx','.ts','.tsx','.vue','.svelte','.astro','.xaml','.axaml','.dcl') }).Count) {
        $lines += '- Use the project capture command if declared; otherwise report visual check: not run - <reason>.'
    }
    return $lines
}

function Format-PaperReviewFixFindings {
    param($Group)
    $lines = @()
    foreach ($f in $Group.Findings) {
        $lines += @("### $($f.Id)", '', "RULE $($f.Rule)", "WHERE $($f.Where)", "WHY $($f.Why)", "FIX $($f.Fix)", '')
    }
    return $lines
}

function New-PaperReviewFixTaskSpec {
    param($Groups, [string] $Run)
    $lines = @('# UI fixes', '', "Review run: $Run. Apply these selected findings using the project task lifecycle.", '')
    foreach ($g in $Groups) {
        $lines += @("## $($g.Folder)", '', ('{files: ' + ($g.Files -join ', ') + '}'), '')
        $lines += @(Format-PaperReviewFixFindings $g)
        $lines += @('### Done when', '') + @(Get-PaperReviewFixDoneWhen $g) + @('')
    }
    return ($lines -join "`n") + "`n"
}

function New-PaperReviewFixBrief {
    param($Group, [string] $Run, [string] $Repo)
    $lines = @('## Goal', '', "Fix the selected UI findings from review $Run in repository $Repo. Read the project instructions and reproduce the affected screen before editing.", '')
    $lines += @(Format-PaperReviewFixFindings $Group)
    $lines += @('## Files you may touch', '', ('{files: ' + ($Group.Files -join ', ') + '}'), '', '## Done when', '')
    $lines += @(Get-PaperReviewFixDoneWhen $Group)
    $lines += @('', '## How A will check', '', '- Read the diff against each RULE, WHERE, WHY and FIX above.', '- Run the project checks for the changed files and inspect the captures required by Done when. Report exact commands, results and any unavailable visual check.', '')
    return ($lines -join "`n") + "`n"
}
