#!/bin/bash
# GitHub : captures d'écran pour la fiche App Store, aux tailles demandées par Apple, en français
# et en anglais, avec la démonstration (films libres de la Blender Foundation, aucune chaîne
# protégée à l'écran) :
#   iPhone 6,9 pouces  1320 × 2868 (lecteur : 2868 × 1320)
#   iPad 13 pouces     2752 × 2064 (à l'horizontale)
#   Apple TV           capturée en 3840 × 2160, réduite en 1920 × 1080
# Rien n'est envoyé à Apple. Résultat : dossier « appstore/captures » (publié par le workflow).
set -uo pipefail
cd "$(dirname "$0")/.."
OUT="$PWD/appstore/captures"
BUNDLE=bzh.fluxtv.player
rm -rf "$OUT"
mkdir -p "$OUT/journaux" "$OUT/plantages"
touch "$OUT/.debut"
log() { echo "$*" | tee -a "$OUT/journal.txt"; }

./Preparer.command --ci > /dev/null || { log "✘ préparation"; exit 1; }
log "$(xcodebuild -version | head -1)"

# $1 schéma, $2 plateforme, $3 nom court
compiler() {
  if xcodebuild -project FluxTV.xcodeproj -scheme "$1" -configuration Debug \
      -destination "generic/platform=$2" -derivedDataPath build/Sim \
      CODE_SIGNING_ALLOWED=NO build > "$OUT/journaux/compilation-$3.log" 2>&1; then
    log "✔ compilation $3"
  else
    log "✘ compilation $3"
    grep -E ": (fatal )?error:" "$OUT/journaux/compilation-$3.log" | head -20 | tee -a "$OUT/journal.txt"
    return 1
  fi
}

# Simulateur du modèle demandé ($1 : expression sur le nom, $2 : iOS ou tvOS), système le plus
# récent. Créé s'il n'existe pas encore. Affiche son identifiant.
simulateur() {
  python3 - "$1" "$2" <<'PY'
import json, re, subprocess, sys
want, platform = sys.argv[1], sys.argv[2]
data = json.loads(subprocess.check_output(["xcrun", "simctl", "list", "-j"]))
def key(name):
    return [int(n) for n in re.findall(r"\d+", name)]
runtimes = [r for r in data["runtimes"] if r.get("isAvailable") and r.get("platform", r["name"].split()[0]) == platform]
runtimes.sort(key=lambda r: key(r.get("version", r["name"])), reverse=True)
for rt in runtimes:
    types = rt.get("supportedDeviceTypes") or data["devicetypes"]
    types = sorted((t for t in types if re.search(want, t["name"])), key=lambda t: key(t["name"]), reverse=True)
    if not types:
        continue
    t = types[0]
    for d in data["devices"].get(rt["identifier"], []):
        if d.get("isAvailable") and d.get("deviceTypeIdentifier") == t["identifier"]:
            print(d["udid"]); sys.exit(0)
    print(subprocess.check_output(["xcrun", "simctl", "create", "FluxTV " + t["name"], t["identifier"], rt["identifier"]], text=True).strip())
    sys.exit(0)
sys.exit(1)
PY
}

demarrer() {
  xcrun simctl boot "$1" > /dev/null 2>&1
  xcrun simctl bootstatus "$1" -b > /dev/null 2>&1
}

# Barre d'état propre (iPhone et iPad) : 9:41, réseau plein, batterie pleine
barre() {
  xcrun simctl status_bar "$1" override --time "2026-11-04T09:41:00Z" --dataNetwork wifi --wifiMode active \
    --wifiBars 3 --cellularMode active --cellularBars 4 --operatorName "" --batteryState charged \
    --batteryLevel 100 >> "$OUT/journaux/barre.txt" 2>&1 ||
  { log "  (barre d'état simplifiée)"; xcrun simctl status_bar "$1" override --time 9:41 --batteryState charged \
      --batteryLevel 100 > /dev/null 2>&1; }
}

