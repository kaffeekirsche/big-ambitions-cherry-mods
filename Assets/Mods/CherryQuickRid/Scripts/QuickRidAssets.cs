#nullable enable
using System.Linq;
using BAModAPI;
using BAModAPI.Services;
using UnityEngine;

namespace CherryQuickRid
{
    /// <summary>
    /// Zugriff auf das AssetBundle der Mod. Es enthält bisher nur das Kartensymbol.
    /// </summary>
    /// <remarks>
    /// Der Bundle-Schlüssel ist der Pfad relativ zum Mod-Ordner, nicht der Plattform-Unterordner:
    /// die Auswahl zwischen Windows und Mac trifft die Laufzeit selbst. Der Name hinter dem
    /// Schrägstrich muss dem <c>AssetBundleName</c> im Manifest entsprechen, sonst findet
    /// <see cref="AssetService"/> die registrierte Datei nicht.
    /// </remarks>
    internal static class QuickRidAssets
    {
        public const string BundleKey = "AssetBundles/quickrid.unity3d";

        /// <summary>Voller Asset-Pfad im Bundle – so legt der Mod Builder die Assets ab.</summary>
        private const string MapIconAssetPath = "Assets/Mods/CherryQuickRid/Icons/quickrid_map.png";

        /// <summary>Dateiname ohne Endung; Rückfallweg, falls der volle Pfad nicht greift.</summary>
        private const string MapIconName = "quickrid_map";

        /// <summary>
        /// Das Symbol des Kartenfilters. Null ist kein Fehlerfall: der Filter nimmt dann das
        /// Symbol des Vanilla-Lieferjobs (siehe <see cref="QuickRidMapFilter"/>).
        /// </summary>
        public static Sprite? LoadMapIcon(ModContext context)
        {
            AssetBundle bundle = AssetService.GetBundle(context.ModId, BundleKey, true);
            if (bundle == null)
            {
                context.Logger.Warn(
                    $"QuickRid: AssetBundle \"{BundleKey}\" nicht geladen – " +
                    "der Kartenfilter benutzt das Symbol des Lieferjobs.");
                return null;
            }

            Sprite? icon = bundle.LoadAsset<Sprite>(MapIconAssetPath);

            if (icon == null)
            {
                // Der Pfad im Bundle hängt am Import-Zeitpunkt; der Name ist stabiler.
                icon = bundle.LoadAllAssets<Sprite>().FirstOrDefault(s => s != null && s.name == MapIconName);
                QuickRidLog.Dev(context.Logger,
                    $"QuickRid: \"{MapIconAssetPath}\" nicht im Bundle – Suche über den Namen " +
                    $"ergab {(icon != null ? "einen Treffer" : "nichts")}.");
            }

            if (icon == null)
            {
                context.Logger.Warn(
                    $"QuickRid: Kartensymbol \"{MapIconName}\" nicht im Bundle gefunden – " +
                    "der Kartenfilter benutzt das Symbol des Lieferjobs.");
                return null;
            }

            context.Logger.Info($"QuickRid: Kartensymbol \"{icon.name}\" geladen.");
            return icon;
        }
    }
}
