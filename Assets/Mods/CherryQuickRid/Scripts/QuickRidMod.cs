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
