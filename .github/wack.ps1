# Kit de certification des applications Windows (le même contrôle que le Store) sur l'app installée.
# Résultat : build\wack\rapport.xml et build\wack\wack.txt (résumé, tests en échec).
$ErrorActionPreference = 'Continue'
Set-Location (Split-Path $PSScriptRoot -Parent)
$dossier = 'build\wack'
New-Item -ItemType Directory -Force $dossier | Out-Null
$resume = Join-Path $dossier 'wack.txt'
function Note([string]$t) { $t | Tee-Object -FilePath $resume -Append | Write-Host }

$appcert = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe' -ErrorAction SilentlyContinue |
  Select-Object -First 1
if (-not $appcert) { Note 'Kit de certification absent de ce serveur'; exit 0 }
$paquet = Get-AppxPackage -Name Kevin22.FluxTVPlayer
if (-not $paquet) { Note 'App non installée : pas de contrôle'; exit 0 }
Get-Process -Name FluxTV -ErrorAction SilentlyContinue | Stop-Process -Force

& $appcert.FullName reset | Out-Null
$rapport = Join-Path (Resolve-Path $dossier) 'rapport.xml'
& $appcert.FullName test -packagefullname $paquet.PackageFullName -reportoutputpath $rapport 2>&1 | Out-Null
if (-not (Test-Path $rapport)) { Note "Pas de rapport (code $LASTEXITCODE)"; exit 0 }

[xml]$xml = Get-Content $rapport -Raw
Note "Résultat global : $($xml.REPORT.OVERALL_RESULT)"
foreach ($test in $xml.SelectNodes('//TEST')) {
  $etat = $test.RESULT.'#cdata-section'
  if (-not $etat) { $etat = $test.RESULT.InnerText }
  if ($etat -notmatch 'PASS') {
    Note "[$etat] $($test.NAME) : $($test.DESCRIPTION)"
    foreach ($m in $test.MESSAGES.MESSAGE) { Note "    $($m.TEXT)" }
  }
}
exit 0
