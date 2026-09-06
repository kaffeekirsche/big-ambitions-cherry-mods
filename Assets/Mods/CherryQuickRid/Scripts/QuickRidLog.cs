#nullable enable
using BAModAPI;

namespace CherryQuickRid
{
    /// <summary>
    /// Trennt Betriebsmeldungen von Diagnose-Ausgaben.
    /// </summary>
    /// <remarks>
    /// Betriebsmeldungen (Laden, Online/Offline, abgeschlossene Fahrt, gesperrte Adresse, alle
    /// Warnungen und Fehler) gehen unverändert über <c>context.Logger</c> und stehen immer im Log –
    /// ohne sie ist eine Fehlermeldung aus dem Workshop nicht nachvollziehbar.
    /// <para>
    /// Diagnose-Ausgaben (Trinkgeld-Wurf, Schreibdetails der Sperrliste, Statistik des
    /// Adress-Caches, Suche nach Sprites und Symbolen) laufen über <see cref="Dev"/> und erscheinen
    /// nur, wenn <see cref="QuickRidSettings.DeveloperMode"/> gesetzt ist. Sie beschreiben inneren
    /// Zustand, den ein Spieler nicht deuten kann, und würden das Log der Spielsitzung zumüllen.
    /// </para>
    /// <para>
    /// Keine dieser Ausgaben darf in einem Pfad stehen, der jeden Frame läuft: auch eine
    /// abgeschaltete Zeile wertet ihre Argumente aus, weil der Text vor dem Aufruf gebaut wird.
    /// </para>
    /// </remarks>
    internal static class QuickRidLog
    {
        /// <summary>Diagnose-Ausgabe; verschwindet, wenn der Entwicklermodus aus ist.</summary>
        public static void Dev(IModLogger? logger, string message)
        {
            // Der Compiler hält den Rumpf bei abgeschalteter Konstante für unerreichbar.
#pragma warning disable CS0162
            if (QuickRidSettings.DeveloperMode)
                logger?.Info(message);
#pragma warning restore CS0162
        }
    }
}
