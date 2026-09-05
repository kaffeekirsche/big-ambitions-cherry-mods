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

        /// <summary>Kürzeste Wartezeit auf die nächste Anfrage, in Spielminuten.</summary>
        public static int RequestWaitMinMinutes = 15;

        /// <summary>Längste Wartezeit auf die nächste Anfrage, in Spielminuten.</summary>
        public static int RequestWaitMaxMinutes = 45;

        /// <summary>Nach so vielen Spielminuten verfällt eine nicht angenommene Anfrage.</summary>
        public static int OfferTimeoutMinutes = 20;

        /// <summary>Umkreis um das Auto, in dem nach einem Abholpunkt gesucht wird, in Metern.</summary>
        public static int PassengerSearchRadiusMeters = 300;

        /// <summary>Abstand, in dem das Auto zum Ein- und Aussteigen stehen muss, in Metern.</summary>
        public static int PickupRadiusMeters = 8;

        /// <summary>Kürzeste Fahrtstrecke in Metern (Luftlinie Abholung → Ziel).</summary>
        public static int MinTripDistanceMeters = 300;

        /// <summary>Maximale Fahrtstrecke in Metern (Luftlinie Abholung → Ziel).</summary>
        public static int MaxTripDistanceMeters = 1500;

        public static float FareMultiplier => FareMultiplierPercent / 100f;
        public static float TimeAllowance => TimeAllowancePercent / 100f;
    }
}
