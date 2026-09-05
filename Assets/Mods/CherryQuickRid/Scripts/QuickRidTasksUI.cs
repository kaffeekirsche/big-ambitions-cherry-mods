#nullable enable
using Helpers;
using Localizor.LanguageChangeEvent;
using Streets;
using UI;
using UI.Tasks;
using UnityEngine;

namespace CherryQuickRid
{
    /// <summary>
    /// Eintrag im Aufgabenpanel, solange der Spieler online ist.
    /// Vorlage: _reference/BeATaxi~/BeATaxi/TaxiTasksUI.cs
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
                    Arguments = new { stars = QuickRidRating.FormatStars(stars) }
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
        /// CloseButton mit <see cref="OnClickCancelJob"/>. Anders als Be A Taxi lassen wir diesen
        /// Button sichtbar – er ist unser "Offline gehen".
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

            // Kein Häkchen und kein Sprung-zur-Adresse-Button: der Kartenpin sitzt schon richtig.
            HideEntryDecorations(_addressEntry);
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
            Transform checkmark = entry.Find("Checkmark");
            if (checkmark != null)
                checkmark.gameObject.SetActive(false);

            Transform destinationButton = entry.Find("DestinationButton");
            if (destinationButton != null)
                destinationButton.gameObject.SetActive(false);
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
