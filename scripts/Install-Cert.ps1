#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [string]$CertificatePath = (Join-Path $PSScriptRoot "..\VetTVStudios-Signing.cer")
)

$ErrorActionPreference = "Stop"
$certificatePath = (Resolve-Path -LiteralPath $CertificatePath).Path
$certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certificatePath)

if ($certificate.Subject -ne "CN=VetTV Studios")
{
    throw "Unexpected certificate subject '$($certificate.Subject)'. Expected 'CN=VetTV Studios'."
}

$storePath = "Cert:\LocalMachine\TrustedPeople"
$existing = Get-ChildItem -Path $storePath |
    Where-Object { $_.Thumbprint -eq $certificate.Thumbprint }

if (-not $existing)
{
    Import-Certificate -FilePath $certificatePath -CertStoreLocation $storePath | Out-Null
}

Write-Host "Trusted VetTV Studios package certificate in Local Machine\Trusted People."
Write-Host "Thumbprint: $($certificate.Thumbprint)"