# Système du simulateur dans la langue $2 (date de la barre d'état de l'iPad), puis redémarrage
langue_systeme() {
  local loc=en_US
  [ "$2" = fr ] && loc=fr_FR
  xcrun simctl spawn "$1" defaults write -g AppleLanguages -array "$2" > /dev/null 2>&1
  xcrun simctl spawn "$1" defaults write -g AppleLocale -string "$loc" > /dev/null 2>&1
  xcrun simctl shutdown "$1" > /dev/null 2>&1
  demarrer "$1"
  barre "$1"
}

capture() {
  if xcrun simctl io "$1" screenshot --type=png "$OUT/$2.png" > /dev/null 2>&1; then
    log "  capture $2 ($(sips -g pixelWidth -g pixelHeight "$OUT/$2.png" | awk '/pixel/ { printf "%s ", $2 }'))"
  else
    log "  ✘ capture $2"
  fi
}

# Lance l'app ($1 simulateur, $2 nom, puis les arguments) ; affiche le numéro du processus
lancer() {
  local udid="$1" nom="$2"
  shift 2
  xcrun simctl terminate "$udid" "$BUNDLE" > /dev/null 2>&1
  sleep 1
  # « bzh.fluxtv.player: 12345 » : l'app du simulateur est un processus du Mac
  xcrun simctl launch --stdout="$OUT/journaux/$nom.out.txt" --stderr="$OUT/journaux/$nom.err.txt" \
    "$udid" "$BUNDLE" "$@" 2>/dev/null | awk -F': ' '/bzh.fluxtv.player/ { print $2 }'
}

# $1 simulateur, $2 nom, $3 attentes avant chaque capture (« 14 7 7 » : trois captures), puis les
# arguments de lancement
essai() {
  local udid="$1" nom="$2" attentes="$3" pid n=0 a
  shift 3
  pid="$(lancer "$udid" "$nom" "$@")"
  if [ -z "$pid" ]; then
    log "  ✘ lancement impossible ($nom)"
    return
  fi
  set -- $attentes
  for a in "$@"; do
    sleep "$a"
    n=$((n + 1))
    local fichier="$nom"
    [ "$#" -gt 1 ] && fichier="$nom-$n"
    if kill -0 "$pid" 2>/dev/null; then
      capture "$udid" "$fichier"
    else
      log "  ✘ PLANTAGE : l'app s'est fermée ($nom)"
      capture "$udid" "$nom-plantage"
      return
    fi
  done
}

# Série complète pour un appareil ($1 simulateur, $2 iphone|ipad|tv, $3 langue, $4 app)
serie() {
  local u="$1" p="$2" l="$3" app="$4"
  local L=(-FluxTVLang "$l") O=() P=()
  [ "$p" = ipad ] && O=(-FluxTVOrientation landscape)
  [ "$p" != tv ] && P=(-FluxTVOrientation landscape)
  log "── $p ($l)"
  # Installation neuve : aucune source enregistrée, rien de regardé
  xcrun simctl uninstall "$u" "$BUNDLE" > /dev/null 2>&1
  xcrun simctl keychain "$u" reset >> "$OUT/journaux/trousseau.txt" 2>&1 || log "  (trousseau non vidé)"
  xcrun simctl install "$u" "$app" || { log "  ✘ installation"; return 1; }
  [ "$l" = fr ] && essai "$u" "$p-langue" 8 ${O[@]+"${O[@]}"}
  essai "$u" "$p-$l-bienvenue" 8 "${L[@]}" ${O[@]+"${O[@]}"}
  # TV en direct avant toute lecture : catégorie « Toutes »
  essai "$u" "$p-$l-direct" 12 "${L[@]}" ${O[@]+"${O[@]}"} -FluxTVDemo YES -lastTab live
  # Lecteur, commandes affichées (iPhone et iPad à l'horizontale)
  essai "$u" "$p-$l-lecture-direct" 22 "${L[@]}" ${P[@]+"${P[@]}"} -FluxTVDemo YES -FluxTVKeepControls YES -FluxTVPlay demo:live2
  essai "$u" "$p-$l-lecture-film" "16 6 6" "${L[@]}" ${P[@]+"${P[@]}"} -FluxTVDemo YES -FluxTVKeepControls YES \
    -FluxTVPlay demo:bbb -FluxTVStartAt 64
  # Écrans après la lecture : « Reprendre », chaînes récentes
  essai "$u" "$p-$l-accueil" 12 "${L[@]}" ${O[@]+"${O[@]}"} -FluxTVDemo YES -lastTab home
  essai "$u" "$p-$l-fiche-film" 10 "${L[@]}" ${O[@]+"${O[@]}"} -FluxTVDemo YES -lastTab home -FluxTVOpen demo:bbb
  essai "$u" "$p-$l-fiche-serie" 10 "${L[@]}" ${O[@]+"${O[@]}"} -FluxTVDemo YES -lastTab home -FluxTVOpen m3us:demo-open-movies
  essai "$u" "$p-$l-films" 8 "${L[@]}" ${O[@]+"${O[@]}"} -FluxTVDemo YES -lastTab movies
  if [ "$p" = tv ]; then
    essai "$u" "$p-$l-reglages" 8 "${L[@]}" -FluxTVDemo YES -lastTab settings
  else
    essai "$u" "$p-$l-reglages" 8 "${L[@]}" ${O[@]+"${O[@]}"} -FluxTVDemo YES -lastTab home -FluxTVSettings YES
  fi
}

