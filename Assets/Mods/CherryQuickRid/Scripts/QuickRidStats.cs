#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CherryQuickRid
{
    /// <summary>
    /// Dauerhafte Fahrerstatistik in <c>SaveGameManager.Current.modData</c>: Rating-Historie und Zähler
    /// überleben so das Offline-Gehen (die <see cref="QuickRidMission"/> wird dabei gelöscht).
    /// Muster: Assets/Mods/GoodbyeIRS (<c>save.modData ??= …</c>).
    /// </summary>
    /// <remarks>
    /// Ablauf: <see cref="LoadInto"/> beim Online-Gehen und nach dem Laden eines Spielstands,
    /// <see cref="SaveFrom"/> nach jeder abgeschlossenen Fahrt und beim Offline-Gehen.
    /// Werte werden kulturunabhängig geschrieben, sonst liest ein deutsches System "4,5" anders als
    /// ein englisches. Ungültige oder fehlende Einträge gelten als "noch nichts gefahren".
    /// </remarks>
    public static class QuickRidStats
    {
        /// <summary>Sterne der letzten Fahrten, kommasepariert, älteste zuerst. Beispiel: "5,4,5".</summary>
        public const string HistoryKey = QuickRidSettings.ModDataPrefix + "rating_history";

        /// <summary>Zähler als "fahrten;einnahmen;trinkgeld". Beispiel: "12;340;0".</summary>
        public const string StatsKey = QuickRidSettings.ModDataPrefix + "stats";

        private const char HistorySeparator = ',';
        private const char StatsSeparator = ';';

        /// <summary>Überschreibt Historie und Zähler der Mission mit den gespeicherten Werten.</summary>
        public static void LoadInto(QuickRidMission mission)
        {
            Dictionary<string, string>? modData = SaveGameManager.Current?.modData;

            List<int> history = mission.GetRatingHistory();
            history.Clear();
            mission.completedTrips = 0;
            mission.totalEarnings = 0f;
            mission.totalTips = 0f;

            if (modData == null)
                return;

            if (modData.TryGetValue(HistoryKey, out string historyRaw) && !string.IsNullOrEmpty(historyRaw))
            {
                string[] parts = historyRaw.Split(HistorySeparator);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int stars))
                        QuickRidRating.Push(history, stars);
                }
            }

            if (modData.TryGetValue(StatsKey, out string statsRaw) && !string.IsNullOrEmpty(statsRaw))
            {
                string[] parts = statsRaw.Split(StatsSeparator);
                if (parts.Length > 0 && int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int trips))
                    mission.completedTrips = trips;
                if (parts.Length > 1 && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float earnings))
                    mission.totalEarnings = earnings;
                if (parts.Length > 2 && float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float tips))
                    mission.totalTips = tips;
            }
        }

        /// <summary>Schreibt Historie und Zähler der Mission nach modData.</summary>
        public static void SaveFrom(QuickRidMission mission)
        {
            GameInstance? save = SaveGameManager.Current;
            if (save == null)
                return;

            save.modData ??= new Dictionary<string, string>();

            List<int> history = mission.GetRatingHistory();
            var builder = new StringBuilder(history.Count * 2);
            for (int i = 0; i < history.Count; i++)
            {
                if (i > 0)
                    builder.Append(HistorySeparator);
                builder.Append(history[i].ToString(CultureInfo.InvariantCulture));
            }

            save.modData[HistoryKey] = builder.ToString();
            save.modData[StatsKey] = string.Join(
                StatsSeparator.ToString(),
                mission.completedTrips.ToString(CultureInfo.InvariantCulture),
                mission.totalEarnings.ToString("R", CultureInfo.InvariantCulture),
                mission.totalTips.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
