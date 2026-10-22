#!/bin/bash
# GitHub : essai de l'app sur des simulateurs (iPhone, iPad, Apple TV) avec la démonstration :
# lancement, écrans principaux, lecture d'un film et d'une chaîne. Captures d'écran, journaux
# et rapports de plantage publiés sur la branche « ci-simulateurs ».
set -uo pipefail
cd "$(dirname "$0")/.."
OUT="$PWD/captures"
rm -rf "$OUT"
mkdir -p "$OUT/plantages"
touch "$OUT/.debut"
log() { echo "$*" | tee -a "$OUT/journal.txt"; }

./Preparer.command --ci > /dev/null || { log "✘ préparation"; exit 1; }
log "$(xcodebuild -version | head -1)"

# $1 schéma, $2 plateforme, $3 nom court
compiler() {
  if xcodebuild -project FluxTV.xcodeproj -scheme "$1" -configuration Debug \
      -destination "generic/platform=$2" -derivedDataPath build/Sim \
      CODE_SIGNING_ALLOWED=NO build > "$OUT/compilation-$3.log" 2>&1; then
    log "✔ compilation $3"
  else
    log "✘ compilation $3"
    grep -E ": (fatal )?error:" "$OUT/compilation-$3.log" | head -20 | tee -a "$OUT/journal.txt"
    return 1
  fi
}

# Simulateur disponible pour $1 (iPhone, iPad ou Apple TV) : modèle conseillé, système le plus récent
simulateur() {
  xcrun simctl list devices available -j | python3 -c '
import json, re, sys
kind = sys.argv[1]
prefer = {"iPhone": r"^iPhone \d+ Pro$", "iPad": r"^iPad Pro 11", "Apple TV": r"^Apple TV 4K"}[kind]
found = []
for runtime, devices in json.load(sys.stdin)["devices"].items():
    for d in devices:
        if d["name"].startswith(kind):
            version = [int(n) for n in re.findall(r"\d+", runtime)]
            found.append((bool(re.search(prefer, d["name"])), version, d["name"], d["udid"]))
found.sort()
print(found[-1][3] if found else "")
' "$1"
}

demarrer() {
  UDID="$(simulateur "$1")"
  if [ -z "$UDID" ]; then log "✘ aucun simulateur $1"; return 1; fi
  log "→ $(xcrun simctl list devices | grep "$UDID" | sed -E 's/^ +//; s/ \([0-9A-F-]{36}\)//')"
  xcrun simctl boot "$UDID" > /dev/null 2>&1
  xcrun simctl bootstatus "$UDID" -b > /dev/null 2>&1
}

capture() {
  if xcrun simctl io "$1" screenshot "$OUT/$2.png" > /dev/null 2>&1; then log "  capture $2"; else log "  ✘ capture $2"; fi
}

# $1 simulateur, $2 attente (s), $3 nom de la capture, puis les arguments de lancement
essai() {
  local udid="$1" attente="$2" nom="$3" pid
  shift 3
  xcrun simctl terminate "$udid" bzh.fluxtv.player > /dev/null 2>&1
  sleep 1
  # « bzh.fluxtv.player: 12345 » : l'app du simulateur est un processus du Mac
  pid="$(xcrun simctl launch --stdout="$OUT/$nom.out.txt" --stderr="$OUT/$nom.err.txt" \
      "$udid" bzh.fluxtv.player "$@" 2>/dev/null | awk -F': ' '/bzh.fluxtv.player/ { print $2 }')"
  if [ -z "$pid" ]; then
    log "  ✘ lancement impossible ($nom)"
    return
  fi
  sleep "$attente"
  if kill -0 "$pid" 2>/dev/null; then
    capture "$udid" "$nom"
  else
    log "  ✘ PLANTAGE : l'app s'est fermée ($nom)"
    capture "$udid" "$nom-plantage"
  fi
}

if compiler FluxTV "iOS Simulator" ios; then
  APP=build/Sim/Build/Products/Debug-iphonesimulator/FluxTV.app
  for type in iPhone iPad; do
    demarrer "$type" || continue
    xcrun simctl status_bar "$UDID" override --time 9:41 --batteryState charged --batteryLevel 100 > /dev/null 2>&1
    xcrun simctl install "$UDID" "$APP" || { log "✘ installation ($type)"; continue; }
    p="$(echo "$type" | tr '[:upper:]' '[:lower:]')"
    essai "$UDID" 8 "$p-1-bienvenue"
    essai "$UDID" 12 "$p-2-accueil" -FluxTVDemo YES -lastTab home
    essai "$UDID" 8 "$p-3-tv" -lastTab live
    essai "$UDID" 8 "$p-4-films" -lastTab movies
    essai "$UDID" 8 "$p-5-series" -lastTab series
    essai "$UDID" 8 "$p-8-recherche" -lastTab search
    essai "$UDID" 30 "$p-6-lecture-film" -FluxTVDemo YES -FluxTVPlay demo:bbb
    essai "$UDID" 30 "$p-7-lecture-direct" -FluxTVDemo YES -FluxTVPlay demo:live2
    xcrun simctl shutdown "$UDID" > /dev/null 2>&1
  done
fi

if compiler "FluxTV TV" "tvOS Simulator" tvos; then
  APP=build/Sim/Build/Products/Debug-appletvsimulator/FluxTV.app
  if demarrer "Apple TV"; then
    if xcrun simctl install "$UDID" "$APP"; then
      essai "$UDID" 8 "tv-1-bienvenue"
      essai "$UDID" 12 "tv-2-accueil" -FluxTVDemo YES -lastTab home
      essai "$UDID" 8 "tv-3-direct" -lastTab live
      essai "$UDID" 8 "tv-4-films" -lastTab movies
      essai "$UDID" 8 "tv-6-reglages" -lastTab settings
      essai "$UDID" 30 "tv-5-lecture-film" -FluxTVDemo YES -FluxTVPlay demo:bbb
    else
      log "✘ installation (Apple TV)"
    fi
    xcrun simctl shutdown "$UDID" > /dev/null 2>&1
  fi
fi

find ~/Library/Logs/DiagnosticReports -name "FluxTV*" -newer "$OUT/.debut" -exec cp {} "$OUT/plantages/" \; 2>/dev/null
log "Plantages enregistrés : $(find "$OUT/plantages" -type f | wc -l | tr -d ' ')"
rm -f "$OUT/.debut"
gzip -f "$OUT"/*.log 2>/dev/null

(
  cd "$OUT"
  git init -q -b ci-simulateurs
  git add -A
  git -c user.name="FluxTV CI" -c user.email="ci@fluxtv.invalid" commit -qm "Simulateurs $GITHUB_SHA"
  git push -qf "https://x-access-token:$GH_TOKEN@github.com/$GITHUB_REPOSITORY.git" ci-simulateurs
) || echo "Publication sur la branche ci-simulateurs impossible"
exit 0
