#nullable enable
using BAModAPI;

namespace CherryQuickRid
{
    /// <summary>
    /// Auswahl im Options-Dropdown „Schwierigkeit". Die Reihenfolge bestimmt den gespeicherten Index
    /// (PlayerPrefs) und darf deshalb nie umsortiert werden.
    /// </summary>
    public enum QuickRidDifficultyChoice
    {
        /// <summary>Übernimmt die Schwierigkeit des Spielstands. Standard.</summary>
        MatchGame = 0,
        Easy = 1,
        Normal = 2,
        Hard = 3
    }

    /// <summary>
    /// Fasst Fahrpreis, Zeitpuffer und Trinkgeld-Chance zu einer Schwierigkeit zusammen, damit im
    /// Optionsmenü ein Dropdown statt dreier Prozent-Slider steht.
    /// </summary>
    /// <remarks>
    /// „Wie Spiel" wird bewusst erst bei Gebrauch aufgelöst, nicht beim Registrieren der Options:
    /// <c>SaveGameManager.Current</c> ist im Hauptmenü null (das Spiel selbst geht überall so vor,
    /// siehe <c>ItemHelper.GetSellingMultiplier</c>), und die Schwierigkeit lässt sich im laufenden
    /// Spiel noch ändern. <see cref="Apply"/> läuft deshalb zusätzlich beim Laden eines Spielstands.
    /// </remarks>
    public static class QuickRidDifficulty
    {
        /// <summary>Ein Satz Werte in Prozent, wie ihn die drei bisherigen Slider hatten.</summary>
        public readonly struct Preset
        {
            public readonly int farePercent;
            public readonly int timeAllowancePercent;
            public readonly int tipChancePercent;

            public Preset(int farePercent, int timeAllowancePercent, int tipChancePercent)
            {
                this.farePercent = farePercent;
                this.timeAllowancePercent = timeAllowancePercent;
                this.tipChancePercent = tipChancePercent;
            }
        }

        public static readonly Preset EasyPreset = new Preset(120, 200, 150);
        public static readonly Preset NormalPreset = new Preset(100, 150, 100);
        public static readonly Preset HardPreset = new Preset(85, 120, 50);

        /// <summary>Locale-Keys der Dropdown-Einträge, in der Reihenfolge von <see cref="QuickRidDifficultyChoice"/>.</summary>
        public static readonly string[] ChoiceKeys =
        {
            "quickrid_difficulty_match",
            "quickrid_difficulty_easy",
            "quickrid_difficulty_normal",
            "quickrid_difficulty_hard"
        };

        /// <summary>
        /// Setzt die drei Werte in <see cref="QuickRidSettings"/> aus der aktuellen Auswahl.
        /// Bei „Wie Spiel" wird die Schwierigkeit des Spielstands gelesen, falls einer geladen ist.
        /// </summary>
        public static void Apply(IModLogger? logger = null)
        {
            QuickRidDifficultyChoice choice = QuickRidSettings.DifficultyChoice;
            QuickRidDifficultyChoice resolved = Resolve(choice);
            Preset preset = GetPreset(resolved);

            QuickRidSettings.FareMultiplierPercent = preset.farePercent;
            QuickRidSettings.TimeAllowancePercent = preset.timeAllowancePercent;
            QuickRidSettings.TipChancePercent = preset.tipChancePercent;

            logger?.Info($"QuickRid: Schwierigkeit {choice}" +
                $"{(choice == QuickRidDifficultyChoice.MatchGame ? $" (= {resolved})" : string.Empty)} – " +
                $"Fahrpreis {preset.farePercent} %, Zeitpuffer {preset.timeAllowancePercent} %, " +
                $"Trinkgeld {preset.tipChancePercent} %.");
        }

        /// <summary>Löst „Wie Spiel" auf. Ohne geladenen Spielstand und bei „Eigen" gilt Normal.</summary>
        public static QuickRidDifficultyChoice Resolve(QuickRidDifficultyChoice choice)
        {
            if (choice != QuickRidDifficultyChoice.MatchGame)
                return choice;

            // Im Hauptmenü gibt es keinen Spielstand; Difficulty.Custom hat keine festen Werte.
            GameInstance? save = SaveGameManager.Current;
            if (save == null || save.gameVariables == null)
                return QuickRidDifficultyChoice.Normal;

            switch (save.gameVariables.difficulty)
            {
                case Difficulty.Easy: return QuickRidDifficultyChoice.Easy;
                case Difficulty.Hard: return QuickRidDifficultyChoice.Hard;
                default: return QuickRidDifficultyChoice.Normal;
            }
        }

        public static Preset GetPreset(QuickRidDifficultyChoice resolved)
        {
            switch (resolved)
            {
                case QuickRidDifficultyChoice.Easy: return EasyPreset;
                case QuickRidDifficultyChoice.Hard: return HardPreset;
                default: return NormalPreset;
            }
        }
    }
}
