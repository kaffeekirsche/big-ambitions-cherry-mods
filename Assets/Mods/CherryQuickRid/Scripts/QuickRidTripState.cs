#nullable enable
using System;

namespace CherryQuickRid
{
    /// <summary>
    /// Phase der aktuellen Fahrt innerhalb einer laufenden Schicht.
    /// </summary>
    /// <remarks>
    /// Der Spielstand speichert Enums als Zahl (Odin schreibt den numerischen Wert, nicht den Namen).
    /// Die Werte sind deshalb explizit vergeben und dürfen nie umsortiert werden – neue Zustände
    /// ausschließlich hinten anhängen, sonst laden alte Spielstände den falschen Zustand.
    /// </remarks>
    [Serializable]
    public enum QuickRidTripState
    {
        /// <summary>Online, aber ohne Auftrag – wartet auf die nächste Anfrage.</summary>
        Waiting = 0,

        /// <summary>Anfrage generiert; der Dialog erscheint erst, wenn das Auto langsam genug ist.</summary>
        Offered = 1,

        /// <summary>Angenommen: der Fahrgast steht an der Abholtür.</summary>
        PassengerWaiting = 2,

        /// <summary>Fahrgast im Auto, unterwegs zum Ziel.</summary>
        PassengerAboard = 3
    }
}
