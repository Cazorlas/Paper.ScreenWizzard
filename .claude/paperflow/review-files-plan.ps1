# The decisions of the list of files to review (SPEC "Danh sach file phai xem", F20), pure and fully tested
# (tests/review-files.tests.ps1). review-files.ps1 runs git and ocr and hands their output here.
#
# Sources, one list:
#   ocr >= 1.9.0 (`--format` arrived then): `ocr delegate preview --format json` gives the files and why each
#   one is left out, `ocr delegate rule --format json <paths>` the rule group of each file.
#   otherwise git: `git diff --name-status` and `--numstat` against the merge-base plus untracked files, and
#   no rule groups. The first output line says which source and, for git, why.
#
# In the list: every reviewable file, and every file ocr leaves out ONLY for its extension (unsupported_ext:
# docs, views, project files - what a reviewer reads), marked "not under ocr rules". Out of it, each named
# with its reason: binary, deleted, secret_exclude, too_large and any other reason ocr gives; and, checked
# against the working tree after the merge, missing (not on disk), submodule, and for the git source
# "secret_exclude (name)".
#
# Exit: 0 the list is made | 5 no file changed at all. ocr JSON this cannot read is refused with a reason
# (ExitCode 2 of the reader); the shell then makes the list from git and names that reason.
#
# THIS FILE DECLARES NO param() BLOCK: it is dot-sourced. ASCII only: 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperOcrMinVersion = '1.9.0'
# Reasons ocr gives for leaving a file out that still put the file in front of a reviewer.
$script:PaperOcrKeptReasons = @('unsupported_ext')

function Select-PaperReviewSource {
    <#
    .SYNOPSIS
    Picks the source of the list from what `ocr --version` printed ('' when ocr was not found).
    #>
    param([string] $OcrVersionText, [int] $VersionExitCode = 0, [string] $NotRunnable = '')

    if ($NotRunnable) { return [pscustomobject]@{ Source = 'git'; Version = ''; Reason = $NotRunnable } }
    if ($VersionExitCode -ne 0) { return [pscustomobject]@{ Source = 'git'; Version = ''; Reason = "ocr --version failed (exit $VersionExitCode)" } }
    if (-not "$OcrVersionText".Trim()) {
        return [pscustomobject]@{ Source = 'git'; Version = ''; Reason = "ocr not found, needs $($script:PaperOcrMinVersion) or newer" }
    }
    $m = [regex]::Match($OcrVersionText, '(?<![\d.])v?(\d+)\.(\d+)\.(\d+)')
    if (-not $m.Success) {
        return [pscustomobject]@{ Source = 'git'; Version = ''; Reason = "ocr printed no version, needs $($script:PaperOcrMinVersion) or newer" }
    }
    $found = "$($m.Groups[1].Value).$($m.Groups[2].Value).$($m.Groups[3].Value)"
    if ([version] $found -lt [version] $script:PaperOcrMinVersion) {
        return [pscustomobject]@{ Source = 'git'; Version = $found; Reason = "ocr $found is older than $($script:PaperOcrMinVersion)" }
    }
    return [pscustomobject]@{ Source = 'ocr'; Version = $found; Reason = '' }
}

function New-PaperReviewFile {
    param(
        [string] $Path,
        [string] $Status,
        [int] $Insertions = 0,
        [int] $Deletions = 0,
        [string] $ExcludeReason = '',
        [switch] $NoOcrRule,
        # The path before a rename or the source of a copy, as git names it; '' otherwise.
        [string] $OldPath = ''
    )
    return [pscustomobject]@{
        Path = $Path; Status = $Status; Insertions = $Insertions; Deletions = $Deletions
        ExcludeReason = $ExcludeReason; NoOcrRule = [bool] $NoOcrRule; OldPath = $OldPath
    }
}

function Test-PaperJsonHas($Object, [string] $Name) {
    return ($null -ne $Object -and $Object -is [psobject] -and @($Object.PSObject.Properties.Name) -contains $Name)
}

function ConvertTo-PaperInt($Value) {
    $n = 0
    if ([int]::TryParse("$Value", [ref] $n)) { return $n }
    return 0
}

