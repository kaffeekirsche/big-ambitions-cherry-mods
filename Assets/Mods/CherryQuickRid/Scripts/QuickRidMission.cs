#nullable enable
using System;
using BigAmbitions.DayNightCycle;
using Player.PlayerMissions;

namespace CherryQuickRid
{
    /// <summary>
    /// Laufende QuickRid-Schicht inklusive der aktuellen Fahrt. Liegt als <see cref="PlayerMission"/>
    /// im Missions-Slot <c>SaveGameManager.Current.currentPlayerMission</c> und wird mit dem
    /// Spielstand gespeichert. Vorlage: _reference/BeATaxi~/BeATaxi/TaxiMission.cs
    /// </summary>
    /// <remarks>
    /// <see cref="PlayerMission"/> hat keine abstrakten Member – die Klasse ist ein reiner Datenbehälter.
    /// <para>
    /// Nur öffentliche Felder, keine Properties: der Spielstand wird je nach Modus mit Odin (binär)
    /// oder Newtonsoft (JSON) geschrieben. Odin ignoriert Properties, Newtonsoft nicht – eine
    /// Property würde also nur in einem der beiden Formate auftauchen.
    /// </para>
    /// <para>
    /// Laufzeit-Referenzen (Auto, gespawnter Fahrgast, gecachte Transforms) gehören bewusst nicht
    /// hierher, sondern in <see cref="QuickRidController"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class QuickRidMission : PlayerMission
    {
        /// <summary>Id der VehicleInstance, mit der der Spieler online gegangen ist.</summary>
        public string? vehicleId;

        /// <summary>Phase der aktuellen Fahrt. Standard ist <see cref="QuickRidTripState.Waiting"/> (= 0).</summary>
        public QuickRidTripState state;

        /// <summary>Adresse, an der der Fahrgast wartet. Null, solange keine Anfrage offen ist.</summary>
        /// <remarks>
        /// Die konkrete Weltposition des Fahrgasts wird absichtlich nicht gespeichert: Stufe 3 bricht
        /// eine laufende Fahrt beim Laden ab (siehe IDEEN.md), die Position würde also nie gebraucht.
        /// Erst die Wiederherstellung in Stufe 5 braucht hier eine SerializableVector3.
        /// </remarks>
        public Address? pickupAddress;

        /// <summary>Zieladresse der Fahrt. Null, solange keine Anfrage offen ist.</summary>
        public Address? destinationAddress;

        /// <summary>Berechneter Fahrpreis. In Stufe 3 nur Anzeige – ausgezahlt wird ab Stufe 4.</summary>
        public float fare;

        /// <summary>Luftlinie Abholung → Ziel in Metern, Grundlage des Fahrpreises.</summary>
        public float tripDistance;

        /// <summary>Spielzeit, ab der die nächste Anfrage erscheinen darf. Null = beim nächsten Tick würfeln.</summary>
        /// <remarks>
        /// Bewusst nullbar statt mit Default: ein frisches <c>new Timestamp()</c> wäre Tag 0, 00:00 Uhr
        /// und läge damit immer in der Vergangenheit – die erste Anfrage käme sofort.
        /// </remarks>
        public Timestamp? nextRequestTime;

        /// <summary>Spielzeit, ab der eine nicht angenommene Anfrage lautlos verfällt.</summary>
        public Timestamp? offerExpiryTime;

        /// <summary>Spielzeit der Auftragsannahme. Ab Stufe 4 Grundlage der Sternebewertung.</summary>
        public Timestamp? tripStartTime;

        /// <summary>Setzt alle fahrtbezogenen Felder zurück, ohne die Schicht zu beenden.</summary>
        public void ClearTrip()
        {
            state = QuickRidTripState.Waiting;
            pickupAddress = null;
            destinationAddress = null;
            fare = 0f;
            tripDistance = 0f;
            offerExpiryTime = null;
            tripStartTime = null;
        }
    }
}
