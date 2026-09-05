#nullable enable
using System;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Mods;

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

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        /// <remarks>
        /// AddSlider(id, label, min, max, defaultValue, onValueChanged, valueLabelKey) – nur int,
        /// keine Schrittweite. Der onValueChanged-Callback feuert auch einmal beim Aufbau der UI mit
        /// dem gespeicherten Wert; er ist damit zugleich der Lesepfad für persistierte Einstellungen.
        /// </remarks>
        public Task OnLoadAsync(ModContext context)
        {
            _context = context;

            var options = new ModOptions()
                .AddHeader("quickrid_options_header")
                .AddSlider("fare_multiplier", "quickrid_fare_multiplier", 50, 300,
                    QuickRidSettings.FareMultiplierPercent, v => QuickRidSettings.FareMultiplierPercent = v,
                    "quickrid_percent_value")
                .AddSlider("time_allowance", "quickrid_time_allowance", 100, 300,
                    QuickRidSettings.TimeAllowancePercent, v => QuickRidSettings.TimeAllowancePercent = v,
                    "quickrid_percent_value")
                .AddHeader("quickrid_options_trip_header")
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
                    "quickrid_meters_value")
                .AddSplitter();

            OptionsService.Register(context.ModId, options);
            context.Logger.Info("QuickRid options registered.");
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            if (_context != null)
                OptionsService.RemoveModOptions(_context.ModId);
            _context = null;
            return Task.CompletedTask;
        }
    }
}
