# Projekt: Big-Ambitions-Modding-SDK (Fork Dudeldups/big-ambitions-mods)

- Unity 2022.3.62f2, HDRP, C#, Editor: VS Code. Bauen über Unity-Menü **Big Ambitions → Mod Builder → Build & Install**.
- Test-Spielstand: "ModTest" (Charakter Kaffeekirsche). Hauptspielstand bleibt modfrei.
- Game-DLLs sind importiert; `defineConstraints: BA_GAME_DLLS_IMPORTED` in jeder asmdef.

## Mods in diesem Projekt
- `Assets/Mods/CherryRetailPack` – fertige Retail-Mod (Bäckerei, Tierfutter, Apotheke). **Nicht anfassen**, nur als Strukturvorlage lesen.
- `Assets/Mods/CherryQuickRid` – neue Fahrdienst-Mod "QuickRid" (aktuelles Projekt). Stufenplan in `IDEEN.md` dort.

## Referenzen (Ordner mit `~` werden von Unity ignoriert)
- `_reference/BeATaxi~` – decompilierte Workshop-Mod "Be A Taxi" (NOSTY); Vorlage für Fahrgast-Spawn, Mission, Abrechnung.
- `_reference/BeATaxi_API-Analyse.md` – Kurzfassung der benutzten Game-APIs. **Zuerst lesen.**
- `_reference/GameSource~` – decompilierte Game-Assemblies (Signaturen nachschlagen, nie raten).
- SDK-Beispiele: `Assets/Mods/Example-Options` (Options), `GoodbyeIRS` (modData-Speicherung, MonoBehaviour-Runtime), `Pink` (CityLoad-Muster).

## Konventionen
- ModId = Ordnername = asmdef-Name (`CherryQuickRid`), Namespace `CherryQuickRid`, Klassen-Präfix `QuickRid`.
- Locale-Keys `quickrid_*` in `Locales/en.json` und `de.json` – jeder neue Key in **beide** Dateien.
- modData-Keys `quickrid:*`.
- Keine neuen AssetBundles, solange nicht nötig (Icon kommt später).
- Jede Stufe einzeln: Code → Build → Test im Spiel → erst dann nächste Stufe. Vor Game-API-Aufrufen Signatur in `_reference/GameSource~` prüfen.
- Kein Umbau von CherryRetailPack, keine Änderungen an SDK-Beispielen.
