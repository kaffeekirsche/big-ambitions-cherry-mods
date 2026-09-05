# QuickRid – Ideen & Stufenplan

Fahrdienst mit dem eigenen Auto als vierter Einstiegsjob (neben Kasse, Essenslieferung, Botenfahrten).
Kein Depot, kein Firmenwagen, keine Schicht – man geht online, nimmt ein paar Fahrten mit, geht offline.

## Stufen
1. **Gerüst** ABGESCHLOSSEN – Mod lädt, Options-Eintrag erscheint (dieses Startpaket). ✔ wenn im Spiel sichtbar
2. **Online gehen** ABGESCHLOSSEN – Button im Fahrzeug-Panel (neben „Parken“), sichtbar nur im eigenen Auto; Bestätigungsdialog; `QuickRidMission : PlayerMission` wird `SaveGameManager.Current.currentPlayerMission`, Aufgabenpanel zeigt „Auf eine Fahrtanfrage warten“. Offline geht über denselben Button oder das X im Panel. ✔ wenn der Button nur im eigenen Auto erscheint und das Panel auf-/zugeht
3. **Fahrgast** – Anfrage wird nach zufälliger Wartezeit (Spielzeit) generiert, der Annehmen/Ablehnen-Dialog erscheint aber erst, wenn das Auto steht oder Schrittgeschwindigkeit fährt (bis dahin zeigt das Panel „Neue Anfrage – anhalten zum Annehmen“). Eine nicht angenommene Anfrage verfällt lautlos nach `offer_timeout` (Startwert 20 Spielminuten). Nach Annahme: NPC an der Abholtür spawnen, Kartenpin, einsteigen im Pickup-Radius, Ziel = andere Adresse (min/max Distanz). Fahrtstatus und Adressen liegen in `QuickRidMission`; beim Laden mit laufender Fahrt wird sie abgebrochen (Wiederherstellung = Stufe 5). Noch kein Geld.
   - Abhol- und Zieladresse stehen schon bei der Generierung fest, nicht erst nach der Annahme – der Dialog nennt beide plus Fahrpreis.
   - Fahrgast-Prefab ist `Characters/DummyHuman` (`BaseHuman`), **nicht** `Characters/HumanDefinitionLow`: letzteres ist ein `ThirdPersonCharacter` mit Update-Schleife, NavMeshAgent und Rigging. `DummyHuman` ist das, was das Spiel selbst für stehende NPCs nimmt (`SellerStandController`, `BaseHumanPool`). Das Prefab wird inaktiv ausgeliefert – `SetActive(true)` nicht vergessen, sonst bleibt der Fahrgast unsichtbar (dieser Fehler steckt in der Be-A-Taxi-Vorlage).
   - Aussehen wird nur einmal beim Anlegen gewürfelt. `AppearanceSetter` baut dabei ein Runtime-Mesh, und der SkinnedMeshCombiner ist Fremdcode ohne Quelle – ob ein erneutes Würfeln das alte Mesh freigibt, ist nicht nachprüfbar. `BaseHumanPool` im Spiel macht es genauso.
4. **Ankunft & Bezahlung** – Distanz + Stillstand, Fahrpreis = Grundpauschale + Betrag/Meter × Multiplikator × Rating-Modifier, Sterne berechnen, `GameManager.ChangeMoneySafe`
   - Die feste 24-Stunden-`endTime` ist weg: `PlayerMission.IsOngoing()` ist **nicht virtual**, überschreiben geht also nicht. Stattdessen liegt `endTime` zehn Jahre in der Zukunft (`timeLimitMinutes = 0`); kein Spielcode wertet die Frist einer fremden Missionsklasse aus, alle `IsOngoing`-Aufrufe im Spiel sind typspezifisch (FoodDelivery, DeliveryDriver). Alte Spielstände werden in `RestoreState` migriert.
   - Rating-Modifier steckt schon im Angebotspreis (`TryCreateRequest`), ausgezahlt wird `mission.fare` unverändert – der Preis aus dem Dialog muss dem gezahlten entsprechen. Ohne bewertete Fahrt gilt Modifier 1,0.
   - Zeitfenster: Fahrzeit zählt ab Einstieg (`boardingTime`), Soll = (`BaseMinutes` 15 + Luftlinie / 8 m pro **Spielminute**) × Zeitpuffer. Die Spieluhr läuft bei 1× mit einer Spielminute pro Realsekunde (`GameManager.RunMainGameTick`), Autos fahren in Realzeit – 8 m/Spielminute ist also 8 m/s Realzeit. Die Grundzeit deckt wie in Vanilla das ab, was auf jeder Fahrt gleich anfällt (wenden, Parklücke suchen); die Essenslieferung rechnet dafür 60 Minuten, dort aber für einen Fußweg.
   - Anzeige: Der Angebotsdialog nennt das Zeitfenster (`{minutes}` in `quickrid_offer_body`), mit Fahrgast an Bord zeigt das Aufgabenpanel statt des Statustexts die Restzeit als `HH:MM`-Countdown (nach Ablauf mit Minuszeichen) plus eine Sterne-Vorschau fürs sofortige Absetzen. Beides kommt aus `QuickRidController.TryGetTripPreview`, damit Vorschau und Abrechnung dieselbe Rechnung benutzen.
   - Schaden = Zuwachs von `VehicleInstance.damage` seit dem Einstieg; unter 1 % kein Abzug (`MinDamageForPenalty`).
   - Historie (letzte 10) und Zähler liegen in `QuickRidMission` **und** dauerhaft in `modData` (`quickrid:rating_history`, `quickrid:stats`, siehe `QuickRidStats`): Laden beim Online-Gehen und in `RestoreState`, Zurückschreiben nach jeder Fahrt und beim Offline-Gehen. Offline gehen mit Fahrgast an Bord = 1 Stern; der Lade-Abbruch kostet keinen Stern.
   - EconoView zeigt den Transaktionstyp doppelt: Beschreibung = `quickrid_transaction`, Typ-Spalte/Filter = derselbe Key + `_label` → `quickrid_transaction_label` ist Pflicht.
