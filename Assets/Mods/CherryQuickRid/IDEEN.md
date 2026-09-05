# QuickRid – Ideen & Stufenplan

Fahrdienst mit dem eigenen Auto als vierter Einstiegsjob (neben Kasse, Essenslieferung, Botenfahrten).
Kein Depot, kein Firmenwagen, keine Schicht – man geht online, nimmt ein paar Fahrten mit, geht offline.

## Stufen
1. **Gerüst** – Mod lädt, Options-Eintrag erscheint (dieses Startpaket). ✔ wenn im Spiel sichtbar
2. **Online gehen** – Interaktion am eigenen Auto (CTA wie bei Be A Taxi) oder Handy/Kontakt; `RideMission : PlayerMission` wird `GameInstance.currentPlayerMission`
3. **Fahrgast** – zufällige Gebäudetür in Reichweite, NPC spawnen (`Characters/HumanDefinitionLow`), Marker, einsteigen im Pickup-Radius, Ziel = andere Adresse (min/max Distanz)
4. **Ankunft & Bezahlung** – Distanz + Stillstand, Fahrpreis = Grundpauschale + Betrag/Meter × Multiplikator × Rating-Modifier, Sterne berechnen, `GameManager.ChangeMoneySafe`
5. **Abschluss** – Tagesübersicht wie Lieferjob (`DeliveryDriverMission` → `DailySummary.RunDeliveryJobSummary`), Rating im Savegame (`modData`)
6. **Feinschliff** – Peak/Nacht-Tarif (`TimeHelper.IsInHourRange`), Trinkgeld, Kartenfilter unter Jobs, Icon, Balancing gegen Vanilla-Lieferjob

## Balancing-Ziel
Eine typische Fahrt ≈ ein Essenslieferauftrag. Flexibler, nicht lukrativer. Aufwärtspotenzial nur über Rating und Auto.

## Offene Fragen
- Handy-App vs. Auto-CTA als Einstieg?
- Fahrzeugklasse als Tariffaktor (Kleinwagen/Limousine)?
- Wie speichert Be A Taxi die laufende Mission ins Savegame? (in `_reference/BeATaxi~` nachsehen)
