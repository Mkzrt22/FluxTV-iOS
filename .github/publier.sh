#!/bin/bash
# GitHub : publie le résultat de la compilation.
#  - branche « ci » : résumé, erreurs, journaux et l'app iPhone (lisible avec un simple git fetch) ;
#  - page « Releases » : « Version de test » avec les .ipa iPhone et Apple TV.
# $1 résultat de la fabrication des .ipa, $2 résultat des tests du cœur.
set -uo pipefail
cd "$(dirname "$0")/.."

out="$(mktemp -d)"
{
  echo "commit: $GITHUB_SHA"
  echo "ipa: $1"
  echo "tests: $2"
  echo "date: $(date -u +%FT%TZ)"
  xcodebuild -version | head -1
  ls -l ipa 2>/dev/null
} > "$out/resultat.txt"

for f in build/*.log; do
  [ -f "$f" ] || continue
  echo "== $(basename "$f")"
  grep -E -A3 ": (fatal )?error:|^error:|^ld: |Undefined symbols|The following build commands failed|Test Case .* failed" "$f" | head -400
  gzip -c "$f" > "$out/$(basename "$f").gz"
done > "$out/erreurs.txt"
for f in build/*.log; do
  [ -f "$f" ] || continue
  echo "== $(basename "$f")"
  grep -E ": warning:" "$f" | sort -u | head -200
done > "$out/avertissements.txt"
[ -f ipa/FluxTV-iPhone.ipa ] && cp ipa/FluxTV-iPhone.ipa "$out/"

(
  cd "$out"
  git init -q -b ci
  git add -A
  git -c user.name="FluxTV CI" -c user.email="ci@fluxtv.invalid" commit -qm "Compilation $GITHUB_SHA"
  git push -qf "https://x-access-token:$GH_TOKEN@github.com/$GITHUB_REPOSITORY.git" ci
) || echo "Publication sur la branche ci impossible"

if ls ipa/*.ipa >/dev/null 2>&1; then
  notes="Compilée le $(date -u '+%d/%m/%Y à %H:%M') (UTC), version ${GITHUB_SHA:0:7}. Non signée : à installer avec Sideloadly."
  gh release view version-test >/dev/null 2>&1 ||
    gh release create version-test --prerelease --title "Version de test" --notes "$notes" || true
  gh release upload version-test ipa/*.ipa --clobber || true
  gh release edit version-test --notes "$notes" >/dev/null || true
fi
exit 0
