# Signs an add-in DLL with the current user's code-signing certificate, so the host application does not ask
# to trust each new build. Run by a host pack's build/*.Sign.targets after CoreBuild:
#   sign-addin.ps1 -Path <dll> -Subject "CN=Paper Dev Signing" -HostName <name the host goes by>
# Prints exactly one line and always exits 0: a machine with no certificate builds green as before. A line
# starting with "warning:" becomes a build warning. No timestamp: a machine with no network still signs.
# ASCII only, PowerShell 5.1: a .ps1 without a BOM is read as ANSI.
param(
    [string] $Path,
    [string] $Subject = 'CN=Paper Dev Signing',
    [string] $HostName = 'the host'
)
# A build started from PowerShell 7 hands down a PSModulePath whose Security module does not load in 5.1.
$env:PSModulePath = [Environment]::GetEnvironmentVariable('PSModulePath', 'Machine')
$line = $null
try {
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot 'sign-addin-plan.ps1')
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        $line = Format-PaperSignResult -File $Path -Subject $Subject -ErrorText 'file not found' -HostName $HostName
    }
    else {
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store('My', 'CurrentUser')
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        try { $certs = @($store.Certificates | ForEach-Object { $_ }) } finally { $store.Close() }
        $decision = Get-PaperSignDecision -Subject $Subject -Certificates @($certs | ForEach-Object { ConvertTo-PaperCertInfo $_ }) -Now (Get-Date) -HostName $HostName
        if ($decision.Outcome -ne 'sign') { $line = $decision.Line }
        else {
            $cert = @($certs | Where-Object { $_.Thumbprint -eq $decision.Thumbprint })[0]
            $sig = Set-AuthenticodeSignature -FilePath $Path -Certificate $cert -HashAlgorithm SHA256
            $line = Format-PaperSignResult -File $Path -Subject $Subject -Status "$($sig.Status)" -HostName $HostName
        }
    }
}
catch {
    $text = ("$($_.Exception.Message)" -replace '\s+', ' ').Trim()
    if (Get-Command Format-PaperSignResult -ErrorAction SilentlyContinue) { $line = Format-PaperSignResult -File $Path -Subject $Subject -ErrorText $text -HostName $HostName }
    else { $line = "warning: signing $Path failed: $text - $HostName will ask to trust this build" }
}
Write-Output $line
exit 0
