#!/bin/bash
# GitHub : archives App Store de FluxTV (iPhone/iPad et Apple TV).
#
#   .github/appstore-archive.sh verifier   archives NON signées et contrôles. Rien n'est envoyé à Apple.
#   .github/appstore-archive.sh envoyer    en plus : Apple signe les apps (« cloud signing », aucun
#                                          certificat à fabriquer) et elles partent sur App Store
#                                          Connect (TestFlight). Clé d'API App Store Connect lue dans
#                                          ASC_KEY_ID, ASC_ISSUER_ID et ASC_KEY_P8 (secrets du dépôt).
#
# PLATEFORMES : « les-deux » (par défaut), « iphone-ipad » ou « apple-tv ».
# Numéro de build : BUILD_NUMBER, sinon 10 + numéro de l'exécution du workflow.
set -uo pipefail
cd "$(dirname "$0")/.."
MODE="${1:-verifier}"
PLATEFORMES="${PLATEFORMES:-les-deux}"
OUT="$PWD/appstore/verification"
TEAM="${APPLE_TEAM_ID:-NNQNJQ6SG2}"
BUILD="${BUILD_NUMBER:-$(( ${GITHUB_RUN_NUMBER:-0} + 10 ))}"
VERSION="$(awk -F'"' '/MARKETING_VERSION/ { print $2; exit }' project.yml)"
rm -rf "$OUT"
mkdir -p "$OUT" build
log() { echo "$*" | tee -a "$OUT/journal.txt"; }
statut=0
erreur() { log "  ✘ $*"; statut=1; }
avert() { log "  ⚠ $*"; }
valeur() { /usr/libexec/PlistBuddy -c "Print :$2" "$1" 2>/dev/null; }
# Les journaux publics ne montrent jamais le nom du titulaire du compte (identités de signature)
masquer() { sed -E 's/((Apple|iPhone|iOS) (Distribution|Development|Developer)): [^(]*\(/\1: … (/g'; }

if [ "$MODE" = envoyer ]; then
  for v in ASC_KEY_ID ASC_ISSUER_ID ASC_KEY_P8; do
    if [ -z "${!v:-}" ]; then
      log "✘ Secret $v absent : envoi impossible. Voir AppStore/Envoi-App-Store.md."
      exit 1
    fi
  done
  CLE_DIR="$(mktemp -d)"
  trap 'rm -rf "$CLE_DIR"' EXIT
  CLE="$CLE_DIR/AuthKey_$ASC_KEY_ID.p8"
  # Contenu du fichier .p8 collé tel quel (ou en base64)
  case "$ASC_KEY_P8" in
    *"BEGIN PRIVATE KEY"*) printf '%s\n' "$ASC_KEY_P8" > "$CLE" ;;
    *) printf '%s' "$ASC_KEY_P8" | base64 --decode > "$CLE" 2>/dev/null ;;
  esac
  chmod 600 "$CLE"
  grep -q "BEGIN PRIVATE KEY" "$CLE" || { log "✘ ASC_KEY_P8 ne contient pas une clé .p8"; exit 1; }
elif [ "$MODE" != verifier ]; then
  echo "Usage : $0 verifier|envoyer"
  exit 2
fi

./Preparer.command --ci > /dev/null || { log "✘ préparation"; exit 1; }
log "$(xcodebuild -version | head -1) · FluxTV $VERSION, build $BUILD · mode : $MODE"

# $1 schéma, $2 plateforme, $3 nom
archiver() {
  if xcodebuild -project FluxTV.xcodeproj -scheme "$1" -configuration Release \
      -destination "generic/platform=$2" -archivePath "build/$3.xcarchive" -derivedDataPath "build/DD-$3" \
      CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" \
      DEVELOPMENT_TEAM="$TEAM" CURRENT_PROJECT_VERSION="$BUILD" \
      archive > "build/archive-$3.log" 2>&1; then
    log "✔ archive $3"
  else
    erreur "archive $3"
    grep -E ": (fatal )?error:|^error:" "build/archive-$3.log" | head -20 | masquer | tee -a "$OUT/journal.txt"
    return 1
  fi
}

# Icônes compilées : nom, taille en pixels, appareil
icones() {
  xcrun --sdk "$2" assetutil --info "$1/Assets.car" 2>/dev/null | python3 -c '
import json, sys
try:
    items = json.load(sys.stdin)
except Exception:
    sys.exit(0)
seen = set()
for i in items:
    n = i.get("Name") or i.get("RenditionName") or ""
    if "Icon" in n or "Top Shelf" in n:
        s = "%s %sx%s %s" % (n, i.get("PixelWidth", "?"), i.get("PixelHeight", "?"), i.get("Idiom", ""))
        if s not in seen:
            seen.add(s); print(s)
'
}

