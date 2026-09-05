#nullable enable
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using BigAmbitions.PlacementSystem;
using Extensions;
using Helpers;
using Localizor.LanguageChangeEvent;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace CherryQuickRid
{
    /// <summary>
    /// Zentrale Laufzeitlogik: Online/Offline, Auftragsgenerierung, Fahrgast, Ankunft, Abrechnung.
    /// Stufenplan siehe IDEEN.md. Vorlage für die Game-API: _reference/BeATaxi~ (TaxiShiftController)
    /// und _reference/BeATaxi_API-Analyse.md.
    /// </summary>
    public sealed class QuickRidController : MonoBehaviour
    {
        /// <summary>
        /// Laufzeit der Schicht. Stufe 2 wertet sie nicht aus; in Stufe 4 fällt die feste Frist weg
        /// (ein Fahrdienst hat keine Schicht) – siehe IDEEN.md.
        /// </summary>
        private const int OnlineDurationMinutes = 1440;

        private const string ButtonName = "QuickRid - Job Button";

        private ModContext? _context;
        private QuickRidTasksUI? _tasksUi;

        private Button? _jobButton;
        private TextLocalizationComponent? _jobButtonLabel;
        private CarController? _currentCar;

        /// <summary>Originalzustand des geklonten Buttons, Basis fuer beide Farbzustaende.</summary>
        private ColorBlock _defaultColors;
        private Color _defaultGraphicColor;
        private bool _colorsCaptured;

        /// <summary>Die laufende Schicht, oder null wenn der Spieler offline ist.</summary>
        private static QuickRidMission? Mission =>
            SaveGameManager.Current?.currentPlayerMission as QuickRidMission;

        public void Initialize(ModContext context)
        {
            _context = context;
        }

        private void Start()
        {
            _tasksUi = new QuickRidTasksUI(this);
            GlobalEvents.onEnterVehicle += OnEnterVehicle;
            GlobalEvents.onExitVehicle += OnExitVehicle;
            GlobalEvents.onPause += OnPaused;
            GlobalEvents.RegisterOnGameLoadedLateCallback(RestoreState);
        }

        private void OnDestroy()
        {
            GlobalEvents.onEnterVehicle -= OnEnterVehicle;
            GlobalEvents.onExitVehicle -= OnExitVehicle;
            GlobalEvents.onPause -= OnPaused;

            if (_jobButton != null)
                Destroy(_jobButton.gameObject);

            _jobButton = null;
            _jobButtonLabel = null;
            _currentCar = null;

            _tasksUi?.Dispose();
            _tasksUi = null;

            // TODO Stufe 3: GlobalEvents.onNewHour wieder abmelden.
        }

        private void Update()
        {
            if (Mission == null)
                return;

            // TODO Stufe 3: Fahrgast an Gebäudetür spawnen (PrefabHelper "Characters/HumanDefinitionLow",
            //               CityManager.cityBuildingControllers → entranceDoors, NavMesh.SamplePosition).
            // TODO Stufe 3: Pickup-Radius prüfen, Fahrgast "einsteigen" lassen, Ziel per GuidersManager.SetGuiderTarget.
            // TODO Stufe 4: Ankunft (Distanz + Rigidbody.velocity ≈ 0) → Fahrpreis + Sterne → GameManager.ChangeMoneySafe.
            // TODO Stufe 5: Tagesabschluss via DeliveryDriverMission + DailySummary.RunDeliveryJobSummary.
        }

        /// <summary>
        /// Nach dem Laden eines Spielstands: Aufgabenpanel wiederherstellen und den Button nachziehen,
        /// falls der Spieler bereits im Auto sitzt (dann ist onEnterVehicle schon durch).
        /// </summary>
        private void RestoreState()
        {
            if (_tasksUi != null && Mission != null)
                _tasksUi.Init();

            VehicleController current = VehicleHelper.GetCurrentVehicleBase();
            if (current != null)
                OnEnterVehicle(current);
        }

        // --- Button im Fahrzeug-Panel --------------------------------------------

        private void OnEnterVehicle(VehicleController vehicle)
        {
            // CarController hat im Spielcode keine Unterklassen; ScooterController und HandTruck
            // leiten direkt von VehicleController ab und fallen damit heraus.
            _currentCar = vehicle is CarController car && IsOwnedByPlayer(car) ? car : null;

            if (_currentCar == null)
            {
                SetJobButtonVisible(false);
                return;
            }

            EnsureJobButton();
            UpdateJobButton();
        }

        private void OnExitVehicle(VehicleController vehicle)
        {
            _currentCar = null;
            SetJobButtonVisible(false);
        }

        /// <remarks>
        /// Klon des vanilla "Parken"-Buttons, direkt daneben einsortiert. Der geklonte Button bringt den
        /// Park-Listener mit – deshalb muss onClick komplett ersetzt und nicht nur ergänzt werden.
        /// Vorlage: EnsureFinishShiftButton in _reference/BeATaxi~/BeATaxi/TaxiShiftController.cs
        /// </remarks>
        private void EnsureJobButton()
        {
            if (_jobButton != null)
                return;

            Button parkButton = InstanceBehavior<UIs>.Instance.playerHUD.itemPanelUI.parkButton;
            if (parkButton == null)
            {
                _context?.Logger.Warn("QuickRid: ItemPanelUI.parkButton nicht gefunden – kein Job-Button im Fahrzeug-Panel.");
                return;
            }

            _jobButton = Instantiate(parkButton, parkButton.transform.parent);
            _jobButton.name = ButtonName;
            _jobButton.onClick = new Button.ButtonClickedEvent();
            _jobButton.onClick.AddListener(OnClickJobButton);
            _jobButton.transform.SetSiblingIndex(parkButton.transform.GetSiblingIndex());

            _jobButtonLabel = _jobButton.transform.GetLanguageChangeEventByName("Label");
            _jobButtonLabel.Suffix = string.Empty;

            _defaultColors = _jobButton.colors;
            if (_jobButton.targetGraphic != null)
                _defaultGraphicColor = _jobButton.targetGraphic.color;
            _colorsCaptured = true;

            _jobButton.gameObject.SetActive(false);

            // onPause feuert nur bei Wechsel; der Button entsteht erst beim Einsteigen.
            ApplyPausedState(InstanceBehavior<UIs>.Instance.gameSpeed.Paused);
        }

        /// <remarks>
        /// Spiegelt ItemPanelUI.OnPaused: der geklonte Button soll bei pausiertem Spiel genauso
        /// ausgegraut sein wie das benachbarte "Parken", im Platzierungsmodus aber bedienbar bleiben.
        /// </remarks>
        private void OnPaused(bool paused)
        {
            ApplyPausedState(paused);
        }

        private void ApplyPausedState(bool paused)
        {
            if (PlacementSystem.IsInPlacementMode)
                paused = false;

            if (_jobButton != null)
                _jobButton.interactable = !paused;
        }

        /// <summary>
        /// Sichtbar nur im eigenen Auto: "Online gehen" wenn gar keine Mission läuft, "Offline gehen"
        /// wenn die eigene Schicht mit genau diesem Auto läuft. Bei einer fremden Mission (z. B.
        /// Lieferfahrer) bleibt der Button weg.
        /// </summary>
        private void UpdateJobButton()
        {
            if (_jobButton == null || _jobButtonLabel == null)
                return;

            if (_currentCar == null || _currentCar.vehicleInstance == null || SaveGameManager.Current == null)
            {
                SetJobButtonVisible(false);
                return;
            }

            QuickRidMission? mission = Mission;
            bool online = mission != null;
            bool show = online
                ? mission!.vehicleId == _currentCar.vehicleInstance.id
                : SaveGameManager.Current.currentPlayerMission == null;

            SetJobButtonVisible(show);

            if (!show)
                return;

            _jobButtonLabel.Key = online ? "quickrid_go_offline" : "quickrid_go_online";
            ApplyJobButtonColors(online);
        }

        /// <summary>
        /// Offline: Standardfarben des Park-Buttons. Online: <c>Colors.Lime</c> aus der Spiel-Palette,
        /// damit der aktive Zustand sofort auffaellt.
        /// </summary>
        /// <remarks>
        /// Die Palette (Colors.cs) fuehrt drei Gruenstufen: darkGreen, green und lime. Dass lime die
        /// hellere Variante von green ist, zeigt SecurityActionPanelUi: rot -> orange -> green -> lime.
        /// <para>
        /// disabledColor, colorMultiplier und fadeDuration bleiben unangetastet aus dem Original –
        /// so graut ItemPanelUI.OnPaused den Button weiterhin genau wie "Parken" aus (dunkelblau).
        /// </para>
        /// </remarks>
        private void ApplyJobButtonColors(bool online)
        {
            if (_jobButton == null || !_colorsCaptured || _jobButton.targetGraphic == null)
                return;

            if (!online)
            {
                _jobButton.colors = _defaultColors;
                _jobButton.targetGraphic.color = _defaultGraphicColor;
                return;
            }

            ColorBlock block = _defaultColors;
            block.normalColor      = Color.white;
            block.selectedColor    = Color.white;
            block.highlightedColor = new Color(1f, 1f, 1f, 1f) * 1.2f;   // leicht heller bei Hover
            block.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f); // leicht dunkler bei Klick
            // disabledColor bleibt aus dem Original → Pause graut weiterhin aus

            _jobButton.colors = block;
            _jobButton.targetGraphic.color = Colors.Lime;
        }

        private void SetJobButtonVisible(bool visible)
        {
            if (_jobButton != null)
                _jobButton.gameObject.SetActive(visible);
        }

        private void OnClickJobButton()
        {
            if (Mission != null)
                PromptGoOffline();
            else
                RequestGoOnline();
        }

        private static bool IsOwnedByPlayer(CarController car)
        {
            if (car.vehicleInstance == null || SaveGameManager.Current == null)
                return false;

            List<VehicleInstance> owned = SaveGameManager.Current.VehicleInstances;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].id == car.vehicleInstance.id)
                    return true;
            }

            return false;
        }

        // --- Online / Offline ----------------------------------------------------

        private void RequestGoOnline()
        {
            if (SaveGameManager.Current.currentPlayerMission != null)
            {
                Notifications.ShowError("notification_already_ongoing_mission");
                return;
            }

            if (PlayerHelper.IsHoldingItem)
            {
                Notifications.ShowError("notification_need_empty_hands_to_interact");
                return;
            }

            CarController? car = _currentCar;
            if (car == null || HudConfirm.isOpen)
                return;

            HudConfirm.Show(
                "quickrid_job_title",
                "quickrid_start_job",
                () => GoOnline(car),
                null,
                "quickrid_accept_job",
                "quickrid_decline_job");
        }

        /// <summary>Bestätigungsdialog vor dem Offline-Gehen – für den Fahrzeug-Button und das Aufgabenpanel.</summary>
        public void PromptGoOffline()
        {
            if (Mission == null || HudConfirm.isOpen)
                return;

            HudConfirm.Show(
                "quickrid_job_title",
                "quickrid_go_offline_confirm",
                GoOffline,
                null,
                "quickrid_go_offline",
                "quickrid_decline_job");
        }

        public void GoOnline(CarController car)
        {
            if (car == null || car.vehicleInstance == null || SaveGameManager.Current.currentPlayerMission != null)
                return;

            Timestamp endTime = TimeHelper.Now();
            endTime.AddMinutes(OnlineDurationMinutes);

            SaveGameManager.Current.currentPlayerMission = new QuickRidMission
            {
                vehicleId = car.vehicleInstance.id,
                startTime = TimeHelper.Now(),
                endTime = endTime,
                timeLimitMinutes = OnlineDurationMinutes
            };

            if (InstanceBehavior<UIs>.Instance.tasksUI.IsCollapsed)
                InstanceBehavior<UIs>.Instance.tasksUI.SetCollapsedState(false);

            _tasksUi?.Init();
            UpdateJobButton();
            _context?.Logger.Info("QuickRid: driver online.");
        }

        public void GoOffline()
        {
            if (Mission == null)
                return;

            SaveGameManager.Current.currentPlayerMission = null;
            _tasksUi?.Hide();
            UpdateJobButton();
            _context?.Logger.Info("QuickRid: driver offline.");
        }
    }
}
