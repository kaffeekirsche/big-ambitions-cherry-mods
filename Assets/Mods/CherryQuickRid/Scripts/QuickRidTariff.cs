#nullable enable
using System;

namespace CherryQuickRid
{
    /// <summary>
    /// Tarifzeit einer Fahrtanfrage. Der Spielstand speichert Enums als Zahl (Odin schreibt den
    /// numerischen Wert), die Werte sind deshalb explizit vergeben und dürfen nie umsortiert werden.
    /// </summary>
    [Serializable]
    public enum QuickRidTariffPeriod
    {
        /// <summary>Normaltarif. Zugleich der Wert, den alte Spielstaende ohne dieses Feld liefern.</summary>
        Standard = 0,

        /// <summary>Hauptverkehrszeit, morgens und nachmittags.</summary>
        Peak = 1,

        /// <summary>Nachttarif.</summary>
        Night = 2
    }

    /// <summary>
    /// Zeitabhängiger Aufschlag auf den Fahrpreis. Reine Funktionen; gerechnet wird in
    /// <c>QuickRidController.TryCreateRequest</c>, angezeigt im Angebotsdialog.
    /// </summary>
    /// <remarks>
    /// <c>TimeHelper.IsInHourRange(hourStart, hourEndExclusive)</c> zählt die Endstunde nicht mit
    /// und kommt mit dem Überlauf über Mitternacht zurecht. Die Fenster decken also
    /// 07:00-08:59, 16:00-18:59 und 22:00-04:59 ab.
    /// </remarks>
    public static class QuickRidTariff
    {
        public const float PeakMultiplier = 1.25f;
        public const float NightMultiplier = 1.15f;

        private const int PeakMorningStart = 7;
        private const int PeakMorningEnd = 9;
        private const int PeakEveningStart = 16;
        private const int PeakEveningEnd = 19;
        private const int NightStart = 22;
        private const int NightEnd = 5;

        public static QuickRidTariffPeriod CurrentPeriod()
        {
            if (TimeHelper.IsInHourRange(PeakMorningStart, PeakMorningEnd)
                || TimeHelper.IsInHourRange(PeakEveningStart, PeakEveningEnd))
                return QuickRidTariffPeriod.Peak;

            if (TimeHelper.IsInHourRange(NightStart, NightEnd))
                return QuickRidTariffPeriod.Night;

            return QuickRidTariffPeriod.Standard;
        }

        public static float Multiplier(QuickRidTariffPeriod period)
        {
            switch (period)
            {
                case QuickRidTariffPeriod.Peak: return PeakMultiplier;
                case QuickRidTariffPeriod.Night: return NightMultiplier;
                default: return 1f;
            }
        }

        /// <summary>Locale-Key der Tarifzeile im Angebotsdialog; der Aufschlag steht im Text.</summary>
        public static string LocaleKey(QuickRidTariffPeriod period)
        {
            switch (period)
            {
                case QuickRidTariffPeriod.Peak: return "quickrid_tariff_peak";
                case QuickRidTariffPeriod.Night: return "quickrid_tariff_night";
                default: return "quickrid_tariff_standard";
            }
        }
    }
}