5. **Abschluss** – Tagesübersicht wie Lieferjob (`DeliveryDriverMission` → `DailySummary.RunDeliveryJobSummary`); Rating liegt seit Stufe 4 in `modData`, hier nur noch in die Übersicht aufnehmen. Wiederherstellung einer laufenden Fahrt beim Laden.
6. **Feinschliff** – Peak/Nacht-Tarif (`TimeHelper.IsInHourRange`), Trinkgeld, Kartenfilter unter Jobs, Icon, Balancing gegen Vanilla-Lieferjob

## Balancing-Ziel
QuickRid liegt zwischen den beiden Vanilla-Einstiegsjobs: mehr als eine Essenslieferung, weniger als eine Lieferfahrer-Tour. Aufwärtspotenzial nur über Rating und Auto.

Vanilla-Werte aus `_reference/GameSource~` (Stand Stufe 4):

| | Essenslieferung | Lieferfahrer | QuickRid |
|---|---|---|---|
| Geld | 30 $ + 0,08 $/m, max. 600 m | 100 $ je Ziel × 3 Ziele | 30 $ + 0,10 $/m |
| 300 m | 54 $ | – | 60 $ |
| 600 m | 78 $ | – | 90 $ |
| 1500 m | nicht möglich | ~300 $ für die ganze Tour | 180 $ |
| Zeit | 60 + 0,45 min/m, 60–360 | Gesamtstrecke × `minutesPerMeter` (Asset) | (15 + 0,125 min/m) × 150 % |
| Überschreitung | Abbruch ohne Geld | folgenlos, offene Ziele verfallen | Sternabzug, Geld bleibt |
| Trinkgeld | 1 Wurf je Lieferung | 1 Wurf je Ziel | erst Stufe 6 |
| Schaden | – | Reparaturkosten vom Lohn | 1 Stern ab 1 % |

Fundstellen: `FoodDeliveryJobConfig` (baseReward, rewardPerMeter, destinationRadius, baseTimeMinutes, minutesPerMeter), `FoodDeliveryJobHelper.TryCreateOfferAt`, `DeliveryJobStartLocation.deliveryReward`, `DeliveryJobStartController.PromptJob`, `DeliveryJobVehicle.GiveEarningsAndReset`.

## Offene Fragen
- Fahrzeugklasse als Tariffaktor (Kleinwagen/Limousine)?
- ~~Handy-App vs. Auto-CTA als Einstieg?~~ → entschieden: Button im Fahrzeug-Panel, während man im eigenen Auto sitzt. Ein `ICtaBehavior` am Auto war zuerst gebaut und wurde wieder verworfen: `CtaManager.UpdateCta` bricht beim ersten Treffer ab, dadurch verdrängt jeder eigene CTA das vanilla „Klicken zum Fahren“ – man kommt offline nicht mehr ins eigene Auto. Ein Fahrzeug-CTA ist für diese Mod also generell keine Option.
- ~~Wie speichert Be A Taxi die laufende Mission ins Savegame?~~ → gar nicht gesondert: die Mission liegt als `PlayerMission`-Unterklasse im `GameInstance` und wird mit dem Spielstand serialisiert. Voraussetzung ist `[Serializable]` auf der Klasse.

## Release-Hinweise
- **Vor dem Deinstallieren der Mod offline gehen.** Solange man online ist, liegt eine `QuickRidMission`
  im Spielstand. Ohne die Mod kennt das Spiel diesen Typ nicht mehr, und der Spielstand kann beim Laden
  stolpern. Das gehört in die Workshop-Beschreibung.