# Contrôles de l'app archivée ($1 nom, $2 SDK)
controler() {
  local app="build/$1.xcarchive/Products/Applications/FluxTV.app"
  local plist="$app/Info.plist" bin="$app/FluxTV" v fw n b archs
  [ -d "$app" ] || { erreur "$1 : app absente de l'archive"; return; }
  log "── Contrôles $1"
  v="$(valeur "$plist" CFBundleIdentifier)"; [ "$v" = bzh.fluxtv.player ] || erreur "identifiant : $v"
  v="$(valeur "$plist" CFBundleShortVersionString)"; [ "$v" = "$VERSION" ] || erreur "version : $v"
  v="$(valeur "$plist" CFBundleVersion)"; [ "$v" = "$BUILD" ] || erreur "build : $v"
  v="$(valeur "$plist" MinimumOSVersion)"; [ "$v" = 17.0 ] || erreur "système minimum : $v"
  v="$(valeur "$plist" ITSAppUsesNonExemptEncryption)"; [ "$v" = false ] || erreur "chiffrement non déclaré"
  log "  bzh.fluxtv.player $(valeur "$plist" CFBundleShortVersionString) ($(valeur "$plist" CFBundleVersion)), $(valeur "$plist" DTSDKName), système $(valeur "$plist" MinimumOSVersion) minimum"
  [ -f "$app/PrivacyInfo.xcprivacy" ] && log "  ✔ manifeste de confidentialité" || erreur "manifeste de confidentialité absent"
  [ ! -e "$app/compte.flux" ] && log "  ✔ aucun compte intégré" || erreur "UN COMPTE EST INTÉGRÉ À L'APP"
  strings -a "$bin" > "build/textes-$1.txt"
  if grep -qE "FluxTVKeepControls|FluxTVStartAt|FluxTVOpen" "build/textes-$1.txt"; then
    erreur "réglages d'essai présents dans la version App Store"
  else
    log "  ✔ réglages d'essai absents (version Release)"
  fi
  icones "$app" "$2" > "$OUT/icones-$1.txt"
  if [ "$1" = iOS ]; then
    grep -q "1024x1024" "$OUT/icones-$1.txt" && log "  ✔ icône App Store 1024 × 1024" || avert "icône 1024 × 1024 non trouvée (à vérifier)"
  else
    grep -q "1280x768" "$OUT/icones-$1.txt" && log "  ✔ icône App Store 1280 × 768" || avert "icône App Store 1280 × 768 non trouvée (à vérifier)"
    grep -q "Top Shelf" "$OUT/icones-$1.txt" && log "  ✔ image « Top Shelf »" || avert "image « Top Shelf » non trouvée (à vérifier)"
  fi
  for fw in "$app"/Frameworks/*.framework; do
    [ -d "$fw" ] || continue
    n="$(basename "$fw" .framework)"
    b="$fw/$(valeur "$fw/Info.plist" CFBundleExecutable)"
    archs="$(lipo -archs "$b" 2>/dev/null)"
    [ "$archs" = arm64 ] || erreur "$n : architectures « $archs »"
    [ "$(otool -l "$b" | grep -c "segname __LLVM")" = 0 ] || erreur "$n : contient du bitcode"
    [ "$(file "$b" | grep -c "dynamically linked shared library")" -ge 1 ] || erreur "$n : n'est pas une bibliothèque dynamique"
    for k in CFBundleIdentifier CFBundleShortVersionString CFBundleVersion MinimumOSVersion; do
      [ -n "$(valeur "$fw/Info.plist" $k)" ] || erreur "$n : $k manquant"
    done
    log "  ✔ $n : $archs, système $(valeur "$fw/Info.plist" MinimumOSVersion) minimum, $(du -sh "$fw" | cut -f1)"
  done
  log "  taille de l'app : $(du -sh "$app" | cut -f1)"
}

# Signature par Apple et envoi sur App Store Connect ($1 nom)
envoyer() {
  cat > build/ExportOptions.plist <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>method</key><string>app-store-connect</string>
	<key>destination</key><string>upload</string>
	<key>teamID</key><string>$TEAM</string>
	<key>signingStyle</key><string>automatic</string>
	<key>uploadSymbols</key><true/>
	<key>manageAppVersionAndBuildNumber</key><false/>
</dict>
</plist>
EOF
  if xcodebuild -exportArchive -archivePath "build/$1.xcarchive" -exportPath "build/envoi-$1" \
      -exportOptionsPlist build/ExportOptions.plist -allowProvisioningUpdates \
      -authenticationKeyPath "$CLE" -authenticationKeyID "$ASC_KEY_ID" -authenticationKeyIssuerID "$ASC_ISSUER_ID" \
      > "build/envoi-$1.log" 2>&1; then
    log "✔ $1 envoyé à App Store Connect (build $BUILD) : traitement par Apple, puis TestFlight"
  else
    erreur "envoi $1"
    grep -E "error|Error|ERROR|failed" "build/envoi-$1.log" | grep -v "^ *$" | head -30 | masquer | tee -a "$OUT/journal.txt"
  fi
}

for p in iOS AppleTV; do
  case "$PLATEFORMES:$p" in
    iphone-ipad:AppleTV | apple-tv:iOS) continue ;;
  esac
  if [ "$p" = iOS ]; then
    archiver FluxTV iOS iOS && controler iOS iphoneos
  else
    archiver "FluxTV TV" tvOS AppleTV && controler AppleTV appletvos
  fi
  if [ "$MODE" = envoyer ]; then
    if [ "$statut" = 0 ]; then envoyer "$p"; else log "✘ $p non envoyé : contrôles en échec"; fi
  fi
done

[ "$statut" = 0 ] && log "✔ Tout est conforme" || log "✘ Des contrôles ont échoué"
exit "$statut"
