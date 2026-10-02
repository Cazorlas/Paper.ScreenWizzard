# The pure half of signing an add-in DLL: which certificate signs, the one line the build shows, and what the
# machine owner's certificate script would do. No store, no disk, no clock - the kit's signing tests
# dot-source this file. One shared copy for every host pack that signs; the host's name is a parameter.
# The I/O halves are sign-addin.ps1 and new-dev-signing-cert.ps1 next to it.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperCertCommand = '.claude/paperflow/new-dev-signing-cert.ps1'

# The certificates that can sign for this subject: same name (trimmed, ignoring case), private key present,
# code-signing use (EKU 1.3.6.1.5.5.7.3.3). Each certificate is @{ Subject; Thumbprint; NotAfter; HasPrivateKey; CodeSigning }.
function Get-PaperSignCandidates {
    param([string] $Subject, $Certificates)
    $want = "$Subject".Trim()
    return @(@($Certificates) | Where-Object {
            $null -ne $_ -and [string]::Equals("$($_.Subject)".Trim(), $want, [StringComparison]::OrdinalIgnoreCase) -and
            [bool] $_.HasPrivateKey -and [bool] $_.CodeSigning
        } | Sort-Object { [datetime] $_.NotAfter } -Descending)
}

# @{ Outcome = sign / nocert / expired; Thumbprint; Line }. Several valid ones: the one that expires last.
function Get-PaperSignDecision {
    param([string] $Subject, $Certificates, [datetime] $Now, [string] $HostName = 'the host')
    $all = @(Get-PaperSignCandidates -Subject $Subject -Certificates $Certificates)
    $valid = @($all | Where-Object { [datetime] $_.NotAfter -gt $Now })
    if ($valid.Count -gt 0) {
        return [pscustomobject]@{ Outcome = 'sign'; Thumbprint = "$($valid[0].Thumbprint)"; Line = $null }
    }
    if ($all.Count -gt 0) {
        $when = ([datetime] $all[0].NotAfter).ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
        return [pscustomobject]@{
            Outcome = 'expired'; Thumbprint = "$($all[0].Thumbprint)"
            Line = "not signed: the $Subject certificate in CurrentUser\My expired on $when - run $($script:PaperCertCommand) again; $HostName will ask to trust this build"
        }
    }
    return [pscustomobject]@{
        Outcome = 'nocert'; Thumbprint = $null
        Line = "not signed: no $Subject code-signing certificate in CurrentUser\My - $HostName will ask to trust this build (run $($script:PaperCertCommand) once on this machine)"
    }
}

# The one line sign-addin.ps1 prints for a file it tried to sign: -Status from Set-AuthenticodeSignature, or
# -ErrorText when it failed. A line starting with "warning:" becomes a build warning.
function Format-PaperSignResult {
    param([string] $File, [string] $Subject, [string] $Status, [string] $ErrorText, [string] $HostName = 'the host')
    $leaf = if ($File) { [System.IO.Path]::GetFileName($File) } else { '<no file>' }
    if ($ErrorText) { return "warning: signing $leaf failed: $ErrorText - $HostName will ask to trust this build" }
    if ($Status -eq 'Valid') { return "signed: $leaf with $Subject - $HostName will not ask to trust this build" }
    return "warning: signing $leaf returned $Status - $HostName will ask to trust this build"
}

# What new-dev-signing-cert.ps1 does: @{ Create; Thumbprint; AddRoot; AddPublisher; Lines }. A valid certificate
# is reused and only added to the trust store that lacks it; none valid: create one and add it to both.
function Get-PaperCertSetupPlan {
    param([string] $Subject, $Certificates, [string[]] $RootThumbprints, [string[]] $PublisherThumbprints, [datetime] $Now)
    $valid = @(@(Get-PaperSignCandidates -Subject $Subject -Certificates $Certificates) | Where-Object { [datetime] $_.NotAfter -gt $Now })
    $lines = New-Object System.Collections.Generic.List[string]
    if ($valid.Count -eq 0) {
        $lines.Add("create a code-signing certificate $Subject in CurrentUser\My, valid 5 years")
        $lines.Add('add the new certificate to CurrentUser\Root (Windows asks you to confirm once)')
        $lines.Add('add the new certificate to CurrentUser\TrustedPublisher')
        return [pscustomobject]@{ Create = $true; Thumbprint = $null; AddRoot = $true; AddPublisher = $true; Lines = $lines.ToArray() }
    }
    $thumb = "$($valid[0].Thumbprint)"
    $inRoot = @(@($RootThumbprints) | Where-Object { [string]::Equals("$_", $thumb, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
    $inPublisher = @(@($PublisherThumbprints) | Where-Object { [string]::Equals("$_", $thumb, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
    if (-not $inRoot) { $lines.Add("add $thumb to CurrentUser\Root (Windows asks you to confirm once)") }
    if (-not $inPublisher) { $lines.Add("add $thumb to CurrentUser\TrustedPublisher") }
    if ($lines.Count -eq 0) {
        $until = ([datetime] $valid[0].NotAfter).ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
        $lines.Add("nothing to do: $Subject $thumb is valid until $until and trusted")
    }
    return [pscustomobject]@{ Create = $false; Thumbprint = $thumb; AddRoot = (-not $inRoot); AddPublisher = (-not $inPublisher); Lines = $lines.ToArray() }
}

# One certificate of an X509 store as the functions above read it.
function ConvertTo-PaperCertInfo {
    param($Certificate)
    $code = $false
    foreach ($ext in @($Certificate.Extensions)) {
        if ($ext -is [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]) {
            foreach ($u in $ext.EnhancedKeyUsages) { if ($u.Value -eq '1.3.6.1.5.5.7.3.3') { $code = $true } }
        }
    }
    return @{ Subject = $Certificate.Subject; Thumbprint = $Certificate.Thumbprint; NotAfter = $Certificate.NotAfter; HasPrivateKey = $Certificate.HasPrivateKey; CodeSigning = $code }
}
