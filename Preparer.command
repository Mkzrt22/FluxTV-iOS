#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
#  FluxTV pour iPhone, iPad et Apple TV — préparation du projet (une seule fois)
#
#  Double-clic sur ce fichier (ou : clic droit › Ouvrir la première fois).
#   1. Télécharge le moteur de lecture VLC officiel (VideoLAN) et vérifie son empreinte
#   2. Garde uniquement les architectures utiles (taille réduite)
#   3. Renseigne ton équipe Apple si ton compte est ajouté dans Xcode
#   4. Ouvre le projet dans Xcode
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail
cd "$(dirname "$0")"
CI_MODE="${1:-}"

echo ""
echo "== FluxTV : préparation du projet Xcode =="

if ! xcode-select -p >/dev/null 2>&1; then
  echo "Xcode n'est pas installé : installe-le depuis l'App Store (gratuit), ouvre-le une fois, puis relance ce fichier."
  read -r -p "Appuie sur Entrée pour fermer." _ || true
  exit 1
fi

mkdir -p Vendor

fetch() {
  local name="$1" url="$2" sha="$3"
  if [ -d "Vendor/$name.xcframework" ]; then
    echo "✔ $name déjà présent"
    return
  fi
  echo "→ Téléchargement de $name (moteur VLC, 130 à 230 Mo)…"
  local tmp
  tmp="$(mktemp -d)"
  curl -fL --retry 3 --progress-bar -o "$tmp/$name.tar.xz" "$url"
  echo "→ Vérification de l'empreinte…"
  echo "$sha  $tmp/$name.tar.xz" | shasum -a 256 -c - >/dev/null || {
    echo "✘ Fichier altéré ou incomplet : relance le script."; rm -rf "$tmp"; exit 1; }
  echo "→ Décompression…"
  tar -xJf "$tmp/$name.tar.xz" -C "$tmp"
  mv "$tmp/$name-binary/$name.xcframework" "Vendor/"
  cp "$tmp/$name-binary/COPYING.txt" "Vendor/$name-LICENCE-LGPL.txt" 2>/dev/null || true
  rm -rf "$tmp"
  echo "✔ $name prêt"
}

fetch MobileVLCKit "https://download.videolan.org/pub/cocoapods/prod/MobileVLCKit-3.0-20260922-1512.tar.xz" \
  "c649c4c47bd065b3a5511746361b5a9066a34033f2df9cbc9454409c0f363b52"
fetch TVVLCKit "https://download.videolan.org/cocoapods/prod/TVVLCKit-3.0-20260930-0854.tar.xz" \
  "07892cae8e59e95a23f1fcf11692f802eae106312aa5ad9782faa2556b2d65d5"

# iPhone : les anciennes architectures 32 bits (armv7) ne servent plus depuis iOS 11
FW_DIR="Vendor/MobileVLCKit.xcframework"
for lib in "$FW_DIR"/ios-arm64_armv7*; do
  [ -d "$lib" ] || continue
  bin="$lib/MobileVLCKit.framework/MobileVLCKit"
  if lipo -archs "$bin" 2>/dev/null | grep -q armv7; then
    echo "→ Allègement du moteur iPhone (arm64 uniquement)…"
    lipo "$bin" -extract arm64 -output "$bin.arm64" 2>/dev/null || lipo "$bin" -thin arm64 -output "$bin.arm64"
    mv "$bin.arm64" "$bin"
    PB=/usr/libexec/PlistBuddy
    plist="$FW_DIR/Info.plist"
    i=0
    while $PB -c "Print :AvailableLibraries:$i" "$plist" >/dev/null 2>&1; do
      if [ "$($PB -c "Print :AvailableLibraries:$i:LibraryIdentifier" "$plist")" = "$(basename "$lib")" ]; then
        $PB -c "Delete :AvailableLibraries:$i:SupportedArchitectures" "$plist"
        $PB -c "Add :AvailableLibraries:$i:SupportedArchitectures array" "$plist"
        $PB -c "Add :AvailableLibraries:$i:SupportedArchitectures:0 string arm64" "$plist"
      fi
      i=$((i + 1))
    done
  fi
done

# Équipe Apple (signature) : prise dans Xcode › Réglages › Comptes si elle y est
CFG="Config/Signing.xcconfig"
if ! grep -qE '^DEVELOPMENT_TEAM = [A-Z0-9]{10}' "$CFG"; then
  TEAM="$(defaults read com.apple.dt.Xcode IDEProvisioningTeamByIdentifier 2>/dev/null | awk '
    /isFreeProvisioningTeam/ { free = ($3 ~ /1/) }
    /teamID/ { gsub(/[;"]/, "", $3); if (!free && paid == "") paid = $3; if (any == "") any = $3 }
    END { print (paid != "" ? paid : any) }' || true)"
  if [ -n "$TEAM" ]; then
    sed -i '' "s/^DEVELOPMENT_TEAM =.*/DEVELOPMENT_TEAM = $TEAM/" "$CFG"
    echo "✔ Équipe Apple : $TEAM"
  else
    echo "ℹ︎ Aucune équipe Apple trouvée : dans Xcode, ouvre Réglages › Comptes, ajoute ton identifiant Apple, puis relance ce fichier."
  fi
fi

echo ""
echo "✔ Tout est prêt."
if [ "$CI_MODE" != "--ci" ]; then
  echo "→ Ouverture du projet dans Xcode…"
  open FluxTV.xcodeproj
  echo ""
  echo "Dans Xcode : choisis « FluxTV » (iPhone / iPad) ou « FluxTV TV » (Apple TV) en haut,"
  echo "puis ton appareil, et clique sur ▶︎."
  read -r -p "Appuie sur Entrée pour fermer cette fenêtre." _ || true
fi
