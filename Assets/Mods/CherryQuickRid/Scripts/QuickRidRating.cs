#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CherryQuickRid
{
    /// <summary>
    /// Sternebewertung pro Fahrt und gleitender Schnitt über die letzten Fahrten. Reine Funktionen
    /// ohne Spielzugriff; gespeichert wird die Historie in <see cref="QuickRidMission"/> und dauerhaft
    /// über <see cref="QuickRidStats"/> in <c>modData</c>.
    /// </summary>
    public static class QuickRidRating
    {
        public const int HistoryLength = 10;

        /// <summary>
        /// Angenommene Durchschnittsgeschwindigkeit in Metern pro Spielminute.
        /// </summary>
        /// <remarks>
        /// Autos fahren in Realzeit, die Spieluhr läuft bei 1× mit einer Spielminute pro Realsekunde
        /// (<c>GameManager.RunMainGameTick(Time.deltaTime * MinutesMultiplier)</c>). 8 m pro
        /// Spielminute entsprechen also 8 m/s Realzeit bei normalem Tempo. Die Luftlinie ist kürzer
        /// als die Straße – der Zeitpuffer (<see cref="QuickRidSettings.TimeAllowance"/>) fängt das ab.
        /// </remarks>
        public const float AssumedSpeedMetersPerMinute = 8f;

        /// <summary>
        /// Grundzeit in Spielminuten, die jede Fahrt zusätzlich zur Strecke bekommt.
        /// </summary>
        /// <remarks>
        /// Deckt ab, was auf jeder Fahrt gleich anfällt und nichts mit der Distanz zu tun hat:
        /// wenden, um den Block fahren, eine Parklücke am Ziel suchen. Vanilla macht es genauso –
        /// die Essenslieferung rechnet <c>60 + Distanz × 0,45</c> Minuten
        /// (FoodDeliveryJobConfig.baseTimeMinutes, Tooltip: „Covers the overhead that is the same on
        /// every trip"). 15 statt 60 Minuten, weil die 60 dort einen Fußweg abdecken.
        /// </remarks>
        public const float BaseMinutes = 15f;

        /// <summary>Unterhalb dieses Schadenzuwachses (0..1) gibt es keinen Sternabzug – Kratzer zählen nicht.</summary>
        public const float MinDamageForPenalty = 0.01f;

        /// <summary>Sterne-Zeichen der Vorschau im Aufgabenpanel.</summary>
        private const char FilledStar = '★';
        private const char EmptyStar = '☆';

        /// <summary>Farben der Sterne-Vorschau: Gold für vergebene, Grau für offene Sterne.</summary>
        private const string FilledStarColor = "FFD700";
        private const string EmptyStarColor = "808080";

        /// <summary>
        /// Erwartete Fahrzeit in Spielminuten: Grundzeit plus Luftlinie Abholung → Ziel.
        /// </summary>
        /// <remarks>
        /// Ergibt 0,125 Minuten pro Meter gegenüber 0,45 bei der Essenslieferung – ein Auto ist
        /// schneller als ein Fußgänger. Mit dem Zeitpuffer von 150 % stehen für 300 m rund 79, für
        /// 1500 m rund 304 Spielminuten zur Verfügung (Vanilla-Essenslieferung: 195 bzw. gar nicht,
        /// weil dort bei 600 m Schluss ist).
        /// </remarks>
        public static float ExpectedMinutes(float distanceMeters)
        {
            return BaseMinutes + Mathf.Max(0f, distanceMeters) / AssumedSpeedMetersPerMinute;
        }

        /// <summary>Zeitfenster für volle Sterne: erwartete Fahrzeit × Zeitpuffer.</summary>
        public static float AllowedMinutes(float distanceMeters)
        {
            return ExpectedMinutes(distanceMeters) * QuickRidSettings.TimeAllowance;
        }

        /// <param name="elapsedMinutes">Benötigte Spielzeit der Fahrt ab Einstieg.</param>
        /// <param name="allowedMinutes">Zeitfenster aus <see cref="AllowedMinutes"/>.</param>
        /// <param name="damageTaken">Schadenzuwachs (0..1) zwischen Einstieg und Absetzen.</param>
        public static int CalculateStars(float elapsedMinutes, float allowedMinutes, float damageTaken)
        {
            int stars = 5;
            float ratio = allowedMinutes <= 0f ? 1f : elapsedMinutes / allowedMinutes;

            if (ratio > 1.00f) stars -= 1;
            if (ratio > 1.25f) stars -= 1;
            if (ratio > 1.50f) stars -= 1;
            if (damageTaken >= MinDamageForPenalty) stars -= 1;

            return Mathf.Clamp(stars, 1, 5);
        }

        public static float Average(IReadOnlyList<int> history)
        {
            if (history.Count == 0)
                return 5f;
            float sum = 0f;
            foreach (var s in history) sum += s;
            return sum / history.Count;
        }

        /// <summary>Bonus/Malus auf den Fahrpreis je nach Schnitt: 4,5+ → +20 %, unter 3 → −20 %.</summary>
        public static float FareModifier(float averageStars)
        {
            if (averageStars >= 4.5f) return 1.20f;
            if (averageStars >= 4.0f) return 1.10f;
            if (averageStars < 3.0f) return 0.80f;
            return 1.00f;
        }

        /// <summary>
        /// Modifier für die nächste Anfrage. Ohne bewertete Fahrt 1,0 – <see cref="Average"/> liefert
        /// für eine leere Historie 5 und würde einem Neufahrer sonst sofort +20 % schenken.
        /// </summary>
        public static float CurrentModifier(IReadOnlyList<int>? history)
        {
            if (history == null || history.Count == 0)
                return 1f;
            return FareModifier(Average(history));
        }

        /// <summary>Hängt eine Bewertung an und kürzt vorne auf <see cref="HistoryLength"/>.</summary>
        public static void Push(List<int> history, int stars)
        {
            history.Add(Mathf.Clamp(stars, 1, 5));
            while (history.Count > HistoryLength)
                history.RemoveAt(0);
        }

        /// <summary>Sterne als Zeichenkette, z. B. "★★★★☆" für vier von fünf.</summary>
        public static string FormatStars(int stars)
        {
            int filled = Mathf.Clamp(stars, 0, 5);
            return new string(FilledStar, filled) + new string(EmptyStar, 5 - filled);
        }

        /// <summary>Dieselben Sterne mit TMP-Farbmarkierung, für Labels mit Rich Text.</summary>
        /// <remarks>
        /// Nur für Zeilen aus <c>MissionTasksUI.CreateAddressEntry</c> geeignet: die schaltet
        /// <c>richText</c> ein, und Vanilla setzt dort selbst Farbmarkierungen
        /// (<c>FormatAddressWithDistance</c>). In einer Benachrichtigung oder im Log stünde sonst der
        /// rohe Markup-Text.
        /// </remarks>
        public static string FormatStarsColored(int stars)
        {
            int filled = Mathf.Clamp(stars, 0, 5);

            var builder = new StringBuilder(64);
            if (filled > 0)
            {
                builder.Append("<color=#").Append(FilledStarColor).Append('>');
                builder.Append(FilledStar, filled);
                builder.Append("</color>");
            }

            if (filled < 5)
            {
                builder.Append("<color=#").Append(EmptyStarColor).Append('>');
                builder.Append(EmptyStar, 5 - filled);
                builder.Append("</color>");
            }

            return builder.ToString();
        }

        /// <summary>Schnitt mit einer Dezimalstelle, kulturunabhängig; "–" ohne bewertete Fahrt.</summary>
        public static string FormatAverage(IReadOnlyList<int>? history)
        {
            if (history == null || history.Count == 0)
                return "–";
            return Average(history).ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
