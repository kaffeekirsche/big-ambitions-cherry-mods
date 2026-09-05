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

        /// <summary>Zuletzt gesetzter Zeit-Label-Key, damit nicht dreimal pro Sekunde neu gesetzt wird.</summary>
        private string? _lastStatusKey;

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
                    SetStatus("quickrid_tasks_dropoff");
                    ShowAddress(mission.destinationAddress);
                    break;
            }
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
            Transform checkmark = _addressEntry.Find("Checkmark");
            if (checkmark != null)
                checkmark.gameObject.SetActive(false);

            Transform destinationButton = _addressEntry.Find("DestinationButton");
            if (destinationButton != null)
                destinationButton.gameObject.SetActive(false);

            _addressEntry.gameObject.SetActive(false);

            InstanceBehavior<UIs>.Instance.tasksUI.ScheduleUpdateObjectivesHeight();
        }

        /// <summary>Setzt den Statustext nur, wenn er sich wirklich geändert hat.</summary>
        private void SetStatus(string key)
        {
            if (_lastStatusKey == key || timeLabel == null)
                return;

            timeLabel.SetData(new LanguageChangeEventDataHolder { Key = key });
            _lastStatusKey = key;
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
            _lastStatusKey = null;
            _lastAddress = null;
            _lastDistanceDecimeters = -1;
        }

        protected override void OnClickCancelJob()
        {
            _controller.PromptGoOffline();
        }
    }
}
