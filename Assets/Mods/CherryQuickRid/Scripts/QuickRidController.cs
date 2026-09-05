#nullable enable
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using Player.HUD.ItemInfoOverlays;
using UI;
using UI.Notification;
using UnityEngine;

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

        private ModContext? _context;
        private QuickRidTasksUI? _tasksUi;
        private QuickRidCtaBehavior? _ctaBehavior;

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
            RegisterCtaBehavior();
            GlobalEvents.RegisterOnGameLoadedLateCallback(RestoreTasksUi);
        }

        private void OnDestroy()
        {
            UnregisterCtaBehavior();
            _tasksUi?.Dispose();
            _tasksUi = null;

            // TODO Stufe 3: GlobalEvents.onEnterVehicle / onExitVehicle / onNewHour wieder abmelden.
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

        /// <summary>Stellt das Aufgabenpanel wieder her, wenn ein Spielstand im Online-Zustand geladen wird.</summary>
        private void RestoreTasksUi()
        {
            if (_tasksUi != null && Mission != null)
                _tasksUi.Init();
        }

        // --- CTA am eigenen Auto -------------------------------------------------

        /// <remarks>
        /// CtaManager.CtaBehaviors ist privat und wird in fester Reihenfolge durchlaufen, wobei beim
        /// ersten Treffer abgebrochen wird. Unser Behavior muss deshalb vor VehicleCtaBehavior liegen,
        /// sonst gewinnt immer "Klicken zum Fahren".
        /// </remarks>
        private void RegisterCtaBehavior()
        {
            List<ICtaBehavior>? behaviors = GetCtaBehaviors();
            if (behaviors == null)
            {
                _context?.Logger.Warn(
                    "QuickRid: CtaManager.CtaBehaviors nicht gefunden – der CTA am eigenen Auto bleibt aus.");
                return;
            }

            _ctaBehavior = new QuickRidCtaBehavior(this);
            int index = behaviors.FindIndex(behavior => behavior is VehicleCtaBehavior);
            behaviors.Insert(index >= 0 ? index : behaviors.Count, _ctaBehavior);
        }

        private void UnregisterCtaBehavior()
        {
            if (_ctaBehavior == null)
                return;

            GetCtaBehaviors()?.Remove(_ctaBehavior);
            _ctaBehavior = null;
        }

        private static List<ICtaBehavior>? GetCtaBehaviors()
        {
            return typeof(CtaManager)
                .GetField("CtaBehaviors", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as List<ICtaBehavior>;
        }

        // --- Online gehen --------------------------------------------------------

        /// <summary>Aufgerufen vom CTA: Spieler läuft zum Auto, dann kommt der Bestätigungsdialog.</summary>
        public void RequestGoOnline(CarController car)
        {
            if (SaveGameManager.Current.currentPlayerMission != null)
            {
                Notifications.ShowError("notification_already_ongoing_mission");
                return;
            }

            if (VehicleHelper.IsInsideVehicle())
            {
                Notifications.ShowError("notification_must_exit_vehicle_before_action");
                return;
            }

            if (PlayerHelper.IsHoldingItem)
            {
                Notifications.ShowError("notification_need_empty_hands_to_interact");
                return;
            }

            car.MoveTowardsEntity(() => PromptGoOnline(car));
        }

        /// <remarks>Der Weg zum Auto kostet Spielzeit – die Wächter deshalb erneut prüfen.</remarks>
        private void PromptGoOnline(CarController car)
        {
            if (car == null || SaveGameManager.Current.currentPlayerMission != null || HudConfirm.isOpen)
                return;

            HudConfirm.Show(
                "quickrid_job_title",
                "quickrid_start_job",
                () => GoOnline(car),
                null,
                "quickrid_accept_job",
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
            _context?.Logger.Info("QuickRid: driver online.");
        }

        public void GoOffline()
        {
            if (Mission == null)
                return;

            SaveGameManager.Current.currentPlayerMission = null;
            _tasksUi?.Hide();
            _context?.Logger.Info("QuickRid: driver offline.");
        }
    }
}
