#nullable enable
using BAModAPI;
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
        private ModContext? _context;
        private bool _isOnline;

        public void Initialize(ModContext context)
        {
            _context = context;
        }

        private void OnDestroy()
        {
            // TODO Stufe 2: GlobalEvents.onEnterVehicle / onExitVehicle / onNewHour wieder abmelden.
        }

        private void Update()
        {
            if (!_isOnline)
                return;

            // TODO Stufe 2: Wenn Spieler im eigenen Auto sitzt (VehicleHelper.IsInsideMotorVehicle,
            //               VehicleHelper.GetCurrentVehicle()) → Auftrag anbieten (HudConfirm.Show).
            // TODO Stufe 3: Fahrgast an Gebäudetür spawnen (PrefabHelper "Characters/HumanDefinitionLow",
            //               CityManager.cityBuildingControllers → entranceDoors, NavMesh.SamplePosition).
            // TODO Stufe 3: Pickup-Radius prüfen, Fahrgast "einsteigen" lassen, Ziel per GuidersManager.SetGuiderTarget.
            // TODO Stufe 4: Ankunft (Distanz + Rigidbody.velocity ≈ 0) → Fahrpreis + Sterne → GameManager.ChangeMoneySafe.
            // TODO Stufe 5: Tagesabschluss via DeliveryDriverMission + DailySummary.RunDeliveryJobSummary.
        }

        public void GoOnline()
        {
            _isOnline = true;
            _context?.Logger.Info("QuickRid: driver online.");
        }

        public void GoOffline()
        {
            _isOnline = false;
            _context?.Logger.Info("QuickRid: driver offline.");
        }
    }
}