function ConvertFrom-PaperOcrPreview {
    <#
    .SYNOPSIS
    Reads `ocr delegate preview --format json`. ExitCode 2 with a Reason when it cannot be read.
    #>
    param([string] $Json)

    $refuse = { param($why) [pscustomobject]@{ ExitCode = 2; Reason = $why; Files = @() } }
    if (-not "$Json".Trim()) { return & $refuse 'ocr printed nothing' }
    try { $doc = $Json | ConvertFrom-Json }
    catch { return & $refuse ("ocr printed JSON this cannot read: " + (("$Json" -split "`n")[0]).Trim()) }
    foreach ($key in @('schema_version', 'reviewable_files', 'excluded_files')) {
        if (-not (Test-PaperJsonHas $doc $key)) { return & $refuse "ocr preview JSON has no $key" }
    }

    $files = @()
    foreach ($f in @($doc.reviewable_files)) {
        if ($null -eq $f) { continue }
        if (-not (Test-PaperJsonHas $f 'path') -or -not "$($f.path)") { return & $refuse 'ocr preview JSON has a file with no path' }
        $files += New-PaperReviewFile -Path "$($f.path)" -Status "$($f.status)" -Insertions (ConvertTo-PaperInt $f.insertions) -Deletions (ConvertTo-PaperInt $f.deletions)
    }
    foreach ($f in @($doc.excluded_files)) {
        if ($null -eq $f) { continue }
        if (-not (Test-PaperJsonHas $f 'path') -or -not "$($f.path)") { return & $refuse 'ocr preview JSON has a file with no path' }
        $reason = "$($f.exclude_reason)"
        if (-not $reason) { $reason = 'excluded' }
        $ins = ConvertTo-PaperInt $f.insertions
        $del = ConvertTo-PaperInt $f.deletions
        if ($script:PaperOcrKeptReasons -contains $reason) {
            $files += New-PaperReviewFile -Path "$($f.path)" -Status "$($f.status)" -Insertions $ins -Deletions $del -NoOcrRule
        }
        else {
            $files += New-PaperReviewFile -Path "$($f.path)" -Status "$($f.status)" -Insertions $ins -Deletions $del -ExcludeReason $reason
        }
    }
    return [pscustomobject]@{ ExitCode = 0; Reason = ''; Files = $files }
}

function ConvertFrom-PaperOcrRules {
    <#
    .SYNOPSIS
    Reads `ocr delegate rule --format json`. ExitCode 2 with a Reason when it cannot be read.
    #>
    param([string] $Json)

    $refuse = { param($why) [pscustomobject]@{ ExitCode = 2; Reason = $why; Groups = @() } }
    if (-not "$Json".Trim()) { return & $refuse 'ocr printed no rules' }
    try { $doc = $Json | ConvertFrom-Json }
    catch { return & $refuse ("ocr printed rule JSON this cannot read: " + (("$Json" -split "`n")[0]).Trim()) }
    if (-not (Test-PaperJsonHas $doc 'groups')) { return & $refuse 'ocr rule JSON has no groups' }

    $groups = @()
    foreach ($g in @($doc.groups)) {
        if ($null -eq $g) { continue }
        $groups += [pscustomobject]@{
            Pattern = "$($g.pattern)"
            Source  = "$($g.source)"
            Files   = @(@($g.files) | Where-Object { $_ } | ForEach-Object { "$_" })
            Rule    = ("$($g.rule)" -replace "`r`n", "`n")
        }
    }
    return [pscustomobject]@{ ExitCode = 0; Reason = ''; Groups = $groups }
}

# Rule groups of several `ocr delegate rule` calls (the paths go in batches) as one set: a group is its
# source and pattern.
function Merge-PaperRuleGroups($Groups) {
    $order = New-Object System.Collections.Generic.List[string]
    $byKey = @{}
    foreach ($g in @($Groups)) {
        if ($null -eq $g) { continue }
        $key = "$($g.Source)|$($g.Pattern)"
        if (-not $byKey.ContainsKey($key)) {
            $order.Add($key)
            $byKey[$key] = [pscustomobject]@{ Pattern = $g.Pattern; Source = $g.Source; Files = @(); Rule = $g.Rule }
        }
        foreach ($f in @($g.Files)) { if ($byKey[$key].Files -notcontains $f) { $byKey[$key].Files += $f } }
    }
    return @($order | ForEach-Object { $byKey[$_] })
}

$script:PaperGitStatusNames = @{ A = 'added'; M = 'modified'; D = 'deleted'; R = 'renamed'; C = 'copied'; T = 'modified'; U = 'unmerged' }

