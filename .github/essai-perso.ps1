# Essai de la copie personnelle : un compte FICTIF est ajouté au paquet tout juste compilé (même outil
# que pour la vraie copie personnelle, faite hors de GitHub), puis le paquet est installé et lancé.
# Windows refuse un paquet mal formé ou mal signé : s'il s'installe, la méthode est bonne.
$ErrorActionPreference = 'Continue'
Set-Location (Split-Path $PSScriptRoot -Parent)
$dossier = 'build\captures'
New-Item -ItemType Directory -Force $dossier | Out-Null
$journal = Join-Path $dossier 'perso.log'
. (Join-Path $PSScriptRoot 'outils.ps1')

# osslsigncode (signature des paquets d'app) via MSYS2, présent sur les serveurs Windows de GitHub
& C:\msys64\usr\bin\bash.exe -lc 'pacman -Sy --noconfirm --needed mingw-w64-x86_64-osslsigncode' 2>&1 | Select-Object -Last 2 | ForEach-Object { Note "$_" }
$env:PATH = "C:\msys64\mingw64\bin;$env:PATH"
if (-not (Get-Command osslsigncode -ErrorAction SilentlyContinue)) { Note 'osslsigncode indisponible'; exit 1 }

$env:FLUXTV_SERVEUR = 'http://exemple.invalid:8080'
$env:FLUXTV_UTILISATEUR = 'essai'
$env:FLUXTV_MOTDEPASSE = 'essai'
python -I -B Ajouter-Compte.py build\paquet\FluxTV.msix build\FluxTV-essai-perso.msix 2>&1 | ForEach-Object { Note "$_" }
if (-not (Test-Path build\FluxTV-essai-perso.msix)) { Note 'Paquet personnel non créé'; exit 1 }

Get-Process -Name FluxTV -ErrorAction SilentlyContinue | Stop-Process -Force
Get-AppxPackage -Name Kevin22.FluxTVPlayer | Remove-AppxPackage
$deps = @(Get-ChildItem build\paquet\Dependances -File | ForEach-Object { $_.FullName })
try {
    Add-AppxPackage -Path build\FluxTV-essai-perso.msix -DependencyPath $deps -ErrorAction Stop
    Note 'Paquet personnel installé'
} catch {
    Note "Installation refusée : $($_.Exception.Message)"
    exit 1
}

# Lancement ordinaire : l'app doit s'ouvrir sur le compte intégré (serveur fictif : erreur de connexion attendue)
$paquet = Get-AppxPackage -Name Kevin22.FluxTVPlayer
$aumid = "$($paquet.PackageFamilyName)!App"
try {
    $pid2 = [Lanceur]::Lancer($aumid, '')
    Note "perso : lancée (processus $pid2)"
    Start-Sleep -Seconds 10
    Capturer 'perso'
    $etat = Join-Path $env:LOCALAPPDATA "Packages\$($paquet.PackageFamilyName)\LocalState\journal.txt"
    if (Test-Path $etat) { Get-Content $etat | ForEach-Object { Note "journal de l'app : $_" } }
} catch {
    Note "perso : lancement impossible : $($_.Exception.Message)"
}
Get-Process -Name FluxTV -ErrorAction SilentlyContinue | Stop-Process -Force
exit 0
