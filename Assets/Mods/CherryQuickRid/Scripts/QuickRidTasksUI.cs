#nullable enable
using System.Reflection;
using BAModAPI;
using Helpers;
using Localizor.LanguageChangeEvent;
using Streets;
using UI;
using UI.Smartphone.Apps.Persona;
using UI.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace CherryQuickRid
{
    /// <summary>
    /// Eintrag im Aufgabenpanel, solange der Spieler online ist.
    /// Vorbild: das Aufgabenpanel des Vanilla-Lieferjobs.
    /// </summary>
    /// <remarks>
    /// Die Basisklasse pollt alle 0,3 s und ruft selbst <c>Hide()</c>, sobald im Missions-Slot
    /// keine <see cref="QuickRidMission"/> mehr liegt – Offline gehen muss die UI also nicht abräumen.
    /// </remarks>
    internal sealed class QuickRidTasksUI : MissionTasksUI<QuickRidMission>
    {
        private readonly QuickRidController _controller;

        private Transform? _addressEntry;
        private TextLocalizationComponent? _addressLabel;

        /// <summary>Meldet die Adresse der laufenden Fahrt als nicht anfahrbar.</summary>
        private Button? _excludeButton;

        /// <summary>Locale-Key des Tooltips am Ausschluss-Button.</summary>
        private const string ExcludeTooltipKey = "quickrid_exclude_tooltip";

        /// <summary>
        /// Das rote X der Zufriedenheitsanzeige, einmal gefunden und gemerkt. Wird beim Verlassen
        /// der Stadt mit dem Smartphone-Prefab zerstört; der Unity-Null-Vergleich merkt das und
        /// die Suche läuft in der nächsten Stadt erneut.
        /// </summary>
        private static Sprite? _happinessNegativeSprite;

        /// <summary>Ergebnis der einmaligen Sprite-Suche; null heißt "nichts gefunden".</summary>
        private static Sprite? _fallbackCrossSprite;

        private static bool _fallbackCrossSearched;

        /// <summary>Sterne-Vorschau für sofortiges Absetzen. Nur mit Fahrgast an Bord sichtbar.</summary>
        private Transform? _previewEntry;
        private TextLocalizationComponent? _previewLabel;

        /// <summary>Dauerhaft sichtbare Zeile mit Fahrerbewertung und Fahrtenzahl.</summary>
        private Transform? _ratingEntry;
        private TextLocalizationComponent? _ratingLabel;

        /// <summary>Zuletzt gesetzter Zeit-Label-Key, damit nicht dreimal pro Sekunde neu gesetzt wird.</summary>
        private string? _lastStatusKey;

        /// <summary>Zuletzt angezeigte Restminuten des Zeitfensters, vorzeichenbehaftet.</summary>
        private int _lastRemainingMinutes = NoCountdown;

        /// <summary>Zuletzt angezeigte Sterne der Vorschau; 0 = noch nichts gesetzt.</summary>
        private int _lastPreviewStars;

        /// <summary>Wert außerhalb jeder echten Restzeit, damit der erste Tick immer schreibt.</summary>
        private const int NoCountdown = int.MinValue;

        /// <summary>"schnitt|fahrten" der letzten Rating-Anzeige – gleiche Idee wie bei <see cref="_lastStatusKey"/>.</summary>
        private string? _lastRatingKey;

        /// <summary>Zuletzt angezeigte Entfernung in Dezimetern – gleiche Idee wie in FoodDeliveryJobUI.</summary>
        private int _lastDistanceDecimeters = -1;

        private Address? _lastAddress;

        public QuickRidTasksUI(QuickRidController controller)
        {
            _controller = controller;
        }

        public void Init()
        {
            UpdateUI();
            StartUpdateRoutine();
        }

        public override void UpdateUI()
        {
            if (!TryGetMission(out QuickRidMission mission))
                return;

            if (tasksGroup == null)
                CreateUI();

            switch (mission.state)
            {
                case QuickRidTripState.Waiting:
                    SetStatus("quickrid_tasks_wait");
                    SetAddressVisible(false);
                    break;

                case QuickRidTripState.Offered:
                    SetStatus("quickrid_tasks_request_pending");
                    SetAddressVisible(false);
                    break;

                case QuickRidTripState.PassengerWaiting:
                    SetStatus("quickrid_tasks_pickup");
                    ShowAddress(mission.pickupAddress);
                    break;

                case QuickRidTripState.PassengerAboard:
                    // Mit Fahrgast an Bord ersetzt der Countdown den Statustext – wie im Vanilla-
                    // Lieferjob, wo dieselbe Zeile die verbleibende Zeit zeigt (FoodDeliveryJobUI).
                    if (!ShowTripCountdown(mission))
                    {
                        // Ohne Einstiegszeitpunkt gibt es nichts vorzurechnen – zurück zum Statustext.
                        SetPreviewVisible(false);
                        SetStatus("quickrid_tasks_dropoff");
                    }
                    ShowAddress(mission.destinationAddress);
                    break;
            }

            if (mission.state != QuickRidTripState.PassengerAboard)
                SetPreviewVisible(false);

            // Melden lässt sich nur, was gerade angefahren wird.
            SetExcludeVisible(mission.state == QuickRidTripState.PassengerWaiting
                || mission.state == QuickRidTripState.PassengerAboard);

            UpdateRating(mission);
        }

        /// <summary>
        /// Schreibt Restzeit und Sterne-Vorschau, solange eine Fahrt läuft. Rückgabe false, wenn es
        /// nichts anzuzeigen gibt – dann bleibt es beim Statustext.
        /// </summary>
        /// <remarks>
        /// Bei normalem Tempo vergeht eine Spielminute pro Realsekunde, der Countdown zählt also
        /// sekündlich herunter. Geschrieben wird trotzdem nur bei echtem Wechsel, sonst baut TMP den
        /// Text dreimal pro Sekunde neu auf. Nach Ablauf des Fensters läuft er mit Minuszeichen weiter
        /// – die Fahrt bleibt gültig, sie kostet nur Sterne.
        /// </remarks>
        private bool ShowTripCountdown(QuickRidMission mission)
        {
            if (timeLabel == null || !_controller.TryGetTripPreview(mission, out float remaining, out int stars))
                return false;

            SetPreviewVisible(true);

            bool overdue = remaining < 0f;
            int minutes = Mathf.CeilToInt(Mathf.Abs(remaining));
            int signed = overdue ? -minutes : minutes;

            if (signed != _lastRemainingMinutes)
            {
                _lastRemainingMinutes = signed;

                // Der Statustext muss danach neu gesetzt werden dürfen, sonst bleibt die Zeile leer.
                _lastStatusKey = null;

                string text = $"{(overdue ? "-" : string.Empty)}{minutes / 60:0#}:{minutes % 60:0#}";

                // clearKey, damit ein Sprachwechsel den Rohtext nicht durch einen alten Key ersetzt.
                timeLabel.SetValue(text, true);
            }

            if (_previewLabel != null && stars != _lastPreviewStars)
            {
                _lastPreviewStars = stars;
                _previewLabel.SetData(new LanguageChangeEventDataHolder
                {
                    Key = "quickrid_rating_preview",
                    // Farbig, weil CreateAddressEntry für diese Zeile Rich Text einschaltet.
                    Arguments = new { stars = QuickRidRating.FormatStarsColored(stars) }
                });
            }

            return true;
        }

        /// <remarks>Höhe nur beim echten Wechsel nachziehen – siehe <see cref="SetAddressVisible"/>.</remarks>
        private void SetPreviewVisible(bool visible)
        {
            if (_previewEntry == null || _previewEntry.gameObject.activeSelf == visible)
                return;

            _previewEntry.gameObject.SetActive(visible);

            if (!visible)
            {
                _lastPreviewStars = 0;
                _lastRemainingMinutes = NoCountdown;
            }

            InstanceBehavior<UIs>.Instance.tasksUI.ScheduleUpdateObjectivesHeight();
        }

        /// <remarks>
        /// CreateTimeEntry blendet Checkmark und DestinationButton selbst aus und verdrahtet den
        /// CloseButton mit <see cref="OnClickCancelJob"/>. Anders als im Vanilla-Lieferjob bleibt
        /// dieser Button sichtbar – er ist unser "Offline gehen".
        /// <para>
        /// Die Adresszeile wird hier einmal angelegt und danach nur noch ein- und ausgeblendet.
        /// Ein zweiter <c>CreateTasksGroup</c>-Aufruf würde eine zusätzliche Gruppe erzeugen, weil
        /// <c>TasksUI.SetUpTasksGroup</c> immer instanziiert und nichts aufräumt.
        /// </para>
        /// </remarks>
        private void CreateUI()
        {
            CreateTasksGroup("quickrid_tasks_header");
            CreateTimeEntry();

            _addressEntry = CreateAddressEntry(string.Empty, out TextLocalizationComponent addressLabel);
            _addressLabel = addressLabel;

            // Kein Häkchen: den Sprung-zur-Adresse-Button braucht der Kartenpin nicht, seine Stelle
            // in der Zeile bekommt stattdessen der Ausschluss-Button. Ist der abgeschaltet, bleibt
            // die Zeile so kahl wie die beiden darunter.
            HideCheckmark(_addressEntry);
            HideDestinationButton(_addressEntry);

            // Der Compiler hält den Aufruf bei abgeschaltetem Knopf für unerreichbar.
#pragma warning disable CS0162
            if (QuickRidSettings.EnableExcludeButton)
                SetUpExcludeButton(_addressEntry);
#pragma warning restore CS0162

            _addressEntry.gameObject.SetActive(false);

            // Zweite Unterzeile: Sterne für ein sofortiges Absetzen, nur während der Fahrt.
            _previewEntry = CreateAddressEntry(string.Empty, out TextLocalizationComponent previewLabel);
            _previewLabel = previewLabel;
            HideEntryDecorations(_previewEntry);
            _previewEntry.gameObject.SetActive(false);

            // Dritte Unterzeile: Fahrerbewertung. Bleibt in jedem Zustand sichtbar.
            _ratingEntry = CreateAddressEntry(string.Empty, out TextLocalizationComponent ratingLabel);
            _ratingLabel = ratingLabel;
            HideEntryDecorations(_ratingEntry);

            InstanceBehavior<UIs>.Instance.tasksUI.ScheduleUpdateObjectivesHeight();
        }

        /// <summary>Häkchen und Sprung-zur-Adresse-Button einer Unterzeile ausblenden.</summary>
        private static void HideEntryDecorations(Transform entry)
        {
            HideCheckmark(entry);
            HideDestinationButton(entry);
        }

        private static void HideDestinationButton(Transform entry)
        {
            Transform destinationButton = entry.Find("DestinationButton");
            if (destinationButton != null)
                destinationButton.gameObject.SetActive(false);
        }

        private static void HideCheckmark(Transform entry)
        {
            Transform checkmark = entry.Find("Checkmark");
            if (checkmark != null)
                checkmark.gameObject.SetActive(false);
        }

        /// <summary>
        /// Widmet den Sprung-zur-Adresse-Button der Adresszeile zum Ausschluss-Button um.
        /// </summary>
        /// <remarks>
        /// Das Aufgabenpanel kennt nur zwei Zeilenvorlagen und keinen Text-Button; der vorhandene
        /// Button ist deshalb der einzige Platz für eine Aktion an der Adresse.
        /// <para>
        /// <c>onClick</c> wird <em>ersetzt</em>, nicht ergänzt: <c>RemoveAllListeners</c> räumt nur
        /// die zur Laufzeit hinzugefügten Zuhörer ab, nicht die aus dem Prefab.
        /// </para>
        /// </remarks>
        private void SetUpExcludeButton(Transform addressEntry)
        {
            Transform destinationButton = addressEntry.Find("DestinationButton");
            if (destinationButton == null)
                return;

            _excludeButton = destinationButton.GetComponent<Button>();
            if (_excludeButton == null)
                return;

            _excludeButton.onClick = new Button.ButtonClickedEvent();
            _excludeButton.onClick.AddListener(OnClickExclude);

            RetargetTooltip(destinationButton);

            // Nur wenn das Symbol wirklich getauscht wurde, wird auch der Hintergrund angefasst –
            // sonst bliebe ein halb umgebauter Knopf stehen.
            Image? icon = TrySwapIcon(destinationButton);
            if (icon != null)
                TryNeutralizeBackground(_excludeButton, icon, _controller.Logger);

            destinationButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Ersetzt den Vanilla-Tooltip "Klicken, um als Ziel festzulegen" durch den eigenen Text.
        /// </summary>
        /// <remarks>
        /// Der Tooltip hängt als <c>BasicTooltip</c> am Button und wird bei jedem Überfahren neu aus
        /// <c>titleKey</c> und <c>descriptionKey</c> aufgebaut – ein Zuweisen der Felder genügt, es
        /// gibt nichts aufzufrischen. Der Vanilla-Key steht nur im Prefab
        /// (<c>bizman_hover_destination_button</c>), nicht im Code.
        /// <para>
        /// <c>descriptionKey</c> wird an Kommas in mehrere Zeilen zerlegt, bleibt hier deshalb leer;
        /// der Text steht in der Überschrift. Findet sich kein <c>BasicTooltip</c>, wird der Tooltip
        /// abgeschaltet – lieber gar keiner als ein falscher.
        /// </para>
        /// </remarks>
        private void RetargetTooltip(Transform destinationButton)
        {
            BasicTooltip basic = destinationButton.GetComponentInChildren<BasicTooltip>(true);
            if (basic != null)
            {
                basic.titleKey = ExcludeTooltipKey;
                basic.descriptionKey = string.Empty;
                return;
            }

            TooltipTarget other = destinationButton.GetComponentInChildren<TooltipTarget>(true);
            if (other == null)
                return;

            other.Hide();
            other.enabled = false;
            QuickRidLog.Dev(_controller.Logger,
                $"QuickRid: Tooltip-Typ {other.GetType().Name} am Ausschluss-Button unbekannt – abgeschaltet.");
        }

        /// <summary>
        /// Tauscht das Kartenpin-Symbol gegen ein X: erste Wahl ist das runde rote Symbol der
        /// Zufriedenheitsanzeige, sonst das X des Schließen-Buttons.
        /// </summary>
        /// <remarks>
        /// Zweite Quelle ist die Zeilenvorlage <c>TasksUI.taskEntryTemplate</c> und nicht ein
        /// erzeugter Zeit-Eintrag: die Vorlage steht, solange das Aufgabenpanel existiert. Eine
        /// eigene Grafik gäbe es nur mit einem zweiten Asset im Bundle.
        /// </remarks>
        /// <returns>Das Bild, in dem jetzt das X steht, oder null wenn nichts getauscht wurde.</returns>
        private Image? TrySwapIcon(Transform destinationButton)
        {
            IModLogger? logger = _controller.Logger;

            Image? targetIcon = FindIcon(destinationButton, "DestinationButton", logger);
            if (targetIcon == null)
            {
                QuickRidLog.Dev(logger, "QuickRid: kein Symbolbild am Ausschluss-Button gefunden – Kartenpin bleibt.");
                return null;
            }

            if (TryApplyHappinessIcon(targetIcon, logger))
                return targetIcon;

            Image? sourceIcon = null;
            Transform? closeButton = FindCloseButtonTemplate();

            if (closeButton != null)
                sourceIcon = FindIcon(closeButton, "CloseButton", logger);
            else
                QuickRidLog.Dev(logger, "QuickRid: CloseButton in der Zeilenvorlage nicht gefunden.");

            Sprite? sprite = sourceIcon != null ? sourceIcon.sprite : FindFallbackCrossSprite(logger);
            if (sprite == null)
            {
                QuickRidLog.Dev(logger, "QuickRid: kein X-Symbol gefunden – Kartenpin bleibt am Ausschluss-Button.");
                return null;
            }

            targetIcon.sprite = sprite;

            // Ein X in einem pinförmigen Feld sähe sonst verzerrt aus.
            if (sourceIcon != null)
            {
                targetIcon.type = sourceIcon.type;
                targetIcon.preserveAspect = sourceIcon.preserveAspect;
            }
            else
            {
                targetIcon.preserveAspect = true;
            }

            QuickRidLog.Dev(logger, $"QuickRid: Ausschluss-Button zeigt jetzt \"{sprite.name}\".");
            return targetIcon;
        }

        /// <summary>
        /// Nimmt dem Ausschluss-Button den orangen Hintergrund. Erste Wahl ist die Optik des
        /// Schließen-X aus der Zeile darüber, sonst wird der Hintergrund unsichtbar geschaltet.
        /// </summary>
        /// <remarks>
        /// Das Orange steckt nicht im Symbol, sondern im Hintergrundbild des Knopfes, und das ist
        /// zugleich sein <c>targetGraphic</c>: die Zustandsfarben des Buttons färben genau dieses
        /// Bild. Ein bloßes Umfärben reichte deshalb nicht – der nächste Zustandswechsel holte das
        /// Orange aus der <c>ColorBlock</c> zurück. Übernommen werden darum Bild <em>und</em>
        /// Übergänge.
        /// <para>
        /// Sitzt das X ausnahmsweise im Hintergrundbild selbst (letzter Zweig von
        /// <see cref="FindIcon"/>), bleibt der Knopf unangetastet, sonst wäre nichts mehr zu sehen.
        /// </para>
        /// </remarks>
        private static void TryNeutralizeBackground(Button? button, Image icon, IModLogger? logger)
        {
            if (button == null)
                return;

            if (!(button.targetGraphic is Image background) || ReferenceEquals(background, icon))
                return;

            Transform? closeButton = FindCloseButtonTemplate();
            Button? closeButtonComponent = closeButton != null ? closeButton.GetComponent<Button>() : null;
            Image? closeBackground = closeButtonComponent != null
                ? closeButtonComponent.targetGraphic as Image
                : null;

            // Hat der Schließen-Button gar kein eigenes Hintergrundbild, ist sein targetGraphic
            // das X selbst – kopiert man das, stünde hinter unserem Kreis ein zweites X.
            if (closeBackground != null && !HasSeparateIcon(closeButton!, closeBackground))
            {
                QuickRidLog.Dev(logger,
                    "QuickRid: Schließen-Button hat kein eigenes Hintergrundbild – Vorlage verworfen.");
                closeBackground = null;
            }

            if (closeBackground != null)
            {
                background.sprite = closeBackground.sprite;
                background.color = closeBackground.color;
                background.type = closeBackground.type;
                background.preserveAspect = closeBackground.preserveAspect;

                button.transition = closeButtonComponent!.transition;
                button.colors = closeButtonComponent.colors;
                button.spriteState = closeButtonComponent.spriteState;

                QuickRidLog.Dev(logger,
                    "QuickRid: Ausschluss-Button übernimmt Hintergrund und Farbübergänge des Schließen-Buttons.");
                return;
            }

            // Ohne Vorlage: Hintergrund unsichtbar. Die Zustandsfarben multiplizieren nur auf die
            // Bildfarbe, Alpha 0 bleibt deshalb in jedem Zustand Alpha 0.
            Color hidden = background.color;
            hidden.a = 0f;
            background.color = hidden;

            QuickRidLog.Dev(logger,
                "QuickRid: kein Schließen-Button als Vorlage – Hintergrund des Ausschluss-Buttons ausgeblendet.");
        }

        /// <summary>Trägt dieser Knopf neben seinem Hintergrund noch ein eigenes Symbolbild?</summary>
        private static bool HasSeparateIcon(Transform button, Image background)
        {
            Image[] images = button.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && !ReferenceEquals(images[i], background))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Setzt das Negativ-Symbol der Zufriedenheitsanzeige auf den Knopf. Rückgabe false, wenn
        /// es nicht zu finden war – dann greift der bisherige Weg über den Schließen-Button.
        /// </summary>
        /// <remarks>
        /// Das Spiel zeichnet dieses Symbol nicht rot, es färbt es rot ein:
        /// <c>CharacterStats.SetUpHappinessModifiers</c> setzt neben <c>sprite</c> auch
        /// <c>Image.color</c> auf <c>GlobalReferences.colors.red</c>. Ohne die Einfärbung bliebe
        /// hier ein blasses Symbol stehen, deshalb wird beides gesetzt.
        /// </remarks>
        private static bool TryApplyHappinessIcon(Image targetIcon, IModLogger? logger)
        {
            Sprite? sprite = FindHappinessNegativeSprite(logger);
            if (sprite == null)
                return false;

            targetIcon.sprite = sprite;

            // Ein rundes Symbol verträgt keine 9-Slice-Streckung des Kartenpin-Feldes.
            targetIcon.type = Image.Type.Simple;
            targetIcon.preserveAspect = true;
            targetIcon.color = Colors.Red;

            QuickRidLog.Dev(logger,
                $"QuickRid: Ausschluss-Button zeigt jetzt \"{sprite.name}\" aus der Zufriedenheitsanzeige.");
            return true;
        }

        /// <summary>
        /// Sucht das Negativ-Symbol, das die Zufriedenheitsanzeige vor Einträge wie "Wet" setzt.
        /// </summary>
        /// <remarks>
        /// <c>CharacterStats.negativeIcon</c> ist ein privates <c>[SerializeField]</c>, das Sprite
        /// steckt also im Prefab und nicht in <c>GlobalReferences</c> – dort liegen nur Verläufe,
        /// Kontakt- und Karten-Symbole. Damit gibt es keinen Sprite-Namen, nach dem sich suchen
        /// ließe; der Weg führt über eine geladene <c>CharacterStats</c>-Instanz und Reflection.
        /// <para>
        /// <c>Resources.FindObjectsOfTypeAll</c> findet die Instanz auch, solange das Smartphone
        /// zugeklappt und damit inaktiv ist. Der gefundene Name steht im Log, damit er sich später
        /// fest eintragen ließe.
        /// </para>
        /// </remarks>
        private static Sprite? FindHappinessNegativeSprite(IModLogger? logger)
        {
            if (_happinessNegativeSprite != null)
                return _happinessNegativeSprite;

            FieldInfo? field = typeof(CharacterStats)
                .GetField("negativeIcon", BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                QuickRidLog.Dev(logger, "QuickRid: CharacterStats.negativeIcon gibt es nicht mehr.");
                return null;
            }

            CharacterStats[] stats = Resources.FindObjectsOfTypeAll<CharacterStats>();
            for (int i = 0; i < stats.Length; i++)
            {
                if (stats[i] == null)
                    continue;

                if (!(field.GetValue(stats[i]) is Sprite sprite) || sprite == null)
                    continue;

                _happinessNegativeSprite = sprite;
                QuickRidLog.Dev(logger,
                    $"QuickRid: Negativ-Symbol \"{sprite.name}\" aus der Zufriedenheitsanzeige übernommen " +
                    $"({stats.Length} CharacterStats geladen).");
                return sprite;
            }

            QuickRidLog.Dev(logger,
                $"QuickRid: kein Negativ-Symbol in {stats.Length} geladenen CharacterStats gefunden.");
            return null;
        }

        /// <summary>Der Schließen-Button der oberen Zeilenvorlage – dort sitzt das X.</summary>
        private static Transform? FindCloseButtonTemplate()
        {
            if (!InstanceBehavior<UIs>.IsInitialized)
                return null;

            TasksUI tasksUi = InstanceBehavior<UIs>.Instance.tasksUI;
            if (tasksUi == null || tasksUi.taskEntryTemplate == null)
                return null;

            return tasksUi.taskEntryTemplate.Find("CloseButton");
        }

        /// <summary>
        /// Das Symbolbild eines Buttons. Erst ein Kind namens "Icon" (die Schreibweise des Spiels,
        /// siehe GameSpeedController und CollapsibleWindow), sonst das einzige Bild, das nicht der
        /// Hintergrund ist, sonst der Hintergrund selbst.
        /// </summary>
        /// <remarks>
        /// Jeder Fund wird protokolliert: ohne Einsicht ins Prefab ist das Log die einzige
        /// Möglichkeit, die tatsächliche Struktur der Zeile zu erfahren. Das ist Diagnose und
        /// erscheint deshalb nur im Entwicklermodus (siehe <see cref="QuickRidLog"/>).
        /// </remarks>
        private static Image? FindIcon(Transform buttonTransform, string label, IModLogger? logger)
        {
            var button = buttonTransform.GetComponent<Button>();
            Graphic? background = button != null ? button.targetGraphic : null;

            Image? named = null;
            Image? single = null;
            bool ambiguous = false;

            Image[] images = buttonTransform.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null)
                    continue;

                bool isBackground = ReferenceEquals(image, background);
                QuickRidLog.Dev(logger, $"QuickRid: {label} -> Bild \"{image.transform.name}\", " +
                    $"Sprite \"{(image.sprite != null ? image.sprite.name : "-")}\", Hintergrund: {isBackground}");

                if (image.transform.name == "Icon" && named == null)
                    named = image;

                if (isBackground || image.sprite == null)
                    continue;

                if (single != null)
                    ambiguous = true;
                else
                    single = image;
            }

            if (named != null)
                return named;
            if (single != null && !ambiguous)
                return single;
            if (single != null)
                return single; // mehrere Kandidaten: der erste ist die bessere Wahl als keiner

            return button != null ? button.image : null;
        }

        /// <summary>
        /// Letzter Ausweg: ein geladenes Vanilla-Sprite, dessen Name nach einem X klingt.
        /// </summary>
        /// <remarks>
        /// Das Spiel führt keine Sammlung mit Symbolen – <c>GlobalReferences</c> hat nur Verläufe,
        /// Kontakt- und Karten-Symbole. Diese Suche ist deshalb geraten und läuft nur einmal; der
        /// gefundene Name landet im Log, damit er beim nächsten Mal fest eingetragen werden kann.
        /// </remarks>
        private static Sprite? FindFallbackCrossSprite(IModLogger? logger)
        {
            if (_fallbackCrossSearched)
                return _fallbackCrossSprite;

            _fallbackCrossSearched = true;

            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite == null || string.IsNullOrEmpty(sprite.name))
                    continue;

                string name = sprite.name.ToLowerInvariant();
                if (!name.Contains("close") && !name.Contains("cross") && !name.Contains("cancel"))
                    continue;

                _fallbackCrossSprite = sprite;
                QuickRidLog.Dev(logger, $"QuickRid: Ersatz-Symbol \"{sprite.name}\" aus den geladenen Sprites gewählt " +
                    $"({sprites.Length} durchsucht).");
                return sprite;
            }

            QuickRidLog.Dev(logger, $"QuickRid: kein X-artiges Sprite unter {sprites.Length} geladenen gefunden.");
            return null;
        }

        /// <remarks>
        /// Ohne <see cref="QuickRidSettings.EnableExcludeButton"/> bleibt <c>_excludeButton</c>
        /// null, der Knopf taucht also in keinem Zustand auf.
        /// </remarks>
        private void SetExcludeVisible(bool visible)
        {
            if (_excludeButton != null && _excludeButton.gameObject.activeSelf != visible)
                _excludeButton.gameObject.SetActive(visible);
        }

        private void OnClickExclude()
        {
            _controller.PromptExcludeAddress();
        }

        /// <remarks>
        /// SetData statt SetValue, damit ein Sprachwechsel die Zeile mit denselben Argumenten neu
        /// rendert. Neu gesetzt wird nur bei Änderung – die Basisklasse ruft UpdateUI alle 0,3 s.
        /// </remarks>
        private void UpdateRating(QuickRidMission mission)
        {
            if (_ratingLabel == null)
                return;

            string rating = QuickRidRating.FormatAverage(mission.GetRatingHistory());
            string key = rating + "|" + mission.completedTrips;
            if (key == _lastRatingKey)
                return;

            _lastRatingKey = key;
            _ratingLabel.SetData(new LanguageChangeEventDataHolder
            {
                Key = "quickrid_rating_label",
                Arguments = new { rating, trips = mission.completedTrips }
            });
        }

        /// <summary>Setzt den Statustext nur, wenn er sich wirklich geändert hat.</summary>
        private void SetStatus(string key)
        {
            if (_lastStatusKey == key || timeLabel == null)
                return;

            timeLabel.SetData(new LanguageChangeEventDataHolder { Key = key });
            _lastStatusKey = key;

            // Ein späterer Countdown muss dieselbe Zeile wieder überschreiben dürfen.
            _lastRemainingMinutes = NoCountdown;
        }

        private void ShowAddress(Address? address)
        {
            if (address == null)
            {
                SetAddressVisible(false);
                return;
            }

            SetAddressVisible(true);

            if (_addressLabel == null)
                return;

            Transform entrance = BuildingHelper.GetAddressEntranceTransform(address);
            float distance = entrance != null
                ? Vector3.Distance(PlayerHelper.GetCityPosition(), entrance.position)
                : 0f;

            // Auf Dezimeter gerundet vergleichen, sonst baut TMP dreimal pro Sekunde neu auf.
            int decimeters = Mathf.RoundToInt(distance * 10f);
            if (address == _lastAddress && decimeters == _lastDistanceDecimeters)
                return;

            _lastAddress = address;
            _lastDistanceDecimeters = decimeters;

            string text = FormatAddressWithDistance(
                AddressHelper.ToFormattedString(address),
                UnitHelper.ToFormattedDistance(distance));

            // clearKey, damit ein Sprachwechsel den Rohtext nicht durch einen alten Key ersetzt.
            _addressLabel.SetValue(text, true);
        }

        /// <remarks>
        /// Die Panelhöhe nur beim echten Wechsel nachziehen: ScheduleUpdateObjectivesHeight startet
        /// jedes Mal eine 0,3-s-Animation, pro Tick aufgerufen würde das Panel dauerhaft zappeln.
        /// </remarks>
        private void SetAddressVisible(bool visible)
        {
            if (_addressEntry == null || _addressEntry.gameObject.activeSelf == visible)
                return;

            _addressEntry.gameObject.SetActive(visible);

            if (!visible)
            {
                _lastAddress = null;
                _lastDistanceDecimeters = -1;
            }

            InstanceBehavior<UIs>.Instance.tasksUI.ScheduleUpdateObjectivesHeight();
        }

        protected override void OnHide()
        {
            _addressEntry = null;
            _addressLabel = null;
            _excludeButton = null;
            _previewEntry = null;
            _previewLabel = null;
            _ratingEntry = null;
            _ratingLabel = null;
            _lastStatusKey = null;
            _lastRatingKey = null;
            _lastAddress = null;
            _lastDistanceDecimeters = -1;
            _lastRemainingMinutes = NoCountdown;
            _lastPreviewStars = 0;
        }

        protected override void OnClickCancelJob()
        {
            _controller.PromptGoOffline();
        }
    }
}
