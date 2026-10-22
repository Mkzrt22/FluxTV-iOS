#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
#  FluxTV — fabrique l'app à installer soi-même (fichiers .ipa non signés)
#
#  Double-clic sur ce fichier (ou : clic droit › Ouvrir la première fois).
#  Résultat dans le dossier « ipa » :
#    FluxTV-iPhone.ipa    iPhone et iPad
#    FluxTV-AppleTV.ipa   Apple TV
#  À installer avec Sideloadly et ton identifiant Apple (voir README).
# ─────────────────────────────────────────────────────────────────────────────
set -uo pipefail
cd "$(dirname "$0")"
CI_MODE="${1:-}"

./Preparer.command --ci || exit 1

rm -rf ipa
mkdir -p ipa build
status=0

# $1 schéma Xcode, $2 plateforme, $3 dossier des produits, $4 nom du .ipa
fabriquer() {
  local scheme="$1" platform="$2" products="$3" name="$4"
  local log="build/$name.log" out="$PWD/ipa/$name.ipa"
  echo "→ Compilation de $name (quelques minutes)…"
  if xcodebuild -project FluxTV.xcodeproj -scheme "$scheme" -configuration Release \
      -destination "generic/platform=$platform" -derivedDataPath build/DerivedData \
      -continueBuildingAfterErrors \
      CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" \
      build >"$log" 2>&1; then
    local tmp
    tmp="$(mktemp -d)"
    mkdir "$tmp/Payload"
    cp -R "build/DerivedData/Build/Products/Release-$products/FluxTV.app" "$tmp/Payload/"
    (cd "$tmp" && zip -qry "$out" Payload)
    rm -rf "$tmp"
    echo "✔ ipa/$name.ipa ($(du -h "$out" | cut -f1))"
  else
    echo "✘ $name : la compilation a échoué. Détails : $log"
    grep -E -A3 ": (fatal )?error:|^ld: |Undefined symbols" "$log" | head -60
    status=1
  fi
}

fabriquer FluxTV iOS iphoneos FluxTV-iPhone
fabriquer "FluxTV TV" tvOS appletvos FluxTV-AppleTV

if [ "$CI_MODE" != "--ci" ]; then
  [ "$status" = 0 ] && open ipa
  read -r -p "Appuie sur Entrée pour fermer cette fenêtre." _ || true
fi
exit "$status"
