# The one way the kit runs a command line a project declares (a verb of .claude/paper.profile.json): the
# flow runner, the worktree baseline and the Stop hook all come through here.
#
# Why one way: the three used to start the child three different ways, and each way had its own bug.
#   - `powershell -Command <line>` hands the line over as process ARGUMENTS, and PowerShell 5.1 passes an
#     argument to a native process without escaping the double quotes inside it, so the child re-splits it.
#     Measured 2026-09-18: `Write-Output "x y: Total: 4"` printed x and y on separate lines, and
#     `& "C:\a b\build step.ps1"` looked for a program called C:\a.
#   - `-EncodedCommand <base64>` keeps the line whole, but with stderr redirected it writes every error and
#     progress record to stderr as CLIXML (`#< CLIXML <Objs ...>`), so an error line is no longer a line.
#     Measured 2026-09-18.
#   - the base64 is 2.7 characters per character of the line, and a process command line holds 32767: a
#     line past ~12,200 characters failed with "The filename or extension is too long".
# So: the line travels as base64 of its UTF-16 text inside a fixed -Command that decodes and dot-sources it
# (no double quote, no space inside the base64, so nothing to re-split, and errors stay plain text). Past
# 12000 characters it is written to a temporary .ps1 (UTF-8 with a BOM, so 5.1 does not read it as ANSI),
# run with -File and deleted afterwards. Both get one trailing line, `if (-not $?) { exit 1 }`, which is
# the exit rule of -Command itself: a line whose last statement failed exits 1 (measured: `cmd /c exit 3`
# as the last statement exits 1 under -EncodedCommand, and 0 under -File without it).
#
# Invoke-PaperProjectCommand -Directory <dir> -Command <line> [-Echo] [-TimeoutSeconds <n>] [-OnStarted <sb>]
#   -Echo            print every line as it arrives: stdout lines to stdout, stderr lines to stderr
#   -TimeoutSeconds  0 = no limit; past it the child is killed and Code is $null
#   -OnStarted       called once with the pid of the child, right after it started, before any output is
#                    read - so a caller can record who runs the command while it runs
# Returns Code (the child's exit code, $null when it was stopped), Lines (stdout and stderr in the order they
# arrived, blank lines kept) and TimedOut.
#
# Dot-sourced; declares no param() block at script level. ASCII only: PowerShell 5.1 reads a .ps1 without a
# BOM as ANSI.

# Past this many characters the line goes through a temporary file instead of the command line.
$script:PaperProjectCommandMaxInline = 12000

function Invoke-PaperProjectCommand {
    param(
        [Parameter(Mandatory = $true)][string] $Directory,
        [Parameter(Mandatory = $true)][string] $Command,
        [switch] $Echo,
        [int] $TimeoutSeconds = 0,
        [scriptblock] $OnStarted
    )

    $tempScript = $null
    $body = $Command + "`r`nif (-not `$?) { exit 1 }"
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'powershell.exe'
    if ($Command.Length -le $script:PaperProjectCommandMaxInline) {
        $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($body))
        $psi.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command . ([scriptblock]::Create([Text.Encoding]::Unicode.GetString([Convert]::FromBase64String(''' + $encoded + '''))))'
    }
    else {
        $tempScript = Join-Path ([System.IO.Path]::GetTempPath()) ('paperflow-command-' + [guid]::NewGuid().ToString('N') + '.ps1')
        [System.IO.File]::WriteAllText($tempScript, $body, (New-Object System.Text.UTF8Encoding $true))
        $psi.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $tempScript + '"'
    }
    $psi.WorkingDirectory = $Directory
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true

    $lines = New-Object System.Collections.Generic.List[string]
    $timedOut = $false
    $p = $null
    try {
        $p = [System.Diagnostics.Process]::Start($psi)
        if ($null -ne $OnStarted) {
            try { & $OnStarted $p.Id } catch { }
        }
        $p.StandardInput.Close()

        # Both streams read on this thread, a line at a time, so a line is echoed when it arrives and the
        # order of stdout and stderr is kept. Event handlers would run script on a thread with no runspace.
        $outReader = $p.StandardOutput
        $errReader = $p.StandardError
        $outTask = $outReader.ReadLineAsync()
        $errTask = $errReader.ReadLineAsync()
        $deadline = if ($TimeoutSeconds -gt 0) { (Get-Date).AddSeconds($TimeoutSeconds) } else { [datetime]::MaxValue }
        $drainUntil = $null
        while ($null -ne $outTask -or $null -ne $errTask) {
            $pending = @($outTask, $errTask | Where-Object { $null -ne $_ })
            [void] [System.Threading.Tasks.Task]::WaitAny([System.Threading.Tasks.Task[]] $pending, 200)
            if ($null -ne $outTask -and $outTask.IsCompleted) {
                $line = $outTask.Result
                if ($null -eq $line) { $outTask = $null }
                else {
                    $lines.Add($line)
                    if ($Echo) { [Console]::Out.WriteLine($line) }
                    $outTask = $outReader.ReadLineAsync()
                }
            }
            if ($null -ne $errTask -and $errTask.IsCompleted) {
                $line = $errTask.Result
                if ($null -eq $line) { $errTask = $null }
                else {
                    $lines.Add($line)
                    if ($Echo) { [Console]::Error.WriteLine($line) }
                    $errTask = $errReader.ReadLineAsync()
                }
            }
            if ((Get-Date) -gt $deadline) {
                $timedOut = $true
                try { $p.Kill() } catch { }
                break
            }
            # A process the command left behind can keep the pipes open after the command itself has
            # ended; its output is not the command's, and waiting for it could last for ever.
            if ($p.HasExited) {
                if ($null -eq $drainUntil) { $drainUntil = (Get-Date).AddSeconds(3) }
                elseif ((Get-Date) -gt $drainUntil) { break }
            }
        }

        if ($timedOut) {
            return [pscustomobject]@{ Code = $null; Lines = $lines.ToArray(); TimedOut = $true }
        }
        $p.WaitForExit()
        return [pscustomobject]@{ Code = $p.ExitCode; Lines = $lines.ToArray(); TimedOut = $false }
    }
    finally {
        if ($null -ne $p) { $p.Dispose() }
        if ($tempScript) { Remove-Item -LiteralPath $tempScript -Force -ErrorAction SilentlyContinue }
    }
}