function ConvertFrom-PaperGitNameStatus {
    <#
    .SYNOPSIS
    Reads `git diff --name-status` and `git diff --numstat` of the same range into the list's shape. A rename
    or copy row (`R087<TAB>old<TAB>new`, `C075<TAB>source<TAB>copy`) is listed under its new path, with the
    first path as OldPath.
    .PARAMETER NumStat
    Its "- -" rows mark binary files; its counts become the insertions and deletions.
    #>
    param([string[]] $NameStatus, [string[]] $NumStat)

    $counts = @{}
    foreach ($line in @($NumStat)) {
        if (-not $line) { continue }
        $parts = $line -split "`t"
        if ($parts.Count -lt 3) { continue }
        # A rename in --numstat without -z is "old => new" or "dir/{a => b}/f" in one field; the last field
        # is the path when the rename is written out in full, so take the part after "=>" either way.
        $path = $parts[-1]
        if ($path -match '^(.*)\{[^{}]* => ([^{}]*)\}(.*)$') { $path = ($Matches[1] + $Matches[2] + $Matches[3]) -replace '//', '/' }
        elseif ($path -match ' => ') { $path = ($path -split ' => ')[-1] }
        $counts[$path] = [pscustomobject]@{ Binary = ($parts[0] -eq '-' -and $parts[1] -eq '-'); Ins = (ConvertTo-PaperInt $parts[0]); Del = (ConvertTo-PaperInt $parts[1]) }
    }

    $files = @()
    foreach ($line in @($NameStatus)) {
        if (-not $line) { continue }
        $parts = $line -split "`t"
        if ($parts.Count -lt 2) { continue }
        $letter = $parts[0].Substring(0, 1).ToUpperInvariant()
        $path = $parts[-1]
        $oldPath = ''
        if ($parts.Count -ge 3 -and ($letter -eq 'R' -or $letter -eq 'C')) { $oldPath = $parts[1] }
        $status = $script:PaperGitStatusNames[$letter]
        if (-not $status) { $status = 'modified' }
        $c = $counts[$path]
        $ins = 0; $del = 0; $binary = $false
        if ($null -ne $c) { $ins = $c.Ins; $del = $c.Del; $binary = $c.Binary }
        $reason = ''
        if ($letter -eq 'D') { $reason = 'deleted' }
        elseif ($binary) { $reason = 'binary' }
        $files += New-PaperReviewFile -Path $path -Status $status -Insertions $ins -Deletions $del -ExcludeReason $reason -OldPath $oldPath
    }
    return $files
}

function ConvertFrom-PaperGitRenames {
    <#
    .SYNOPSIS
    The renames in `git diff --name-status -M` output, as Old -> New pairs for Resolve-PaperReviewFiles. Read
    by ConvertFrom-PaperGitNameStatus, the one reader of that output; a copy is no rename (its source stays).
    #>
    param([string[]] $NameStatus)
    return @(@(ConvertFrom-PaperGitNameStatus -NameStatus $NameStatus -NumStat @()) |
        Where-Object { $_.Status -eq 'renamed' -and $_.OldPath } |
        ForEach-Object { [pscustomobject]@{ Old = $_.OldPath; New = $_.Path } })
}

function ConvertFrom-PaperGitGitlinks {
    <#
    .SYNOPSIS
    The gitlinks (submodules) in `git ls-files -s` output: the rows of mode 160000, by their path.
    #>
    param([string[]] $StageLines)
    $links = @()
    foreach ($line in @($StageLines)) {
        if ("$line" -match '^160000 \S+ \d+\t(.+)$') { $links += $Matches[1] }
    }
    return $links
}

function Get-PaperReviewSourceLabel {
    <#
    .SYNOPSIS
    The source the first line names, and whether the secret-name rule applies (SecretNames: the git source
    only, ocr has its own check). Selected is Select-PaperReviewSource's answer; OcrFailed why ocr, picked,
    gave no list ('' when it did).
    #>
    param($Selected, [string] $OcrFailed = '')
    if ("$($Selected.Source)" -ne 'ocr') { return [pscustomobject]@{ Label = "git ($($Selected.Reason))"; SecretNames = $true } }
    if ($OcrFailed) { return [pscustomobject]@{ Label = "git (ocr failed: $OcrFailed)"; SecretNames = $true } }
    return [pscustomobject]@{ Label = "ocr $($Selected.Version)"; SecretNames = $false }
}

