# The decisions of diagram.ps1, as pure functions: text and edges in, text out. No file is read or written
# here, so tests/diagram.tests.ps1 can check every rule without a repository.
#
# A diagram in a Paper document is Mermaid inside the markdown, never an image file. Three reasons, all
# measured rather than stylistic: it diffs (a reviewer sees which edge changed), every gate the kit already
# has keeps reading the file as text, and it cannot drift away from the document it describes the way a PNG
# in an assets folder does. What a diagram may never be is the only place a fact lives - that rule belongs to
# the document, and paper-kit/docs/ARTIFACT-REGISTRY.md states it.
#
# ASCII with a UTF-8 BOM, PowerShell 5.1 syntax.

$script:PaperDiagramOpen = '<!-- paper-diagram: {0} -->'
$script:PaperDiagramClose = '<!-- /paper-diagram -->'

<#
.SYNOPSIS
    The ProjectReference edges between the projects of one module group.
.DESCRIPTION
    Takes a map of project name -> csproj text and the module the group belongs to. An edge is kept only
    when both ends are in the group, so a diagram of Paper.Structural does not grow every library the
    solution has; that is what makes it readable and what makes it stable between runs.
#>
function Get-PaperDiagramEdges {
    param(
        [Parameter(Mandatory = $true)] [hashtable] $Projects,
        [Parameter(Mandatory = $true)] [string] $Module
    )

    $members = @($Projects.Keys | Where-Object {
        $_ -eq $Module -or $_.StartsWith($Module + '.', [System.StringComparison]::OrdinalIgnoreCase)
    } | Sort-Object)

    $edges = New-Object System.Collections.ArrayList
    foreach ($name in $members) {
        $text = [string] $Projects[$name]
        foreach ($match in [regex]::Matches($text, '<ProjectReference[^>]*Include="([^"]+)"')) {
            $target = [System.IO.Path]::GetFileNameWithoutExtension($match.Groups[1].Value)
            if ($members -notcontains $target) { continue }
            if ($target -eq $name) { continue }
            $null = $edges.Add([pscustomobject]@{ From = $name; To = $target })
        }
    }

    # Sorted so the same repository always produces the same block: a diagram that reorders itself makes
    # every run look like a change and nobody reads the diff after that.
    return @($edges | Sort-Object From, To -Unique)
}

<#
.SYNOPSIS
    A Mermaid block for a module group's layers.
.DESCRIPTION
    Node ids are the project names with dots replaced, because Mermaid reads a dot as a separator. The label
    keeps the real name. A group with no edges still renders its nodes - "nothing references anything" is a
    finding worth seeing, not an empty diagram.
#>
function ConvertTo-PaperDiagramMermaid {
    param(
        [Parameter(Mandatory = $true)] [AllowEmptyCollection()] [object[]] $Edges,
        [Parameter(Mandatory = $true)] [AllowEmptyCollection()] [string[]] $Nodes
    )

    $lines = New-Object System.Collections.ArrayList
    $null = $lines.Add('```mermaid')
    $null = $lines.Add('graph TD')
    foreach ($node in ($Nodes | Sort-Object)) {
        $null = $lines.Add(('  {0}["{1}"]' -f ($node -replace '[^A-Za-z0-9]', '_'), $node))
    }
    foreach ($edge in $Edges) {
        $null = $lines.Add(('  {0} --> {1}' -f ($edge.From -replace '[^A-Za-z0-9]', '_'), ($edge.To -replace '[^A-Za-z0-9]', '_')))
    }
    $null = $lines.Add('```')
    return ($lines -join "`n")
}

<#
.SYNOPSIS
    Puts a generated block between its markers, adding the pair when the document has none.
.DESCRIPTION
    Idempotent: running it twice on the same inputs returns the same text, which is what lets -Check compare
    rather than guess. Everything outside the markers is untouched - the document is the author's.
#>
function Update-PaperDiagramBlock {
    param(
        [Parameter(Mandatory = $true)] [AllowEmptyString()] [string] $Text,
        [Parameter(Mandatory = $true)] [string] $Id,
        [Parameter(Mandatory = $true)] [string] $Body
    )

    $open = [string]::Format($script:PaperDiagramOpen, $Id)
    $close = $script:PaperDiagramClose
    $block = $open + "`n" + $Body + "`n" + $close

    $pattern = [regex]::Escape($open) + '.*?' + [regex]::Escape($close)
    if ([regex]::IsMatch($Text, $pattern, 'Singleline')) {
        return [regex]::Replace($Text, $pattern, { param($m) $block }, 'Singleline')
    }

    if ([string]::IsNullOrEmpty($Text)) { return $block + "`n" }
    return $Text.TrimEnd() + "`n`n" + $block + "`n"
}

<#
.SYNOPSIS
    True when the document already carries exactly this block.
.DESCRIPTION
    The check a gate runs. False means the picture and the code disagree, and the picture is the one that is
    wrong - it is generated, the code is not.
