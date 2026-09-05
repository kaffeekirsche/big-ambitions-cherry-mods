#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace CherryQuickRid
{
    /// <summary>
    /// Sternebewertung pro Fahrt und gleitender Schnitt über die letzten Fahrten.
    /// Persistenz: SaveGameManager.Current.modData["quickrid:rating_history"] (siehe GoodbyeIRS im SDK).
    /// </summary>
    public static class QuickRidRating
    {
        public const int HistoryLength = 10;
        public const string HistoryKey = QuickRidSettings.ModDataPrefix + "rating_history";

        /// <param name="elapsedMinutes">Benötigte Spielzeit der Fahrt.</param>
        /// <param name="allowedMinutes">Zeitfenster (Basiszeit × TimeAllowance).</param>
        /// <param name="damageTaken">Schaden während der Fahrt (0 = keiner).</param>
        public static int CalculateStars(float elapsedMinutes, float allowedMinutes, float damageTaken)
        {
            int stars = 5;
            float ratio = allowedMinutes <= 0f ? 1f : elapsedMinutes / allowedMinutes;

            if (ratio > 1.00f) stars -= 1;
            if (ratio > 1.25f) stars -= 1;
            if (ratio > 1.50f) stars -= 1;
            if (damageTaken > 0f) stars -= 1;

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
    }
}
