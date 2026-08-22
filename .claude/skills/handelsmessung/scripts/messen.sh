#!/usr/bin/env bash
# E2E-Messmatrix ueber vier feste Seeds. Gibt das Ergebnis als Band aus, weil eine einzelne Zahl
# hier nichts aussagt: Der Seed beherrscht alles andere.
#
# Aufruf: messen.sh [jahre]     Vorgabe 15

set -uo pipefail

CLIENT="/c/Projekte/Godot/Conspiratio.Godot"
GODOT="/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe"
JAHRE="${1:-15}"
SEEDS=(4711 1234 2023 42)

AUS="$(mktemp -d)"
trap 'rm -rf "$AUS"' EXIT

echo "E2E-Messmatrix: $JAHRE Jahre, 1 Spieler, ohne Zufallsaktionen"
echo "Vier Laeufe a einige Minuten - bitte durchlaufen lassen."
echo

printf "%-6s %8s %8s %10s %10s %6s\n" "Seed" "Taler" "Zuege" "Jahre" "Waren" "Exit"
printf -- "------------------------------------------------------------\n"

werte=()
for seed in "${SEEDS[@]}"; do
  log="$AUS/seed_$seed.txt"
  (cd "$CLIENT" && "$GODOT" --headless --path . "res://scenes/E2eTest.tscn" -- \
      "--jahre=$JAHRE" --spieler=1 --ohne-aktionen "--seed=$seed") > "$log" 2>&1
  exit_code=$?

  hole() { grep -oE "$1" "$log" | grep -oE '\-?[0-9]+$' | head -1; }
  taler=$(hole 'Spieler 1: *-?[0-9]+')
  zuege=$(hole 'Züge: *[0-9]+')
  jahre=$(hole 'Gespielte Jahre: *[0-9]+')
  waren=$(hole 'Verkaufte Waren: *[0-9]+')

  printf "%-6s %8s %8s %10s %10s %6s\n" \
    "$seed" "${taler:-?}" "${zuege:-?}" "${jahre:-?}" "${waren:-?}" "$exit_code"

  [ -n "${taler:-}" ] && werte+=("$taler")
done

echo
if [ "${#werte[@]}" -gt 0 ]; then
  python - "${werte[@]}" <<'PY'
import sys
w = sorted(int(x) for x in sys.argv[1:])
print(f"Band:   {w[0]:+d} bis {w[-1]:+d}")
print(f"Mittel: {sum(w) // len(w):+d}  ({len(w)} Laeufe)")
print()
print("Als Band lesen, nie als Einzelzahl - der Seed beherrscht alles andere.")
print("Unterschiede unter ~10 000 Talern sind nichts.")
print("Nicht auf Gespielte Jahre normieren: die Zugzahl ist konstant, die Jahreszahl nicht.")
PY
else
  echo "Keine Werte gelesen - bitte die Logs pruefen."
fi
