#nullable enable
namespace CherryQuickRid
{
    /// <summary>
    /// Laufzeit-Einstellungen (werden über Options → Mods gesetzt).
    /// Werte sind Ganzzahlen, weil ModOptions.AddSlider nur int liefert.
    /// </summary>
    public static class QuickRidSettings
    {
        public const string LocalePrefix = "quickrid_";
        public const string ModDataPrefix = "quickrid:";

        /// <summary>Fahrpreis-Multiplikator in Prozent (100 = 1,0x).</summary>
        public static int FareMultiplierPercent = 100;

        /// <summary>Zeitpuffer in Prozent auf die berechnete Fahrzeit (Basis für die Sternebewertung).</summary>
        public static int TimeAllowancePercent = 150;

        /// <summary>Maximale Fahrtstrecke in Metern (Luftlinie Abholung → Ziel).</summary>
        public static int MaxTripDistanceMeters = 1500;

        public static float FareMultiplier => FareMultiplierPercent / 100f;
        public static float TimeAllowance => TimeAllowancePercent / 100f;
    }
}