function ConvertFrom-PaperGitUntracked {
    <#
    .SYNOPSIS
    Untracked files as added ones; those the shell found binary are out with that reason.
    .PARAMETER Insertions
    Path -> its line count, which is what a new file inserts. Optional.
    #>
    param([string[]] $Paths, [string[]] $BinaryPaths, [hashtable] $Insertions = @{})
    $files = @()
    foreach ($p in @($Paths)) {
        if (-not $p) { continue }
        $reason = ''
        if (@($BinaryPaths) -contains $p) { $reason = 'binary' }
        $ins = 0
        if ($null -ne $Insertions -and $Insertions.ContainsKey($p)) { $ins = [int] $Insertions[$p] }
        $files += New-PaperReviewFile -Path $p -Status 'added' -Insertions $ins -ExcludeReason $reason
    }
    return $files
}

function Merge-PaperReviewFiles {
    <#
    .SYNOPSIS
    Two sets of changed files as one, each path once. Then is the later state (the working tree after the
    commits): its status and reason win, except that a file the branch added stays added; counts add up.
    #>
    param($First, $Then)

    $order = New-Object System.Collections.Generic.List[string]
    $byPath = @{}
    foreach ($f in @(@($First) + @($Then))) {
        if ($null -eq $f) { continue }
        $key = "$($f.Path)".ToLowerInvariant()
        if (-not $byPath.ContainsKey($key)) {
            $order.Add($key)
            $byPath[$key] = New-PaperReviewFile -Path $f.Path -Status $f.Status -Insertions $f.Insertions -Deletions $f.Deletions -ExcludeReason $f.ExcludeReason -NoOcrRule:([bool] $f.NoOcrRule)
            continue
        }
        $was = $byPath[$key]
        $status = $f.Status
        if ($was.Status -eq 'added' -and $f.Status -eq 'modified') { $status = 'added' }
        # Deleted by the branch and created again: a change to a file the base had.
        if ($was.Status -eq 'deleted' -and $f.Status -eq 'added') { $status = 'modified' }
        $byPath[$key] = New-PaperReviewFile -Path $f.Path -Status $status -Insertions ($was.Insertions + $f.Insertions) -Deletions ($was.Deletions + $f.Deletions) -ExcludeReason $f.ExcludeReason -NoOcrRule:([bool] $f.NoOcrRule)
    }
    return @($order | ForEach-Object { $byPath[$_] })
}

function Get-PaperReviewList {
    <#
    .SYNOPSIS
    The files to review, each with the patterns of the rule groups it falls under, and the files left out.
    ExitCode 5 when no file changed at all; a change that only deletes still makes a list (total 0).
    #>
    param($Files, $Groups)

    $all = @(@($Files) | Where-Object { $null -ne $_ })
    $groupList = @(@($Groups) | Where-Object { $null -ne $_ })
    $review = @()
    foreach ($f in @($all | Where-Object { -not $_.ExcludeReason })) {
        $rules = @()
        if (-not $f.NoOcrRule) {
            foreach ($g in $groupList) { if (@($g.Files) -contains $f.Path) { $rules += $g.Pattern } }
        }
        $review += [pscustomobject]@{
            Path = $f.Path; Status = $f.Status; Insertions = $f.Insertions; Deletions = $f.Deletions
            NoOcrRule = [bool] $f.NoOcrRule; Rules = $rules
        }
    }
    $excluded = @($all | Where-Object { $_.ExcludeReason })
    $code = 0
    if ($all.Count -eq 0) { $code = 5 }
    return [pscustomobject]@{ ExitCode = $code; Total = $review.Count; Review = $review; Excluded = $excluded; Groups = $groupList }
}

function Format-PaperReviewFile($File, [bool] $WithRules) {
    $line = "  {0}   {1} +{2} -{3}" -f $File.Path, $File.Status, $File.Insertions, $File.Deletions
    if ($File.NoOcrRule) { return "$line  [not under ocr rules]" }
    if ($WithRules -and @($File.Rules).Count -gt 0) { return "$line  [rule: $(@($File.Rules) -join ', ')]" }
    return $line
}

