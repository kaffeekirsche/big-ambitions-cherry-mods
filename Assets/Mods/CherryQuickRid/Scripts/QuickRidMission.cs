#nullable enable
using System;
using Player.PlayerMissions;

namespace CherryQuickRid
{
    /// <summary>
    /// Laufende QuickRid-Schicht. Liegt als <see cref="PlayerMission"/> im Missions-Slot
    /// <c>SaveGameManager.Current.currentPlayerMission</c> und wird mit dem Spielstand gespeichert.
    /// Vorlage: _reference/BeATaxi~/BeATaxi/TaxiMission.cs
    /// </summary>
    /// <remarks>
    /// <see cref="PlayerMission"/> hat keine abstrakten Member – die Klasse ist ein reiner Datenbehälter.
    /// Fahrgast- und Abrechnungsfelder kommen in Stufe 3/4 dazu.
    /// </remarks>
    [Serializable]
    public sealed class QuickRidMission : PlayerMission
    {
        /// <summary>Id der VehicleInstance, mit der der Spieler online gegangen ist.</summary>
        public string? vehicleId;
    }
}
