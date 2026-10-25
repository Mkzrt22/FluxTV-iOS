# Captures d'écran de l'app installée sur le serveur Windows de GitHub : l'app est lancée en mode
# « essai » (démonstration, rien n'est enregistré) sur chaque écran, puis l'écran est photographié.
# Résultat : build\captures\*.jpg et captures.log.
$ErrorActionPreference = 'Continue'
Set-Location (Split-Path $PSScriptRoot -Parent)
$dossier = 'build\captures'
New-Item -ItemType Directory -Force $dossier | Out-Null
$journal = Join-Path $dossier 'captures.log'
. (Join-Path $PSScriptRoot 'outils.ps1')

$uac = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -ErrorAction SilentlyContinue).EnableLUA
Note "UAC (EnableLUA) : $uac"
try {
    Set-DisplayResolution -Width 1920 -Height 1080 -Force -ErrorAction Stop
    Note 'Écran passé en 1920 × 1080'
} catch {
    Note "Résolution inchangée : $($_.Exception.Message)"
}
$ecran = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Note "Écran : $($ecran.Width) × $($ecran.Height)"

$paquet = Get-AppxPackage -Name FluxTV
if (-not $paquet) { Note 'App non installée : pas de captures'; exit 0 }
$aumid = "$($paquet.PackageFamilyName)!App"
Note "App : $aumid"

# Écran, attente (secondes)
$ecrans = @(
    @('setup', 6), @('home', 7), @('rail', 7), @('row', 7), @('live', 8), @('movies', 8), @('series', 8),
    @('movie', 8), @('seriesdetail', 8), @('search', 9), @('settings', 7), @('play-live', 13), @('play-channels', 13),
    @('play-movie', 13), @('play-options', 13)
)
foreach ($e in $ecrans) {
    $nom = $e[0]
    Get-Process -Name FluxTV -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    try {
        $pid2 = [Lanceur]::Lancer($aumid, "test:$nom")
        Note "$nom : lancée (processus $pid2)"
    } catch {
        Note "$nom : lancement impossible : $($_.Exception.Message)"
        continue
    }
    Start-Sleep -Seconds $e[1]
    if (-not (Get-Process -Id $pid2 -ErrorAction SilentlyContinue)) { Note "$nom : l'app s'est fermée (plantage ?)" }
    Capturer $nom
}
Get-Process -Name FluxTV -ErrorAction SilentlyContinue | Stop-Process -Force

# Journal d'erreurs de l'app (mode essai : démonstration, aucun identifiant)
$etat = Join-Path $env:LOCALAPPDATA "Packages\$($paquet.PackageFamilyName)\LocalState\journal.txt"
if (Test-Path $etat) { Copy-Item $etat (Join-Path $dossier 'journal-app.txt') }
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddMinutes(-15) } -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -match 'FluxTV' -and $_.LevelDisplayName -match 'Erreur|Error' } | Select-Object -First 10 |
    ForEach-Object { Note "Événement Windows : $($_.Message)" }
exit 0
