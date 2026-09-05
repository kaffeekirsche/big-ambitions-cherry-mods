#nullable enable
using System;
using System.Collections.Generic;
using Helpers;
using Player.HUD.ItemInfoOverlays;

namespace CherryQuickRid
{
    /// <summary>
    /// Zeigt am eigenen Auto den Aufruf "Klicken, um online zu gehen".
    /// Wird von <see cref="QuickRidController"/> per Reflection in <c>CtaManager.CtaBehaviors</c> eingehängt.
    /// Vorlage: TaxiShiftCtaBehavior in _reference/BeATaxi~/BeATaxi/TaxiShiftController.cs
    /// </summary>
    internal sealed class QuickRidCtaBehavior : ICtaBehavior
    {
        public const string CtaKey = "quickrid_start_cta";

        private readonly QuickRidController _controller;

        public QuickRidCtaBehavior(QuickRidController controller)
        {
            _controller = controller;
        }

        /// <remarks>
        /// CtaManager.UpdateCta bricht beim ersten Treffer ab. Solange eine Mission läuft, muss dieses
        /// Behavior deshalb false liefern, damit das vanilla VehicleCtaBehavior wieder "Klicken zum Fahren"
        /// anbietet und der Spieler in sein Auto einsteigen kann.
        /// </remarks>
        public override bool ShouldShow(EntityController entityController)
        {
            // CarController hat im Spielcode keine Unterklassen; HandTruck und ScooterController
            // leiten direkt von VehicleController ab und fallen damit heraus.
            if (!(entityController is CarController car) || car.vehicleInstance == null)
                return false;

            if (SaveGameManager.Current == null || SaveGameManager.Current.currentPlayerMission != null)
                return false;

            if (VehicleHelper.IsInsideMotorVehicle())
                return false;

            return IsOwnedByPlayer(car.vehicleInstance);
        }

        public override (string, Action) GetCta(EntityController entityController)
        {
            if (entityController is CarController car)
                return (CtaKey, () => _controller.RequestGoOnline(car));

            return (string.Empty, null!);
        }

        private static bool IsOwnedByPlayer(VehicleInstance instance)
        {
            List<VehicleInstance> owned = SaveGameManager.Current.VehicleInstances;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].id == instance.id)
                    return true;
            }

            return false;
        }
    }
}
