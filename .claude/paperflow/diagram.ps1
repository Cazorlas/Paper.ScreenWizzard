<#
.SYNOPSIS
    Writes or checks a generated Mermaid diagram inside a Paper document.

.DESCRIPTION
    A diagram nobody generates is a diagram that starts lying the week after it is drawn. This reads the
    real ProjectReference graph of one module group and writes it between markers in the document you name;
    -Check writes nothing and fails when the picture and the code disagree.

    The decisions are in diagram-plan.ps1, which is pure and covered by .claude/paperflow/../../tests.

.EXAMPLE
    .\diagram.ps1 -Path "docs/decisions/0009-module-layers.md" -Module Paper.Structural
    .\diagram.ps1 -Path "docs/decisions/0009-module-layers.md" -Module Paper.Structural -Check

.NOTES
    Exit codes: 0 written, or -Check found the block current. 1 -Check found it stale, or nothing was
    written because something is wrong. ASCII with a UTF-8 BOM, PowerShell 5.1 syntax.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Path,
    [Parameter(Mandatory = $true)] [string] $Module,
    [string] $SourceRoot,
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'diagram-plan.ps1')

try {
    if (-not (Test-Path -LiteralPath $Path)) {
        [Console]::Error.WriteLine("diagram: no document at $Path")
        exit 1
    }

    # .NET resolves a relative path against the PROCESS directory, which in PowerShell is not Get-Location.
    # Both are turned into full paths here, or the script reads a file next to itself and says it is missing.
    $Path = (Resolve-Path -LiteralPath $Path).ProviderPath
    if (-not $SourceRoot) { $SourceRoot = (Get-Location).Path }
    if (-not (Test-Path -LiteralPath $SourceRoot)) {
        [Console]::Error.WriteLine("diagram: no source root at $SourceRoot")
        exit 1
    }
    $SourceRoot = (Resolve-Path -LiteralPath $SourceRoot).ProviderPath
    $projects = @{}
    foreach ($csproj in (Get-ChildItem -LiteralPath $SourceRoot -Filter '*.csproj' -Recurse -File -ErrorAction SilentlyContinue)) {
        if ($csproj.FullName -match '[\\/](obj|bin)[\\/]') { continue }
        $projects[$csproj.BaseName] = [System.IO.File]::ReadAllText($csproj.FullName)
    }

    $members = @($projects.Keys | Where-Object {
        $_ -eq $Module -or $_.StartsWith($Module + '.', [System.StringComparison]::OrdinalIgnoreCase)
    })
    if ($members.Count -eq 0) {
        [Console]::Error.WriteLine("diagram: no project named $Module or $Module.* under $SourceRoot")
        exit 1
    }

    $edges = Get-PaperDiagramEdges -Projects $projects -Module $Module
    $body = ConvertTo-PaperDiagramMermaid -Edges $edges -Nodes $members
    $id = "layers $Module"

    $text = [System.IO.File]::ReadAllText($Path)

    if ($Check) {
        if (Test-PaperDiagramBlock -Text $text -Id $id -Body $body) {
            Write-Output ("diagram: '{0}' is current in {1} ({2} projects, {3} edges)" -f $id, $Path, $members.Count, $edges.Count)
            exit 0
        }

        [Console]::Error.WriteLine("diagram: '$id' in $Path no longer matches the projects. Run without -Check to regenerate.")
        exit 1
    }

    $updated = Update-PaperDiagramBlock -Text $text -Id $id -Body $body
    if ($updated -eq $text) {
        Write-Output ("diagram: '{0}' already current in {1}" -f $id, $Path)
        exit 0
    }

    [System.IO.File]::WriteAllText($Path, $updated, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output ("diagram: wrote '{0}' into {1} ({2} projects, {3} edges)" -f $id, $Path, $members.Count, $edges.Count)
    exit 0
}
catch {
    [Console]::Error.WriteLine("diagram: $($_.Exception.Message)")
    exit 1
}
