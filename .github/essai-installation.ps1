# Essai : installe le paquet sur le serveur Windows de GitHub. S'il s'installe, le paquet, sa
# signature et ses dépendances sont valides (même installation que sur la Xbox).
# $args[0] : paquet à essayer (par défaut build\paquet\FluxTV.msix).
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$msix = if ($args.Count -gt 0) { $args[0] } else { 'build\paquet\FluxTV.msix' }

$cle = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
New-Item -Path $cle -Force | Out-Null
Set-ItemProperty -Path $cle -Name AllowAllTrustedApps -Value 1 -Type DWord
Set-ItemProperty -Path $cle -Name AllowDevelopmentWithoutDevLicense -Value 1 -Type DWord
Import-Certificate -FilePath Signature\FluxTV-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

$deps = @(Get-ChildItem build\paquet\Dependances -File | ForEach-Object { $_.FullName })
Get-AppxPackage -Name FluxTV | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Path $msix -DependencyPath $deps
Get-AppxPackage -Name FluxTV | Format-List Name, Version, Publisher, Architecture, Status, InstallLocation |
  Out-String | Tee-Object -FilePath build\installation.log
