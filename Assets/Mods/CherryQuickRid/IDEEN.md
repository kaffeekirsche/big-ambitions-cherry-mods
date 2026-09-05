# QuickRid – Ideen & Stufenplan

Fahrdienst mit dem eigenen Auto als vierter Einstiegsjob (neben Kasse, Essenslieferung, Botenfahrten).
Kein Depot, kein Firmenwagen, keine Schicht – man geht online, nimmt ein paar Fahrten mit, geht offline.

## Stufen
1. **Gerüst** – Mod lädt, Options-Eintrag erscheint (dieses Startpaket). ✔ wenn im Spiel sichtbar
2. **Online gehen** – Button im Fahrzeug-Panel (neben „Parken“), sichtbar nur im eigenen Auto; Bestätigungsdialog; `QuickRidMission : PlayerMission` wird `SaveGameManager.Current.currentPlayerMission`, Aufgabenpanel zeigt „Auf eine Fahrtanfrage warten“. Offline geht über denselben Button oder das X im Panel. ✔ wenn der Button nur im eigenen Auto erscheint und das Panel auf-/zugeht
3. **Fahrgast** – zufällige Gebäudetür in Reichweite, NPC spawnen (`Characters/HumanDefinitionLow`), Marker, einsteigen im Pickup-Radius, Ziel = andere Adresse (min/max Distanz)
4. **Ankunft & Bezahlung** – Distanz + Stillstand, Fahrpreis = Grundpauschale + Betrag/Meter × Multiplikator × Rating-Modifier, Sterne berechnen, `GameManager.ChangeMoneySafe`
   - Dabei die feste 24-Stunden-`endTime` aus Stufe 2 abschaffen – ein Fahrdienst hat keine Schicht. Entweder `endTime` weit in die Zukunft setzen oder `IsOngoing()` überschreiben. Ein Zeitlimit gehört dann an die einzelne Fahrt, nicht an die Schicht.
5. **Abschluss** – Tagesübersicht wie Lieferjob (`DeliveryDriverMission` → `DailySummary.RunDeliveryJobSummary`), Rating im Savegame (`modData`)
6. **Feinschliff** – Peak/Nacht-Tarif (`TimeHelper.IsInHourRange`), Trinkgeld, Kartenfilter unter Jobs, Icon, Balancing gegen Vanilla-Lieferjob

## Balancing-Ziel
Eine typische Fahrt ≈ ein Essenslieferauftrag. Flexibler, nicht lukrativer. Aufwärtspotenzial nur über Rating und Auto.

## Offene Fragen
- Fahrzeugklasse als Tariffaktor (Kleinwagen/Limousine)?
- ~~Handy-App vs. Auto-CTA als Einstieg?~~ → entschieden: Button im Fahrzeug-Panel, während man im eigenen Auto sitzt. Ein `ICtaBehavior` am Auto war zuerst gebaut und wurde wieder verworfen: `CtaManager.UpdateCta` bricht beim ersten Treffer ab, dadurch verdrängt jeder eigene CTA das vanilla „Klicken zum Fahren“ – man kommt offline nicht mehr ins eigene Auto. Ein Fahrzeug-CTA ist für diese Mod also generell keine Option.
- ~~Wie speichert Be A Taxi die laufende Mission ins Savegame?~~ → gar nicht gesondert: die Mission liegt als `PlayerMission`-Unterklasse im `GameInstance` und wird mit dem Spielstand serialisiert. Voraussetzung ist `[Serializable]` auf der Klasse.

## Release-Hinweise
- **Vor dem Deinstallieren der Mod offline gehen.** Solange man online ist, liegt eine `QuickRidMission`
  im Spielstand. Ohne die Mod kennt das Spiel diesen Typ nicht mehr, und der Spielstand kann beim Laden
  stolpern. Das gehört in die Workshop-Beschreibung.