# iPhone 6,9 pouces (1320 × 2868) et iPad 13 pouces (2064 × 2752)
if compiler FluxTV "iOS Simulator" ios; then
  APP=build/Sim/Build/Products/Debug-iphonesimulator/FluxTV.app
  for modele in "iphone|^iPhone [0-9]+ Pro Max$" "ipad|^iPad Pro 13-inch"; do
    p="${modele%%|*}"
    UDID="$(simulateur "${modele#*|}" iOS)"
    if [ -z "$UDID" ]; then log "✘ aucun simulateur pour $p"; continue; fi
    demarrer "$UDID"
    log "→ $(xcrun simctl list devices | grep "$UDID" | sed -E 's/^ +//; s/ \([0-9A-F-]{36}\)//')"
    for l in fr en; do
      langue_systeme "$UDID" "$l"
      serie "$UDID" "$p" "$l" "$APP"
    done
    xcrun simctl shutdown "$UDID" > /dev/null 2>&1
  done
fi

# Apple TV 4K (3840 × 2160, réduites en 1920 × 1080 : texte plus net qu'une capture en 1080p)
if compiler "FluxTV TV" "tvOS Simulator" tvos; then
  APP=build/Sim/Build/Products/Debug-appletvsimulator/FluxTV.app
  UDID="$(simulateur "^Apple TV 4K \(3rd generation\)$" tvOS)"
  if [ -n "$UDID" ]; then
    demarrer "$UDID"
    log "→ $(xcrun simctl list devices | grep "$UDID" | sed -E 's/^ +//; s/ \([0-9A-F-]{36}\)//')"
    for l in fr en; do serie "$UDID" tv "$l" "$APP"; done
    xcrun simctl shutdown "$UDID" > /dev/null 2>&1
    for f in "$OUT"/tv-*.png; do
      [ -f "$f" ] || continue
      w="$(sips -g pixelWidth "$f" | awk '/pixelWidth/ { print $2 }')"
      [ "$w" -gt 1920 ] && sips -z 1080 1920 "$f" > /dev/null
    done
    log "Apple TV : captures réduites en 1920 × 1080"
  else
    log "✘ aucun simulateur Apple TV 4K"
  fi
fi

find ~/Library/Logs/DiagnosticReports -name "FluxTV*" -newer "$OUT/.debut" -exec cp {} "$OUT/plantages/" \; 2>/dev/null
log "Plantages enregistrés : $(find "$OUT/plantages" -type f | wc -l | tr -d ' ')"
rm -f "$OUT/.debut"
gzip -f "$OUT"/journaux/*.log 2>/dev/null
exit 0
