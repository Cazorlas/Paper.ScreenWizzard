# Creates the machine's development code-signing certificate and trusts it, once per machine and user, so
# the host packs' build step can sign the add-in DLL. The MACHINE OWNER runs this, never an
# agent: adding a certificate to CurrentUser\Root is a Windows confirmation dialog meant for a person.
#   new-dev-signing-cert.ps1                      create (or reuse) CN=Paper Dev Signing and trust it
#   new-dev-signing-cert.ps1 -Subject "CN=Acme Dev" -DryRun
#                                                 read the three stores and print "would: ..." per step only
# A valid certificate of that name is reused: only the trust store that lacks it gets it. No admin rights.
# Exit: 0 done or nothing to do (or -DryRun), 1 a step failed.
# ASCII only, PowerShell 5.1: a .ps1 without a BOM is read as ANSI.
param(
    [string] $Subject = 'CN=Paper Dev Signing',
    [switch] $DryRun
)
# A shell started from PowerShell 7 hands down a PSModulePath whose PKI module does not load in 5.1.
$env:PSModulePath = [Environment]::GetEnvironmentVariable('PSModulePath', 'Machine')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'sign-addin-plan.ps1')
$X = 'System.Security.Cryptography.X509Certificates'

function Read-Store([string] $Name) {
    $store = New-Object "$X.X509Store"($Name, 'CurrentUser')
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
    try { return @($store.Certificates | ForEach-Object { $_ }) } finally { $store.Close() }
}
function Add-ToStore([string] $Name, $Certificate) {
    $public = New-Object "$X.X509Certificate2"(, $Certificate.RawData)
    $store = New-Object "$X.X509Store"($Name, 'CurrentUser')
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    try { $store.Add($public) } finally { $store.Close() }
}

try {
    $my = Read-Store 'My'
    $root = @(Read-Store 'Root' | ForEach-Object { $_.Thumbprint })
    $publisher = @(Read-Store 'TrustedPublisher' | ForEach-Object { $_.Thumbprint })
    $plan = Get-PaperCertSetupPlan -Subject $Subject -Certificates @($my | ForEach-Object { ConvertTo-PaperCertInfo $_ }) `
        -RootThumbprints $root -PublisherThumbprints $publisher -Now (Get-Date)
    if ($DryRun) {
        foreach ($l in @($plan.Lines)) { Write-Output "would: $l" }
        exit 0
    }
    if (-not $plan.Create -and -not $plan.AddRoot -and -not $plan.AddPublisher) {
        foreach ($l in @($plan.Lines)) { Write-Output $l }
        exit 0
    }
    if ($plan.Create) {
        $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -CertStoreLocation 'Cert:\CurrentUser\My' `
            -KeyAlgorithm RSA -KeyLength 2048 -KeyUsage DigitalSignature -HashAlgorithm SHA256 -NotAfter (Get-Date).AddYears(5)
    }
    else {
        $cert = @($my | Where-Object { $_.Thumbprint -eq $plan.Thumbprint })[0]
    }
    foreach ($l in @($plan.Lines)) { Write-Output $l }
    if ($plan.AddRoot) { Add-ToStore 'Root' $cert }
    if ($plan.AddPublisher) { Add-ToStore 'TrustedPublisher' $cert }
    Write-Output "done: $($cert.Thumbprint)"
    exit 0
}
catch {
    Write-Output ("failed: " + ("$($_.Exception.Message)" -replace '\s+', ' ').Trim())
    exit 1
}
