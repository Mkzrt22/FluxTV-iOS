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

$paquet = Get-AppxPackage -Name Kevin22.FluxTVPlayer
if (-not $paquet) { Note 'App non installée : pas de captures'; exit 0 }
$aumid = "$($paquet.PackageFamilyName)!App"
Note "App : $aumid"

# Écran, attente (secondes). « @en » : le même écran en anglais (fichier « -en »)
$ecrans = @(
    @('intro', 4), @('intro-sortie', 0), @('demarrage', 11), @('setup', 6), @('home', 7), @('rail', 7), @('row', 7), @('live', 8), @('movies', 8), @('series', 8),
    @('movie', 8), @('seriesdetail', 8), @('search', 9), @('settings', 7), @('credits', 8), @('play-live', 13), @('play-channels', 13),
    @('play-movie', 13), @('play-options', 13),
    @('language', 5), @('intro@en', 4), @('setup@en', 6), @('home@en', 7), @('rail@en', 7), @('live@en', 8), @('movies@en', 8), @('series@en', 8),
    @('movie@en', 8), @('seriesdetail@en', 8), @('search@en', 9), @('settings@en', 7), @('play-channels@en', 13), @('play-movie@en', 13),
    @('play-options@en', 13)
)
# Écrans repris sur la fiche du Store, en français et en anglais (démonstration uniquement : contenus libres de droits)
$pourLeStore = @('intro', 'home', 'live', 'movies', 'movie', 'seriesdetail', 'search', 'play-channels', 'play-movie')
$pourLeStore += $pourLeStore | ForEach-Object { "$_@en" }
foreach ($e in $ecrans) {
    $nom = $e[0]
    $fichier = $nom -replace '@', '-'
    Get-Process -Name FluxTV -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    try {
        # « demarrage » : lancement normal (intro, puis choix de la source)
        $arguments = if ($nom -eq 'demarrage') { '' } else { "test:$nom" }
        $pid2 = [Lanceur]::Lancer($aumid, $arguments)
        Note "$nom : lancée (processus $pid2)"
    } catch {
        Note "$nom : lancement impossible : $($_.Exception.Message)"
        continue
    }
    if ($nom -eq 'demarrage') {
        # Premier lancement : choix de la langue (celle du serveur proposée), validé avec Entrée comme avec Ⓐ
        Start-Sleep -Seconds 5
        Capturer 'premier-lancement'
        [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
        Note 'demarrage : langue validée (Entrée)'
    }
    if ($nom -eq 'intro-sortie') {
        # Fin de l'animation en rafale : de 2,6 s à environ 6 s après le lancement
        Start-Sleep -Milliseconds 2600
        for ($i = 1; $i -le 40; $i++) {
            Capturer ('intro-sortie-{0:D2}' -f $i)
            Start-Sleep -Milliseconds 30
        }
        continue
    }
    Start-Sleep -Seconds $e[1]
    if (-not (Get-Process -Id $pid2 -ErrorAction SilentlyContinue)) { Note "$nom : l'app s'est fermée (plantage ?)" }
    Capturer $fichier -Png:($pourLeStore -contains $nom)
}
Get-Process -Name FluxTV -ErrorAction SilentlyContinue | Stop-Process -Force

# Journal d'erreurs de l'app (mode essai : démonstration, aucun identifiant)
$etat = Join-Path $env:LOCALAPPDATA "Packages\$($paquet.PackageFamilyName)\LocalState\journal.txt"
if (Test-Path $etat) { Copy-Item $etat (Join-Path $dossier 'journal-app.txt') }
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddMinutes(-15) } -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -match 'FluxTV' -and $_.LevelDisplayName -match 'Erreur|Error' } | Select-Object -First 10 |
    ForEach-Object { Note "Événement Windows : $($_.Message)" }
exit 0
