"""PostToolUse-Waechter: nimmt die Tab-Verfaelschung von `godot --import` in CLAUDE.md zurueck.

Gemessen: Jeder Lauf von `godot --headless --path . --import` schreibt in CLAUDE.md fuehrende
Leerzeichen in Tabs um -- reproduzierbar dieselben ~29 Zeilen, und nur in dieser Datei. Das ist
nicht bloss kosmetisch: Ein fuehrender Tab in der Fortsetzungszeile eines Listenpunkts kann als
Codeblock geparst werden und zerreisst die Liste. Acht solcher Zeilen wurden unbemerkt committet,
bevor die Ursache gefunden war.

Sicherheitsnetz gegen Datenverlust: Zurueckgesetzt wird nur, wenn `git diff -w` leer ist, die
Aenderung also nachweislich rein aus Whitespace besteht. Steckt eine inhaltliche Aenderung darin --
etwa weil jemand CLAUDE.md gerade bearbeitet hat --, bleibt alles stehen. Lieber eine Verfaelschung
uebersehen als eine echte Aenderung wegwerfen.
"""

import json
import os
import subprocess
import sys

DATEI = "CLAUDE.md"


def git(projektverzeichnis: str, *argumente: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", *argumente],
        cwd=projektverzeichnis,
        capture_output=True,
        text=True,
        check=False,
    )


def main() -> int:
    try:
        eingabe = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0

    befehl = eingabe.get("tool_input", {}).get("command", "")

    if "--import" not in befehl:
        return 0

    projektverzeichnis = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()

    # Gibt es ueberhaupt eine Aenderung? Ohne sie ist nichts zu tun.
    if git(projektverzeichnis, "diff", "--quiet", "--", DATEI).returncode == 0:
        return 0

    # Ist sie rein kosmetisch? Nur dann darf zurueckgesetzt werden.
    if git(projektverzeichnis, "diff", "-w", "--quiet", "--", DATEI).returncode != 0:
        print(
            f"{DATEI} wurde nach einem Godot-Import inhaltlich veraendert, nicht nur in der "
            f"Einrueckung -- deshalb nichts zurueckgesetzt. Bitte selbst pruefen: "
            f"git diff -- {DATEI}"
        )
        return 0

    if git(projektverzeichnis, "checkout", "--", DATEI).returncode == 0:
        print(
            f"{DATEI}: Whitespace-Verfaelschung durch `godot --import` zurueckgesetzt "
            f"(Inhalt war unveraendert, per `git diff -w` geprueft)."
        )

    return 0


if __name__ == "__main__":
    sys.exit(main())
