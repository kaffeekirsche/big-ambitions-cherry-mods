#nullable enable
using System;
using System.Collections.Generic;
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
    /// <para>
    /// <c>endTime</c> liegt seit Stufe 4 zehn Jahre in der Zukunft: ein Fahrdienst hat keine Schicht.
    /// <c>PlayerMission.IsOngoing()</c> ist nicht virtual und lässt sich deshalb nicht überschreiben;
    /// kein Spielcode wertet die Frist einer fremden Missionsklasse aus.
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
        /// Die Weltposition des Fahrgasts wird bewusst nicht gespeichert: beim Laden bestimmt
        /// <c>QuickRidController.RestoreState</c> sie aus dieser Adresse neu (Gebäudetür plus
        /// NavMesh-Sample). Der Fahrgast steht dann eventuell ein Stück neben seinem alten Platz,
        /// dafür bleibt der Spielstand frei von Positionsdaten.
        /// </remarks>
        public Address? pickupAddress;

        /// <summary>Zieladresse der Fahrt. Null, solange keine Anfrage offen ist.</summary>
        public Address? destinationAddress;

        /// <summary>
        /// Fahrpreis inklusive Rating-Modifier, wie er im Angebotsdialog stand. Wird beim Absetzen
        /// unverändert ausgezahlt – der genannte Preis muss dem gezahlten entsprechen.
        /// </summary>
        public float fare;

        /// <summary>Luftlinie Abholung → Ziel in Metern, Grundlage von Fahrpreis und Zeitfenster.</summary>
        public float tripDistance;

        /// <summary>Spielzeit, ab der die nächste Anfrage erscheinen darf. Null = beim nächsten Tick würfeln.</summary>
        /// <remarks>
        /// Bewusst nullbar statt mit Default: ein frisches <c>new Timestamp()</c> wäre Tag 0, 00:00 Uhr
        /// und läge damit immer in der Vergangenheit – die erste Anfrage käme sofort.
        /// </remarks>
        public Timestamp? nextRequestTime;

        /// <summary>Spielzeit, ab der eine nicht angenommene Anfrage lautlos verfällt.</summary>
        public Timestamp? offerExpiryTime;

        /// <summary>Spielzeit der Auftragsannahme. Nur Statistik – bewertet wird ab <see cref="boardingTime"/>.</summary>
        public Timestamp? tripStartTime;

        /// <summary>
        /// Spielzeit, zu der der Fahrgast eingestiegen ist. Ab hier läuft die bewertete Fahrzeit,
        /// weil sich das Zeitfenster aus der Distanz Abholung → Ziel ableitet, nicht aus der Anfahrt.
        /// </summary>
        public Timestamp? boardingTime;

        /// <summary><c>VehicleInstance.damage</c> (0..1) beim Einstieg. Bewertet wird nur der Zuwachs bis zum Absetzen.</summary>
        public float damageAtBoarding;

        // --- Statistik und Rating ---------------------------------------------------------------
        // Kopie der dauerhaften Werte aus SaveGameManager.Current.modData (siehe QuickRidStats).
        // Beim Online-Gehen geladen, nach jeder Fahrt und beim Offline-Gehen zurückgeschrieben.

        /// <summary>Abgeschlossene Fahrten insgesamt.</summary>
        public int completedTrips;

        /// <summary>Ausgezahlte Fahrpreise insgesamt (ohne Trinkgeld).</summary>
        public float totalEarnings;

        /// <summary>Trinkgeld insgesamt. Bleibt bis Stufe 6 immer 0.</summary>
        public float totalTips;

        // --- Zähler dieser Online-Sitzung -------------------------------------------------------
        // Getrennt von der dauerhaften Statistik oben: sie beginnen bei jedem Online-Gehen bei 0 und
        // speisen die Übersicht beim Offline-Gehen (siehe QuickRidSessionSummary). Gezählt werden nur
        // abgeschlossene Fahrten – ein Abbruch mit Fahrgast an Bord schlägt sich allein in der
        // Rating-Historie nieder.

        /// <summary>Abgeschlossene Fahrten seit dem Online-Gehen.</summary>
        public int sessionTrips;

        /// <summary>Ausgezahlte Fahrpreise seit dem Online-Gehen.</summary>
        public float sessionEarnings;

        /// <summary>Trinkgeld seit dem Online-Gehen. Bleibt bis Stufe 6 immer 0.</summary>
        public float sessionTips;

        /// <summary>Summe der vergebenen Sterne dieser Sitzung; geteilt durch <see cref="sessionTrips"/> der Schnitt.</summary>
        public int sessionStarsTotal;

        /// <summary>
        /// Sterne der letzten <see cref="QuickRidRating.HistoryLength"/> Fahrten, älteste zuerst.
        /// Nie direkt lesen – <see cref="GetRatingHistory"/> benutzen: Odin legt Objekte ohne
        /// Konstruktor an, der Feldinitialisierer greift also nur im JSON-Pfad.
        /// </summary>
        public List<int>? ratingHistory = new List<int>();

        /// <summary>Historie mit Null-Guard. Methode statt Property, damit Newtonsoft sie nicht mitschreibt.</summary>
        public List<int> GetRatingHistory()
        {
            return ratingHistory ??= new List<int>();
        }

        /// <summary>Setzt alle fahrtbezogenen Felder zurück, ohne Schicht, Statistik oder Historie anzufassen.</summary>
        public void ClearTrip()
        {
            state = QuickRidTripState.Waiting;
            pickupAddress = null;
            destinationAddress = null;
            fare = 0f;
            tripDistance = 0f;
            offerExpiryTime = null;
            tripStartTime = null;
            boardingTime = null;
            damageAtBoarding = 0f;
        }
    }
}
