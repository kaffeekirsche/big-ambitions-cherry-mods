#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BAModAPI;
using BigAmbitions.Items;
using Localizor.LanguageChangeEvent;
using Player.PlayerMissions;
using UI;
using UI.DailySummary;
using Vehicles.DeliveryDriverJob;

namespace CherryQuickRid
{
    /// <summary>
    /// Zusammenfassung einer Online-Sitzung beim Offline-Gehen. Nutzt die Tagesübersicht des
    /// Vanilla-Lieferjobs und beschriftet deren Zeilen um.
    /// Vorlage: ShowNativeShiftSummary in _reference/BeATaxi~/BeATaxi/TaxiShiftController.cs
    /// </summary>
    /// <remarks>
    /// <c>DeliveryJobSummary</c> liest seine Zahlen aus <c>SaveGameManager.Current.currentPlayerMission</c>
    /// und erwartet dort eine <see cref="DeliveryDriverMission"/>. Der Missions-Slot wird deshalb kurz
    /// mit einer Wegwerf-Mission belegt und danach zwingend wieder auf den vorherigen Stand gesetzt.
    /// <c>Run()</c> liest alles vor dem ersten <c>yield</c>, es kann also kein Frame dazwischenfallen,
    /// in dem das Spiel eine fremde Lieferfahrt sähe.
    /// </remarks>
    public static class QuickRidSessionSummary
    {
        // Keys der Vanilla-Zeilen, nachgeschlagen in der locale.json des Spiels
        // (StreamingAssets/locale/en.json). Reihenfolge im Panel: Anzahl, Brutto, Trinkgeld,
        // Schäden, Netto. Die Trinkgeldzeile blendet JobSummary.SetTipsRow bei 0 selbst aus.
        private const string VanillaTitleKey = "delivery_job_summary";
        private const string VanillaCountKey = "delivery_job_completed_count";
        private const string VanillaGrossKey = "delivery_job_gross_payment";
        private const string VanillaDamagesKey = "delivery_job_damages";
        private const string VanillaNetKey = "delivery_job_net_payment";

        /// <summary>Privates Wertfeld der Schadenszeile, das die Durchschnittsbewertung aufnimmt.</summary>
        private const string DamagesLabelField = "damagesLabel";

        /// <summary>
        /// Zeigt die Übersicht der gerade beendeten Sitzung. Ohne abgeschlossene Fahrt passiert nichts.
        /// </summary>
        public static void Show(QuickRidMission mission, IModLogger? logger)
        {
            if (mission.sessionTrips <= 0 || SaveGameManager.Current == null)
                return;

            if (!InstanceBehavior<UIs>.IsInitialized || InstanceBehavior<UIs>.Instance.dailySummary == null)
                return;

            var destinations = new List<DeliveryJobDestination>(mission.sessionTrips);
            for (int i = 0; i < mission.sessionTrips; i++)
            {
                // Ohne Posten gilt ein Ziel als erledigt – die Übersicht zeigt dann "n/n" in Grün.
                destinations.Add(new DeliveryJobDestination(null, Array.Empty<ItemAmountTarget>()));
            }

            var summaryMission = new DeliveryDriverMission
            {
                startTime = mission.startTime,
                endTime = TimeHelper.Now(),
                timeLimitMinutes = 0, // schaltet WasFastDelivery ab; es würde sonst eine Startadresse suchen
                earnings = mission.sessionEarnings,
                tips = mission.sessionTips,
                damageFees = 0f,
                destinations = destinations
            };

            PlayerMission previous = SaveGameManager.Current.currentPlayerMission;
            SaveGameManager.Current.currentPlayerMission = summaryMission;

            try
            {
                InstanceBehavior<UIs>.Instance.dailySummary.RunDeliveryJobSummary();
                Relabel(mission, logger);
            }
            catch (Exception exception)
            {
                logger?.Warn("QuickRid: Sitzungsübersicht konnte nicht aufgebaut werden: " + exception.Message);
            }
            finally
            {
                SaveGameManager.Current.currentPlayerMission = previous;
            }
        }

        /// <summary>Schreibt die Beschriftungen der frisch erzeugten Übersicht auf QuickRid um.</summary>
        private static void Relabel(QuickRidMission mission, IModLogger? logger)
        {
            DeliveryJobSummary summary = UnityEngine.Object.FindObjectOfType<DeliveryJobSummary>();
            if (summary == null)
            {
                logger?.Warn("QuickRid: keine DeliveryJobSummary gefunden – Übersicht bleibt unbeschriftet.");
                return;
            }

            // Die Schadenszeile wird nur umbenannt, wenn auch ihr Wert ersetzt werden konnte –
            // sonst stünde die Bewertung neben einem Geldbetrag.
            bool ratingShown = TrySetLabelText(summary, DamagesLabelField, FormatSessionRating(mission));

            TextLocalizationComponent[] labels = summary.GetComponentsInChildren<TextLocalizationComponent>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TextLocalizationComponent label = labels[i];
                string key;

                switch (label.Key)
                {
                    case VanillaTitleKey:
                        key = "quickrid_summary_title";
                        break;
                    case VanillaCountKey:
                        key = "quickrid_summary_trips";
                        break;
                    case VanillaGrossKey:
                        key = "quickrid_summary_earnings";
                        break;
                    case VanillaNetKey:
                        key = "quickrid_summary_payout";
                        break;
                    case VanillaDamagesKey when ratingShown:
                        key = "quickrid_summary_rating";
                        break;
                    default:
                        continue;
                }

                label.SetData(new LanguageChangeEventDataHolder { Key = key });
            }

            if (!ratingShown)
                logger?.Warn("QuickRid: Wertfeld der Schadenszeile nicht gefunden – Bewertung fehlt in der Übersicht.");
        }

        /// <summary>Durchschnitt der Sitzung, z. B. "4.3 / 5".</summary>
        private static string FormatSessionRating(QuickRidMission mission)
        {
            float average = mission.sessionTrips > 0
                ? (float)mission.sessionStarsTotal / mission.sessionTrips
                : 0f;

            return average.ToString("0.0", CultureInfo.InvariantCulture) + " / 5";
        }

        /// <summary>Setzt den Text eines privaten Wertfelds der Übersicht.</summary>
        /// <remarks>
        /// Die Wertfelder sind <c>[SerializeField] private TextMeshProUGUI</c>. TextMeshPro ist in
        /// dieser asmdef nicht referenziert, deshalb läuft auch der Zugriff auf <c>text</c> über
        /// Reflection statt über den Typ. Schlägt etwas fehl, bleibt die Zeile unverändert.
        /// </remarks>
        private static bool TrySetLabelText(DeliveryJobSummary summary, string fieldName, string value)
        {
            FieldInfo? field = typeof(DeliveryJobSummary).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            object? label = field?.GetValue(summary);

            // Ein zerstörtes UnityEngine.Object ist nur über den Unity-Vergleich als null erkennbar.
            if (label == null || (label is UnityEngine.Object unityObject && unityObject == null))
                return false;

            PropertyInfo? text = label.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (text == null || !text.CanWrite)
                return false;

            text.SetValue(label, value);
            return true;
        }
    }
}
