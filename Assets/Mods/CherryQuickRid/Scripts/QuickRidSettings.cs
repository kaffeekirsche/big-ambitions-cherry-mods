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

        /// <summary>
        /// Schaltet Entwicklerwerkzeug frei: die Feinjustierung (Wartezeiten, Radien,
        /// Streckenlängen) im Optionsmenü und die Diagnose-Ausgaben im Log.
        /// </summary>
        /// <remarks>
        /// Für Spieler steht im Menü nur die Schwierigkeit; die Einzelwerte sind Werkzeug für die
        /// Entwicklung und würden das Menü sonst zumüllen. Ebenso im Log: Betriebsmeldungen bleiben
        /// immer stehen, innerer Zustand nur hier (siehe <see cref="QuickRidLog"/>). Bewusst eine
        /// Konstante und kein Schalter: wer sie braucht, baut die Mod ohnehin selbst.
        /// </remarks>
        public const bool DeveloperMode = false;

        /// <summary>
        /// Auswahl aus dem Dropdown „Schwierigkeit". Setzt Fahrpreis, Zeitpuffer und Trinkgeld über
        /// <see cref="QuickRidDifficulty.Apply"/>.
        /// </summary>
        public static QuickRidDifficultyChoice DifficultyChoice = QuickRidDifficultyChoice.MatchGame;

        /// <summary>Fahrpreis-Multiplikator in Prozent (100 = 1,0x). Kommt aus der Schwierigkeit.</summary>
        public static int FareMultiplierPercent = 100;

        /// <summary>Zeitpuffer in Prozent auf die berechnete Fahrzeit (Basis für die Sternebewertung).</summary>
        public static int TimeAllowancePercent = 150;

        /// <summary>Trinkgeld-Chance in Prozent (100 = Tabellenwerte aus <see cref="QuickRidTips"/> unverändert).</summary>
        public static int TipChancePercent = 100;

        /// <summary>Kürzeste Wartezeit auf die nächste Anfrage, in Spielminuten.</summary>
        public static int RequestWaitMinMinutes = 15;

        /// <summary>Längste Wartezeit auf die nächste Anfrage, in Spielminuten.</summary>
        public static int RequestWaitMaxMinutes = 45;

        /// <summary>Nach so vielen Spielminuten verfällt eine nicht angenommene Anfrage.</summary>
        public static int OfferTimeoutMinutes = 20;

        /// <summary>Umkreis um das Auto, in dem nach einem Abholpunkt gesucht wird, in Metern.</summary>
        public static int PassengerSearchRadiusMeters = 300;

        /// <summary>Abstand, in dem das Auto zum Einsteigen stehen muss, in Metern.</summary>
        public static int PickupRadiusMeters = 10;

        /// <summary>
        /// Abstand zum Gebäudeeingang, in dem das Auto zum Absetzen stehen muss, in Metern.
        /// </summary>
        /// <remarks>
        /// Getrennt vom Abholradius und ab Werk großzügiger: der Fahrgast steht beim Abholen direkt
        /// an der Straße, das Ziel ist dagegen der Gebäudeeingang, der auch mal weiter zurückliegt.
        /// </remarks>
        public static int DropoffRadiusMeters = 10;

        /// <summary>Kürzeste Fahrtstrecke in Metern (Luftlinie Abholung → Ziel).</summary>
        public static int MinTripDistanceMeters = 300;

        /// <summary>Maximale Fahrtstrecke in Metern (Luftlinie Abholung → Ziel).</summary>
        public static int MaxTripDistanceMeters = 1500;

        /// <summary>
        /// So nah muss eine Gebäudetür an einem befahrbaren Straßenpunkt liegen, damit die Adresse
        /// überhaupt als Abholung oder Ziel infrage kommt.
        /// </summary>
        /// <remarks>
        /// Konstante statt Slider: der Wert wirkt nur beim einmaligen Aufbau des Adress-Caches
        /// (<c>QuickRidController.BuildAddressCacheRoutine</c>), eine Änderung zur Laufzeit würde
        /// also erst beim nächsten Stadtladen greifen und im Optionsmenü nur verwirren.
        /// </remarks>
        public const float RoadProximityMeters = 15f;

        public static float FareMultiplier => FareMultiplierPercent / 100f;
        public static float TimeAllowance => TimeAllowancePercent / 100f;

        /// <summary>Skalierung der Trinkgeld-Chancen; 1,0 = Tabellenwerte.</summary>
        public static float TipChance => TipChancePercent / 100f;
    }
}