function Format-PaperReviewList {
    <#
    .SYNOPSIS
    The text every reviewer receives: a first line with count, base, merge-base and source; each rule group
    with its text once and its files; the other files; the files left out with their reason; "total: N".
    #>
    # From: where the change is measured from, the line every verb of the kit prints the same way
    # (Format-PaperChangeFrom in change-set.ps1).
    param($List, [string] $From, [string] $Source)

    $from = $From
    if ($List.ExitCode -eq 5) {
        return @("review-files: NOT APPLICABLE - no file changed - $from, source $Source", 'total: 0')
    }

    $out = @("review-files: $($List.Total) to review - $from, source $Source")
    $shown = @{}
    foreach ($g in @($List.Groups)) {
        $members = @(@($List.Review) | Where-Object { @($_.Rules) -contains $g.Pattern -and -not $shown.ContainsKey($_.Path) })
        if ($members.Count -eq 0) { continue }
        $out += "rules: $($g.Pattern) ($($g.Source)) - $($members.Count) file(s)"
        foreach ($r in @("$($g.Rule)" -split "`n")) { $out += ("    " + $r).TrimEnd() }
        foreach ($f in $members) { $out += Format-PaperReviewFile $f $true; $shown[$f.Path] = $true }
    }
    $rest = @(@($List.Review) | Where-Object { -not $shown.ContainsKey($_.Path) })
    if ($rest.Count -gt 0 -and $shown.Count -gt 0) { $out += 'other files:' }
    foreach ($f in $rest) { $out += Format-PaperReviewFile $f $true }
    $out += "excluded: $(@($List.Excluded).Count)"
    foreach ($f in @($List.Excluded)) { $out += ("  {0}   {1}" -f $f.Path, $f.ExcludeReason) }
    $out += "total: $($List.Total)"
    return $out
}

# ------------------------------------------------------------------ running ocr without cmd.exe