#>
function Test-PaperDiagramBlock {
    param(
        [Parameter(Mandatory = $true)] [AllowEmptyString()] [string] $Text,
        [Parameter(Mandatory = $true)] [string] $Id,
        [Parameter(Mandatory = $true)] [string] $Body
    )

    return (Update-PaperDiagramBlock -Text $Text -Id $Id -Body $Body) -eq $Text
}

<#
.SYNOPSIS
    Every paper-diagram id a document carries, in order.
#>
function Get-PaperDiagramIds {
    param([Parameter(Mandatory = $true)] [AllowEmptyString()] [string] $Text)

    $ids = New-Object System.Collections.ArrayList
    foreach ($match in [regex]::Matches($Text, '<!-- paper-diagram: (.+?) -->')) {
        $null = $ids.Add($match.Groups[1].Value.Trim())
    }
    return @($ids)
}

<#
.SYNOPSIS
    The tasks of a plan, in order: Id, Group, Status, Lanes.
.DESCRIPTION
    A plan's task list is already data - "- [x] T5 [red][ui] ..." under a "### 3. Cua so" heading - so the
    picture of it is generated, never drawn. Status comes from the checkbox: x done, ~ in progress, blank
    waiting. A line that is not a task line is ignored, so prose between groups costs nothing.
#>
function Get-PaperPlanTasks {
    param([Parameter(Mandatory = $true)] [AllowEmptyString()] [string] $Text)

    $tasks = New-Object System.Collections.ArrayList
    $group = ''

    foreach ($line in ($Text -split "`r?`n")) {
        $heading = [regex]::Match($line, '^###\s+(?<name>.+?)\s*$')
        if ($heading.Success) { $group = $heading.Groups['name'].Value.Trim(); continue }

        $task = [regex]::Match($line, '^\s*-\s*\[(?<mark>[ x~X])\]\s*(?<id>T\d+)(?<rest>.*)$')
        if (-not $task.Success) { continue }

        $mark = $task.Groups['mark'].Value.ToLower()
        $status = switch ($mark) {
            'x' { 'done' }
            '~' { 'doing' }
            default { 'todo' }
        }

        $lanes = New-Object System.Collections.ArrayList
        foreach ($lane in [regex]::Matches($task.Groups['rest'].Value, '\[(?<lane>[a-z0-9\-]+)\]')) {
            $null = $lanes.Add($lane.Groups['lane'].Value)
        }

        $null = $tasks.Add([pscustomobject]@{
            Id     = $task.Groups['id'].Value
            Group  = $group
            Status = $status
            Lanes  = @($lanes)
        })
    }

    return @($tasks)
}

<#
.SYNOPSIS
    A Mermaid block for a plan's tasks: one subgraph per group, one node per task, coloured by status.
.DESCRIPTION
    Left to right, because a plan is read as an order. The node label carries the id and its lanes and
    nothing else - the description stays in the list above, where it can be read and copied. A picture that
    repeats the whole task list is a second place for the same fact to go stale.
#>
function ConvertTo-PaperTasksMermaid {
    param([Parameter(Mandatory = $true)] [AllowEmptyCollection()] [object[]] $Tasks)

    $lines = New-Object System.Collections.ArrayList
    $null = $lines.Add('```mermaid')
    $null = $lines.Add('graph LR')
    $null = $lines.Add('  classDef done fill:#d5f5d5,stroke:#2e7d32')
    $null = $lines.Add('  classDef doing fill:#fff3cd,stroke:#b8860b')
    $null = $lines.Add('  classDef todo fill:#f2f2f2,stroke:#888888')

    if ($Tasks.Count -eq 0) {
        $null = $lines.Add('  empty["no task in this plan"]')
        $null = $lines.Add('```')
        return ($lines -join "`n")
    }

    $index = 0
    foreach ($group in ($Tasks | Group-Object -Property Group)) {
        $index++
        $null = $lines.Add(('  subgraph g{0}["{1}"]' -f $index, (ConvertTo-PaperGroupLabel $group.Name)))
        foreach ($task in $group.Group) {
            # Parentheses, not the plan's own square brackets: a "[" inside a quoted Mermaid label
            # breaks the block on some renderers, and a broken block is invisible until somebody opens
            # the page on GitHub.
            $label = if ($task.Lanes.Count -gt 0) { '{0} ({1})' -f $task.Id, ($task.Lanes -join ', ') } else { $task.Id }
            $null = $lines.Add(('    {0}["{1}"]:::{2}' -f $task.Id, $label, $task.Status))
        }
        $null = $lines.Add('  end')
    }

    # One arrow per consecutive pair, in plan order: what a reader wants from a plan is the sequence.
    for ($i = 1; $i -lt $Tasks.Count; $i++) {
        $null = $lines.Add(('  {0} --> {1}' -f $Tasks[$i - 1].Id, $Tasks[$i].Id))
    }

    $null = $lines.Add('```')
    return ($lines -join "`n")
}

<#
.SYNOPSIS
    A group name Mermaid can carry: quotes and brackets in a heading break the block.
#>
function ConvertTo-PaperGroupLabel {
    param([AllowEmptyString()] [string] $Name)

    if ([string]::IsNullOrWhiteSpace($Name)) { return 'ungrouped' }
    return ($Name -replace '["\[\]]', '')
}
