#nullable enable
using UnityEngine;

namespace CherryQuickRid
{
    /// <summary>
    /// Trinkgeld pro abgeschlossener Fahrt. Reine Funktionen ohne Spielzugriff; gewürfelt wird in
    /// <c>QuickRidController.CompleteTrip</c>.
    /// </summary>
    /// <remarks>
    /// Vorlage ist <c>DeliveryJobTipsConfig.RollTip</c> aus dem Vanilla-Lieferjob: ein einziger
    /// <c>Random.value</c>, eine Tabelle aus Schwellen, und es gewinnt der größte Eintrag, dessen
    /// Schwelle über dem Wurf liegt. Eine "schnelle" Fahrt (höchstens die Hälfte des Zeitfensters,
    /// <c>fastDeliveryTimeRatio</c>) hebt jede Schwelle um <c>fastDeliveryChanceUp</c>.
    /// <para>
    /// Anders als Vanilla ist das Trinkgeld kein fester Betrag, sondern ein Anteil des Fahrpreises,
    /// und die Chance hängt an den Sternen dieser Fahrt: 5 Sterne volle Chance, 4 Sterne halbe,
    /// darunter nichts. Die Vanilla-Tabellenwerte liegen nur im Asset, nicht im Code – die Werte
    /// hier sind eigene (siehe IDEEN.md, Stufe 6a).
    /// </para>
    /// </remarks>
    public static class QuickRidTips
    {
        /// <summary>Ein Tabelleneintrag: Schwelle (0..1) und Anteil vom Fahrpreis (0..1).</summary>
        public readonly struct TipChance
        {
            public readonly float chance;
            public readonly float fareShare;

            public TipChance(float chance, float fareShare)
            {
                this.chance = chance;
                this.fareShare = fareShare;
            }
        }

        /// <summary>
        /// Mit 100 % Slider und 5 Sternen: 5 % der Fahrten 30 %, 10 % der Fahrten 20 %, 15 % der
        /// Fahrten 10 % Trinkgeld, 70 % nichts. Die Schwellen sind kumulativ zu lesen, weil derselbe
        /// Wurf gegen alle Einträge geprüft wird und der größte Anteil gewinnt.
        /// </summary>
        public static readonly TipChance[] Table =
        {
            new TipChance(0.30f, 0.10f),
            new TipChance(0.15f, 0.20f),
            new TipChance(0.05f, 0.30f)
        };

        /// <summary>Anteil des Zeitfensters, bis zu dem eine Fahrt als "schnell" gilt (= Vanilla fastDeliveryTimeRatio).</summary>
        public const float FastTripRatio = 0.5f;

        /// <summary>Aufschlag auf jede Schwelle bei einer schnellen Fahrt (= Vanilla fastDeliveryChanceUp).</summary>
        public const float FastChanceBonus = 0.05f;

        public static bool IsFastTrip(float elapsedMinutes, float allowedMinutes)
        {
            return allowedMinutes > 0f && elapsedMinutes <= allowedMinutes * FastTripRatio;
        }

        /// <summary>Faktor auf die Chance je nach Sternen dieser Fahrt: 5 → 1, 4 → 0,5, sonst 0.</summary>
        public static float StarsChanceFactor(int stars)
        {
            if (stars >= 5) return 1f;
            if (stars == 4) return 0.5f;
            return 0f;
        }

        /// <summary>
        /// Kern des Wurfs, ohne Zufall: liefert den Anteil vom Fahrpreis (0 = kein Trinkgeld).
        /// </summary>
        /// <param name="roll">Wurf aus [0, 1).</param>
        /// <param name="chanceScale">Multiplikator aus den Options (<see cref="QuickRidSettings.TipChance"/>).</param>
        public static float RollFareShare(float roll, bool fast, int stars, float chanceScale)
        {
            float factor = StarsChanceFactor(stars) * Mathf.Max(0f, chanceScale);
            if (factor <= 0f)
                return 0f;

            float best = 0f;
            for (int i = 0; i < Table.Length; i++)
            {
                TipChance entry = Table[i];
                float effective = (entry.chance + (fast ? FastChanceBonus : 0f)) * factor;
                if (entry.fareShare > best && roll < effective)
                    best = entry.fareShare;
            }

            return best;
        }

        /// <summary>
        /// Würfelt das Trinkgeld dieser Fahrt und gibt Wurf und wirksame Chance zurück, damit die
        /// Abrechnung beides protokollieren kann.
        /// </summary>
        public static float Roll(bool fast, int stars, out float roll, out float chance)
        {
            float scale = QuickRidSettings.TipChance;
            roll = UnityEngine.Random.value;
            chance = BestChance(fast, stars, scale);
            return RollFareShare(roll, fast, stars, scale);
        }

        /// <summary>
        /// Höchste wirksame Schwelle der Tabelle, also die Wahrscheinlichkeit, überhaupt etwas zu
        /// bekommen. Nur für die Anzeige im Log – gewürfelt wird in <see cref="RollFareShare"/>.
        /// </summary>
        public static float BestChance(bool fast, int stars, float chanceScale)
        {
            float factor = StarsChanceFactor(stars) * Mathf.Max(0f, chanceScale);
            if (factor <= 0f)
                return 0f;

            float best = 0f;
            for (int i = 0; i < Table.Length; i++)
            {
                float effective = (Table[i].chance + (fast ? FastChanceBonus : 0f)) * factor;
                if (effective > best)
                    best = effective;
            }

            return best;
        }

        /// <summary>Betrag in Dollar, auf ganze Dollar gerundet – wie der Fahrpreis selbst.</summary>
        public static float ToAmount(float fare, float fareShare)
        {
            return Mathf.Round(Mathf.Max(0f, fare) * Mathf.Clamp01(fareShare));
        }
    }
}
