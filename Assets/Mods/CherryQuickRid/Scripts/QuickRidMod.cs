#nullable enable
using System;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Mods;
using UI.Notification;
using UnityEngine;

[assembly: RegisterModClass(typeof(CherryQuickRid.QuickRidMod))]

namespace CherryQuickRid
{
    /// <summary>
    /// Einstiegspunkt beim Spielstart: registriert den Options-Eintrag.
    /// Alles, was die geladene Stadt braucht, gehört in <see cref="QuickRidCityMod"/>.
    /// </summary>
    [ModEntryOnInitializationLoad]
    public sealed class QuickRidMod : IModBigAmbitions
    {
        private ModContext? _context;

        /// <summary>Setzt die Beschriftung des Sperrlisten-Knopfes nach; siehe QuickRidOptionsUiFixer.</summary>
        private GameObject? _optionsUiFixer;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        /// <remarks>
        /// AddSlider(id, label, min, max, defaultValue, onValueChanged, valueLabelKey) und
        /// AddDropdown(id, label, choiceKeys, defaultIndex, onValueChanged) – die Choice-Keys werden
        /// wie alle Beschriftungen lokalisiert. Der onValueChanged-Callback feuert auch einmal beim
        /// Aufbau der UI mit dem gespeicherten Wert; er ist damit zugleich der Lesepfad für
        /// persistierte Einstellungen.
        /// </remarks>
        public Task OnLoadAsync(ModContext context)
        {
            _context = context;

            // Die Sperrliste liegt als Datei im ModData-Ordner und gilt spielstandübergreifend;
            // sie muss stehen, bevor die erste Stadt geladen wird.
            QuickRidBlacklist.Initialize(context);

            var options = new ModOptions()
                .AddHeader("quickrid_options_header")
                .AddDropdown("difficulty", "quickrid_difficulty", QuickRidDifficulty.ChoiceKeys,
                    (int)QuickRidSettings.DifficultyChoice, OnDifficultyChanged);

            // Die Feinjustierung ist Entwicklerwerkzeug und bleibt dem Spieler erspart. Der Block
            // ist per Konstante abgeschaltet, deshalb hält der Compiler ihn für unerreichbar.
#pragma warning disable CS0162
            if (QuickRidSettings.DeveloperOptions)
            {
                options
                    .AddHeader("quickrid_options_developer_header")
                    .AddSlider("fare_multiplier", "quickrid_fare_multiplier", 50, 300,
                        QuickRidSettings.FareMultiplierPercent, v => QuickRidSettings.FareMultiplierPercent = v,
                        "quickrid_percent_value")
                    .AddSlider("time_allowance", "quickrid_time_allowance", 100, 300,
                        QuickRidSettings.TimeAllowancePercent, v => QuickRidSettings.TimeAllowancePercent = v,
                        "quickrid_percent_value")
                    .AddSlider("tip_chance", "quickrid_tip_chance", 0, 300,
                        QuickRidSettings.TipChancePercent, v => QuickRidSettings.TipChancePercent = v,
                        "quickrid_percent_value")
                    .AddSlider("request_wait_min", "quickrid_request_wait_min", 1, 120,
                        QuickRidSettings.RequestWaitMinMinutes, v => QuickRidSettings.RequestWaitMinMinutes = v,
                        "quickrid_minutes_value")
                    .AddSlider("request_wait_max", "quickrid_request_wait_max", 1, 240,
                        QuickRidSettings.RequestWaitMaxMinutes, v => QuickRidSettings.RequestWaitMaxMinutes = v,
                        "quickrid_minutes_value")
                    .AddSlider("offer_timeout", "quickrid_offer_timeout", 1, 120,
                        QuickRidSettings.OfferTimeoutMinutes, v => QuickRidSettings.OfferTimeoutMinutes = v,
                        "quickrid_minutes_value")
                    .AddSlider("passenger_search_radius", "quickrid_passenger_search_radius", 50, 1000,
                        QuickRidSettings.PassengerSearchRadiusMeters, v => QuickRidSettings.PassengerSearchRadiusMeters = v,
                        "quickrid_meters_value")
                    .AddSlider("pickup_radius", "quickrid_pickup_radius", 3, 25,
                        QuickRidSettings.PickupRadiusMeters, v => QuickRidSettings.PickupRadiusMeters = v,
                        "quickrid_meters_value")
                    .AddSlider("dropoff_radius", "quickrid_dropoff_radius", 3, 30,
                        QuickRidSettings.DropoffRadiusMeters, v => QuickRidSettings.DropoffRadiusMeters = v,
                        "quickrid_meters_value")
                    .AddSlider("min_trip_distance", "quickrid_min_trip_distance", 100, 2000,
                        QuickRidSettings.MinTripDistanceMeters, v => QuickRidSettings.MinTripDistanceMeters = v,
                        "quickrid_meters_value")
                    .AddSlider("max_trip_distance", "quickrid_max_trip_distance", 300, 4000,
                        QuickRidSettings.MaxTripDistanceMeters, v => QuickRidSettings.MaxTripDistanceMeters = v,
                        "quickrid_meters_value");
            }
#pragma warning restore CS0162

            options
                .AddHeader("quickrid_options_blacklist_header")
                .AddButton("quickrid_blacklist_reset", RequestResetBlacklist)
                .AddSplitter();

            OptionsService.Register(context.ModId, options);

            // Der Knopf der Sperrliste zeigt sonst einen Platzhalter statt seiner Beschriftung.
            if (_optionsUiFixer == null)
            {
                _optionsUiFixer = new GameObject("QuickRid - Options UI Fixer");
                UnityEngine.Object.DontDestroyOnLoad(_optionsUiFixer);
                _optionsUiFixer.AddComponent<QuickRidOptionsUiFixer>();
            }

            context.Logger.Info("QuickRid options registered.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Fragt vor dem Zurücksetzen der Sperrliste nach. Ohne laufendes Spiel passiert nichts.
        /// </summary>
        /// <remarks>
        /// <c>HudConfirm.Show</c> führt die Bestätigungsaktion sofort und ohne Dialog aus, wenn kein
        /// HUD gebunden ist (<c>onShow == null</c>) – im Hauptmenü wäre der Reset also ein Klick
        /// ohne Rückfrage. Deshalb der Guard vor dem Aufruf statt in der Aktion.
        /// </remarks>
        private void RequestResetBlacklist()
        {
            if (HudConfirm.isOpen)
                return;

            if (HudConfirm.onShow == null)
            {
                _context?.Logger.Warn(
                    "QuickRid: Sperrliste zurücksetzen geht nur im laufenden Spiel – " +
                    "im Hauptmenü gibt es keinen Bestätigungsdialog.");
                return;
            }

            HudConfirm.Show(
                "quickrid_blacklist_reset_title",
                "quickrid_blacklist_reset_body",
                ResetBlacklist,
                null,
                "quickrid_blacklist_reset_confirm",
                "quickrid_decline_job",
                false);
        }

        private void ResetBlacklist()
        {
            QuickRidBlacklist.ResetToDefaults();
            Notifications.Show(NotificationType.Info, "quickrid_blacklist_reset_done");
        }

        /// <summary>
        /// Übernimmt die Auswahl aus dem Dropdown. „Wie Spiel" wird beim Laden eines Spielstands
        /// erneut aufgelöst (QuickRidController.RestoreState), weil im Hauptmenü noch keiner steht.
        /// </summary>
        private void OnDifficultyChanged(int index)
        {
            int clamped = Mathf.Clamp(index, 0, QuickRidDifficulty.ChoiceKeys.Length - 1);
            QuickRidSettings.DifficultyChoice = (QuickRidDifficultyChoice)clamped;
            QuickRidDifficulty.Apply(_context?.Logger);
        }

        public Task OnUnloadAsync()
        {
            if (_optionsUiFixer != null)
            {
                UnityEngine.Object.Destroy(_optionsUiFixer);
                _optionsUiFixer = null;
            }

            if (_context != null)
                OptionsService.RemoveModOptions(_context.ModId);
            _context = null;
            return Task.CompletedTask;
        }
    }
}
