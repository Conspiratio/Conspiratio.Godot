"""PreToolUse-Waechter: verhindert pauschales Stagen.

Beide Repos dieses Projekts tragen dauerhaft untrackte Dateien, die nie in einen Commit gehoeren --
`CONTRIBUTING.md` und `CONTRIBUTING-en.md` in der Lib, `docs/ansichten/` im Client. `git add -A`
oder `git add .` zieht sie mit hinein, und es faellt erst beim Push oder gar nicht auf.

Blockiert nur das *pauschale* Stagen. `git add <pfad>` bleibt erlaubt -- das ist der Weg, den dieses
Projekt vorsieht.
"""

import json
import re
import sys

# git add -A / --all / . -- jeweils als eigenstaendiges Argument, damit "git add -Api" oder ein
# Pfad wie "./assets" nicht faelschlich anschlaegt.
PAUSCHAL = re.compile(r"\bgit\s+add\b[^|;&]*?(?:\s(?:-A|--all)(?=\s|$)|\s\.(?=\s|$))")


def main() -> int:
    try:
        eingabe = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        # Laesst sich die Eingabe nicht lesen, blockieren wir nichts -- ein Waechter, der bei
        # unerwarteter Eingabe die Arbeit anhaelt, richtet mehr Schaden an als er verhindert.
        return 0

    befehl = eingabe.get("tool_input", {}).get("command", "")

    if not PAUSCHAL.search(befehl):
        return 0

    print(
        "Pauschales Stagen ist in diesem Projekt blockiert.\n"
        "Beide Repos tragen untrackte Dateien, die nicht dazugehoeren "
        "(CONTRIBUTING*.md in der Lib, docs/ansichten/ im Client).\n"
        "Stage stattdessen die Pfade, die du tatsaechlich geaendert hast: git add <pfad> ...\n"
        "Was offen ist, zeigt: git status --porcelain",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    sys.exit(main())
