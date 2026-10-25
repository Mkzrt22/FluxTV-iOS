# Paquet pour le Microsoft Store : .msixupload (paquet groupé x64, non signé : le Store le signe
# lui-même). Il ne contient aucun compte : c'est la version publique. Résultat : build\store.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
New-Item -ItemType Directory -Force build\store | Out-Null

msbuild FluxTV.Xbox.csproj /restore /m /nologo /verbosity:minimal `
  /p:Configuration=Release /p:Platform=x64 `
  /p:AppxBundle=Always /p:AppxBundlePlatforms=x64 /p:UapAppxPackageBuildMode=StoreUpload `
  /p:AppxPackageSigningEnabled=false /p:GenerateAppxPackageOnBuild=true `
  /p:AppxPackageDir=build\StorePackages\ `
  "/flp:logfile=build\store.log;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "La compilation pour le Store a échoué" }

$upload = Get-ChildItem build\StorePackages -Recurse -Include *.msixupload, *.appxupload | Select-Object -First 1
if (-not $upload) { throw "Paquet .msixupload introuvable" }
Copy-Item $upload.FullName build\store\FluxTV-Player-Store.msixupload

# Vérifications : identité du Store, aucun compte intégré
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($upload.FullName)
$contenu = $zip.Entries | ForEach-Object { $_.FullName }
$zip.Dispose()
$infos = @("Paquet : $($upload.Name) ($([math]::Round($upload.Length / 1MB, 1)) Mo)", "Contenu : $($contenu -join ', ')")
$bundle = Get-ChildItem build\StorePackages -Recurse -Include *.msixbundle | Select-Object -First 1
if ($bundle) {
  $zip = [System.IO.Compression.ZipFile]::OpenRead($bundle.FullName)
  $msix = $zip.Entries | Where-Object { $_.FullName -like '*.msix' } | Select-Object -First 1
  $tmp = Join-Path $env:RUNNER_TEMP 'store-app.msix'
  [System.IO.Compression.ZipFileExtensions]::ExtractToFile($msix, $tmp, $true)
  $zip.Dispose()
  $app = [System.IO.Compression.ZipFile]::OpenRead($tmp)
  $manifeste = $app.Entries | Where-Object { $_.FullName -eq 'AppxManifest.xml' }
  $lecteur = New-Object System.IO.StreamReader($manifeste.Open())
  [xml]$xml = $lecteur.ReadToEnd()
  $lecteur.Dispose()
  $compte = $app.Entries | Where-Object { $_.FullName -match 'compte\.flux' }
  $app.Dispose()
  $id = $xml.Package.Identity
  $infos += "Identité : $($id.Name) · $($id.Publisher) · $($id.Version) · $($id.ProcessorArchitecture)"
  $infos += "Nom affiché : $($xml.Package.Properties.DisplayName) · éditeur : $($xml.Package.Properties.PublisherDisplayName)"
  $infos += "Compte intégré : $(if ($compte) { 'OUI (anomalie)' } else { 'non' })"
  if ($compte) { throw 'Le paquet du Store contient un compte' }
}
$infos | Set-Content build\store\store.txt -Encoding utf8
$infos | Write-Host