function Get-PaperOcrShimScript {
    <#
    .SYNOPSIS
    The .js a .cmd/.bat shim runs with node (npm writes `"%dp0%\node_modules\...\ocr.js" %*`), relative to the
    shim's folder with backslashes; '' when the shim runs no .js. ocr is then started as `node <that .js>`,
    because cmd.exe re-reads every argument of a .cmd: `src/R&D.cs` runs D.cs, %VAR% expands.
    #>
    param([string] $ShimText)
    $m = [regex]::Match("$ShimText", '"%~?dp0%?[\\/]?([^"%]+?\.js)"', 'IgnoreCase')
    if (-not $m.Success) { return '' }
    return ($m.Groups[1].Value -replace '/', '\')
}

function ConvertTo-PaperProcessArgument {
    <#
    .SYNOPSIS
    One argument written so that CommandLineToArgvW (and node, and every C runtime) reads it back unchanged:
    quoted when empty or holding a space, tab or quote; a quote escaped with a backslash, and the
    backslashes right before a quote or the closing quote doubled.
    #>
    param([string] $Value)
    if ($Value -and $Value -notmatch '[\s"]') { return $Value }
    $sb = New-Object System.Text.StringBuilder
    [void] $sb.Append('"')
    $slashes = 0
    foreach ($ch in $Value.ToCharArray()) {
        if ($ch -eq '\') { $slashes++; continue }
        if ($ch -eq '"') { [void] $sb.Append('\', 2 * $slashes + 1); [void] $sb.Append('"') }
        else {
            if ($slashes -gt 0) { [void] $sb.Append('\', $slashes) }
            [void] $sb.Append($ch)
        }
        $slashes = 0
    }
    if ($slashes -gt 0) { [void] $sb.Append('\', 2 * $slashes) }
    [void] $sb.Append('"')
    return $sb.ToString()
}

# ------------------------------------------------------------------ what the shell asks ocr

# The files `ocr delegate rule` is asked about: the ones to review that are under ocr rules.
function Get-PaperOcrRulePaths($Files) {
    return @(@($Files) | Where-Object { $null -ne $_ -and -not $_.ExcludeReason -and -not $_.NoOcrRule } | ForEach-Object { "$($_.Path)" })
}

function Split-PaperOcrRuleBatches {
    <#
    .SYNOPSIS
    The rule call's paths in batches cut by length (each path costs its length plus a quote pair or a space),
    never by count: 40 paths of 200 characters overflow a command line that 40 short ones fit in. A path
    longer than the limit is a batch of its own.
    #>
    param([string[]] $Paths, [int] $MaxChars = 6000)
    $batches = @()
    $current = @()
    $size = 0
    foreach ($p in @($Paths)) {
        if (-not $p) { continue }
        $cost = $p.Length + 3
        if ($current.Count -gt 0 -and $size + $cost -gt $MaxChars) {
            $batches += , $current
            $current = @(); $size = 0
        }
        $current += $p
        $size += $cost
    }
    if ($current.Count -gt 0) { $batches += , $current }
    # Each batch leaves as one object: callers collect them with @().
    return $batches
}

# Whether the branch has commits of its own to ask ocr about (the range merge-base..HEAD).
function Test-PaperReviewNeedsRange([bool] $HasHead, [string] $MergeBase, [string] $Head) {
    return ($HasHead -and $MergeBase -and $MergeBase -ne 'HEAD' -and $MergeBase -ne $Head)
}

# ------------------------------------------------------------------ after the merge

# Obvious secret files by their name alone, for the git source (ocr has its own check): a reviewer agent is
# never handed a private key or an environment file.
function Test-PaperSecretName([string] $Path) {
    $leaf = ("$Path" -split '/')[-1].ToLowerInvariant()
    return ($leaf -match '^\.env' -or $leaf -match '^id_rsa' -or $leaf -match '^id_ed25519' -or $leaf -match '\.(pem|pfx|p12|key)$')
}

function Resolve-PaperReviewFiles {
    <#
    .SYNOPSIS
    The merged list checked against the working tree, in this order:
      - a rename not committed yet (Renames: Old -> New, from `git diff --name-status -M HEAD`) folds the old
        path into the new one: its counts, and "added" when the branch added it;
      - a gitlink (Submodules) or a folder git lists with a trailing '/' is out as submodule;
      - a file to review that is not on disk (Missing) is out as missing;
      - with -SecretNames (the git source), an obvious secret by its name is out as "secret_exclude (name)".
    #>
    param($Files, $Renames, [string[]] $Missing = @(), [string[]] $Submodules = @(), [switch] $SecretNames)

    $key = { param($p) ("$p" -replace '\\', '/').ToLowerInvariant() }
    $order = New-Object System.Collections.Generic.List[string]
    $byPath = @{}
    foreach ($f in @($Files)) {
        if ($null -eq $f) { continue }
        $k = & $key $f.Path
        if (-not $byPath.ContainsKey($k)) { $order.Add($k) }
        $byPath[$k] = New-PaperReviewFile -Path $f.Path -Status $f.Status -Insertions $f.Insertions -Deletions $f.Deletions -ExcludeReason $f.ExcludeReason -NoOcrRule:([bool] $f.NoOcrRule)
    }

    foreach ($pair in @($Renames)) {
        if ($null -eq $pair -or -not $pair.Old -or -not $pair.New) { continue }
        $ko = & $key $pair.Old
        $kn = & $key $pair.New
        if (-not $byPath.ContainsKey($ko) -or $ko -eq $kn) { continue }
        $old = $byPath[$ko]
        $byPath.Remove($ko)
        [void] $order.Remove($ko)
        if ($byPath.ContainsKey($kn)) {
            $new = $byPath[$kn]
            $status = if ($old.Status -eq 'added') { 'added' } else { $new.Status }
            $byPath[$kn] = New-PaperReviewFile -Path $new.Path -Status $status -Insertions ($old.Insertions + $new.Insertions) -Deletions ($old.Deletions + $new.Deletions) -ExcludeReason $new.ExcludeReason -NoOcrRule:([bool] $new.NoOcrRule)
        }
        else {
            $order.Add($kn)
            $byPath[$kn] = New-PaperReviewFile -Path $pair.New -Status $old.Status -Insertions $old.Insertions -Deletions $old.Deletions -ExcludeReason $old.ExcludeReason -NoOcrRule:([bool] $old.NoOcrRule)
        }
    }

    $missingSet = @{}
    foreach ($p in @($Missing)) { if ($p) { $missingSet[(& $key $p)] = $true } }
    $moduleSet = @{}
    foreach ($p in @($Submodules)) { if ($p) { $moduleSet[(& $key $p).TrimEnd('/')] = $true } }

    $out = @()
    foreach ($k in $order) {
        $f = $byPath[$k]
        if ($f.ExcludeReason -ne 'deleted' -and ($moduleSet.ContainsKey($k.TrimEnd('/')) -or "$($f.Path)".EndsWith('/'))) { $f.ExcludeReason = 'submodule' }
        elseif (-not $f.ExcludeReason -and $missingSet.ContainsKey($k)) { $f.ExcludeReason = 'missing' }
        elseif (-not $f.ExcludeReason -and $SecretNames -and (Test-PaperSecretName $f.Path)) { $f.ExcludeReason = 'secret_exclude (name)' }
        $out += $f
    }
    return $out
}
