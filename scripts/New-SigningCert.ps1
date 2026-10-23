<#
.SYNOPSIS
  Creates the self-signed signing certificate for sideload releases.

.DESCRIPTION
  The certificate subject MUST match the Publisher in
  DirectStreamWidget\Package.appxmanifest exactly: CN=VetTV Studios.

  Outputs:
    VetTVStudios-Signing.pfx  - private key; upload its base64 to the GitHub
                                secret SIGNING_CERT_PFX_BASE64 and its password
                                to SIGNING_CERT_PASSWORD. Keep the original safe.
    VetTVStudios-Signing.cer  - public cert end users trust once before
                                installing the .msix (attach to releases).

.EXAMPLE
  .\New-SigningCert.ps1 -Password "a-strong-password"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Password
)

$subject = "CN=VetTV Studios"
$cert = New-SelfSignedCertificate -Type Custom -Subject $subject `
    -KeyUsage DigitalSignature -FriendlyName "VetTV Studios (Direct Stream signing)" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$secure = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath ".\VetTVStudios-Signing.pfx" -Password $secure | Out-Null
Export-Certificate -Cert $cert -FilePath ".\VetTVStudios-Signing.cer" | Out-Null

Write-Host "Created VetTVStudios-Signing.pfx and VetTVStudios-Signing.cer"
Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host ""
Write-Host "For GitHub Actions, set the secret SIGNING_CERT_PFX_BASE64 to:"
Write-Host '  [Convert]::ToBase64String([IO.File]::ReadAllBytes(".\VetTVStudios-Signing.pfx"))'
Write-Host "and SIGNING_CERT_PASSWORD to the password you just used."
