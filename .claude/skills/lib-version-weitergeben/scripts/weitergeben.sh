#!/usr/bin/env bash
# Hebt Conspiratio.Lib auf <version> und bringt sie in den Godot-Client.
#
# Der Schritt, der hier zaehlt, ist das Loeschen des NuGet-Cache-Eintrags: Ohne ihn extrahiert NuGet
# die bekannte Version nicht neu und der Client baut stillschweigend gegen alten Code. Deshalb prueft
# das Skript am Ende nach, statt es anzunehmen.
#
# Aufruf: weitergeben.sh <version>      z. B. weitergeben.sh 4.2.1

set -euo pipefail

LIB="/d/Projekte/C# Projekte/Conspiratio.Lib"
CLIENT="/c/Projekte/Godot/Conspiratio.Godot"
GODOT="/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe"

VERSION="${1:-}"
if [ -z "$VERSION" ]; then
  echo "Aufruf: weitergeben.sh <version>   (z. B. 4.2.1)" >&2
  exit 1
fi

CSPROJ="$LIB/Conspiratio.Lib/Conspiratio.Lib.csproj"
CACHE="$HOME/.nuget/packages/conspiratio.lib/$VERSION"
FEED="$LIB/Conspiratio.Lib/bin/Debug"

echo "== 1/5  Version in der Lib auf $VERSION setzen"
python - "$CSPROJ" "$VERSION" <<'PY'
import re, sys
pfad, version = sys.argv[1], sys.argv[2]
with open(pfad, encoding="utf-8-sig", newline="") as f:
    inhalt = f.read()
neu, n = re.subn(r"<Version>[^<]*</Version>", f"<Version>{version}</Version>", inhalt, count=1)
if n != 1:
    sys.exit(f"FEHLER: <Version> in {pfad} nicht gefunden")
with open(pfad, "w", encoding="utf-8-sig", newline="") as f:
    f.write(neu)
PY

echo "== 2/5  Lib bauen (erzeugt das .nupkg)"
(cd "$LIB" && dotnet build --nologo -v quiet)

NUPKG="$FEED/Conspiratio.Lib.$VERSION.nupkg"
[ -f "$NUPKG" ] || { echo "FEHLER: $NUPKG wurde nicht erzeugt." >&2; exit 1; }

echo "== 3/5  Cache-Eintrag loeschen (der Schritt, der uebersprungen wird)"
rm -rf "$CACHE"

echo "== 4/5  Cache ueber ein Wegwerfprojekt fuellen"
# Das Testprojekt taugt dafuer NICHT: Es haengt per ProjectReference an der Lib und laesst den
# globalen Cache unberuehrt - ein stiller No-op.
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
cat > "$TMP/cachefueller.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Conspiratio.Lib" Version="$VERSION" />
  </ItemGroup>
</Project>
XML
printf 'class P { static void Main() { } }\n' > "$TMP/Program.cs"
(cd "$TMP" && dotnet restore --source "$FEED" --nologo -v quiet)

[ -d "$CACHE" ] || { echo "FEHLER: $CACHE fehlt weiterhin - der Restore hat den Cache nicht gefuellt." >&2; exit 1; }

echo "== 5/5  Client umstellen, bauen, Smoke-Test"
python - "$CLIENT/Conspiratio.Godot.csproj" "$VERSION" <<'PY'
import re, sys
pfad, version = sys.argv[1], sys.argv[2]
with open(pfad, encoding="utf-8-sig", newline="") as f:
    inhalt = f.read()
neu, n = re.subn(r'(Include="Conspiratio\.Lib" Version=")[^"]*(")', rf"\g<1>{version}\g<2>", inhalt, count=1)
if n != 1:
    sys.exit(f"FEHLER: PackageReference in {pfad} nicht gefunden")
with open(pfad, "w", encoding="utf-8-sig", newline="") as f:
    f.write(neu)
PY

(cd "$CLIENT" && dotnet build --nologo -v quiet)
(cd "$CLIENT" && "$GODOT" --headless --path . --quit "res://scenes/Main.tscn" >/dev/null 2>&1) \
  || { echo "FEHLER: Smoke-Test fehlgeschlagen." >&2; exit 1; }

echo
echo "== Nachweis, dass der Client wirklich $VERSION nutzt"

ASSETS="$CLIENT/.godot/mono/temp/obj/project.assets.json"
grep -q "\"Conspiratio.Lib/$VERSION\"" "$ASSETS" \
  && echo "   [ok] project.assets.json loest Conspiratio.Lib/$VERSION auf" \
  || { echo "   [FEHLER] project.assets.json nennt $VERSION nicht" >&2; exit 1; }

GEBAUT="$(find "$CACHE/lib" -name 'Conspiratio.Lib.dll' | head -1)"
KOPIERT="$CLIENT/.godot/mono/temp/bin/Debug/Conspiratio.Lib.dll"
if [ -f "$GEBAUT" ] && [ -f "$KOPIERT" ] \
   && [ "$(sha256sum "$GEBAUT" | cut -d' ' -f1)" = "$(sha256sum "$KOPIERT" | cut -d' ' -f1)" ]; then
  echo "   [ok] DLL im Build-Output ist byte-identisch mit der aus dem Cache"
else
  echo "   [FEHLER] DLL im Build-Output weicht ab - der Client nutzt eine andere Fassung." >&2
  exit 1
fi

echo
echo "Fertig. Noch von Hand:"
echo "  - CHANGELOG der Lib (DE und EN) unter dem einen [Unreleased]-Block"
echo "  - Commits: erst Lib, dann Client; Client-Betreff nennt Conspiratio.Lib $VERSION"
echo "  - Kein 'git add -A' - nur die geaenderten Pfade stagen"
