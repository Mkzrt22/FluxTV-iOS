# GitHub : publie le résultat de la compilation Xbox.
#  - branche « ci-xbox » : résumé, erreurs, journaux et le paquet (lisible avec un simple git fetch) ;
#  - page « Releases » : « xbox-test » avec FluxTV-Xbox.zip.
param([string]$Compilation, [string]$Installation)
Set-Location (Split-Path $PSScriptRoot -Parent)
$racine = Get-Location
$out = Join-Path $env:RUNNER_TEMP 'ci-xbox'
Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $out | Out-Null

@(
  "commit: $env:GITHUB_SHA",
  "compilation: $Compilation",
  "installation: $Installation",
  "date: $((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))"
) | Set-Content -Path "$out\resultat.txt" -Encoding utf8

if (Test-Path build\compilation.log) {
  $lignes = Get-Content build\compilation.log
  $lignes | Select-String -Pattern ': error |error [A-Z]+[0-9]+ *:|: fatal error' |
    ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique | Set-Content "$out\erreurs.txt" -Encoding utf8
  $lignes | Select-String -Pattern ': warning ' |
    ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique | Select-Object -First 300 |
    Set-Content "$out\avertissements.txt" -Encoding utf8
  Compress-Archive -Path build\compilation.log -DestinationPath "$out\compilation.zip" -Force
}
foreach ($f in 'installation.log', 'FluxTV-Xbox.zip') {
  if (Test-Path "build\$f") { Copy-Item "build\$f" "$out\$f" }
}

Push-Location $out
git init -q -b ci-xbox
git add -A
git -c user.name='FluxTV CI' -c user.email='ci@fluxtv.invalid' commit -qm "Xbox $env:GITHUB_SHA"
git push -qf "https://x-access-token:$env:GH_TOKEN@github.com/$env:GITHUB_REPOSITORY.git" ci-xbox
Pop-Location

if (Test-Path build\FluxTV-Xbox.zip) {
  $notes = "Compilée le $((Get-Date).ToUniversalTime().ToString('dd/MM/yyyy HH:mm')) (UTC), version $($env:GITHUB_SHA.Substring(0, 7)). Installation : Device Portal de la Xbox (voir LISEZMOI)."
  gh release view xbox-test -R $env:GITHUB_REPOSITORY *> $null
  if ($LASTEXITCODE -ne 0) {
    gh release create xbox-test -R $env:GITHUB_REPOSITORY --prerelease --target xbox --title 'Xbox — version de test' --notes $notes
  }
  gh release upload xbox-test build\FluxTV-Xbox.zip -R $env:GITHUB_REPOSITORY --clobber
  gh release edit xbox-test -R $env:GITHUB_REPOSITORY --notes $notes *> $null
}
exit 0
