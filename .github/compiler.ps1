# Compilation de FluxTV pour Xbox : paquet .msix signé, dépendances et certificat dans build\paquet,
# et le tout dans build\FluxTV-Xbox.zip.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
New-Item -ItemType Directory -Force build | Out-Null

# Certificat de test : la signature du paquet le cherche dans le magasin de l'utilisateur
$motDePasse = ConvertTo-SecureString 'fluxtv' -AsPlainText -Force
Import-PfxCertificate -FilePath Signature\FluxTV-test.pfx -CertStoreLocation Cert:\CurrentUser\My -Password $motDePasse | Out-Null

msbuild FluxTV.Xbox.csproj /restore /m /nologo /verbosity:minimal `
  /p:Configuration=Release /p:Platform=x64 `
  /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly /p:GenerateAppxPackageOnBuild=true `
  /p:AppxPackageDir=build\AppPackages\ `
  "/flp:logfile=build\compilation.log;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "La compilation a échoué" }

$paquet = 'build\paquet'
New-Item -ItemType Directory -Force "$paquet\Dependances" | Out-Null
$app = Get-ChildItem build\AppPackages -Recurse -Include *.msix, *.appx |
  Where-Object { $_.DirectoryName -notmatch 'Dependencies' } | Select-Object -First 1
if (-not $app) { throw "Paquet de l'app introuvable" }
Copy-Item $app.FullName "$paquet\FluxTV.msix"
Get-ChildItem build\AppPackages -Recurse -Include *.msix, *.appx |
  Where-Object { $_.DirectoryName -match 'Dependencies\\x64' } |
  Copy-Item -Destination "$paquet\Dependances"
Copy-Item Signature\FluxTV-test.cer "$paquet\FluxTV-certificat.cer"
Copy-Item LISEZMOI.txt "$paquet\LISEZMOI.txt"
Compress-Archive -Path "$paquet\*" -DestinationPath build\FluxTV-Xbox.zip -Force
Get-ChildItem $paquet -Recurse -File | Format-Table Name, Length -AutoSize | Out-String | Write-Host
