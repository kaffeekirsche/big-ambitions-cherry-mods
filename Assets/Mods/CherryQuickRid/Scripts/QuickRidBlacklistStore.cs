#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using BAModAPI;
using UnityEngine;

namespace CherryQuickRid
{
    /// <summary>Ein gesperrter Eintrag der JSON-Datei.</summary>
    [Serializable]
    public sealed class QuickRidBlacklistEntry
    {
        /// <summary>Straßen-Key des Spiels, z. B. "ba:street_thirdstreet".</summary>
        public string streetName = string.Empty;

        public int streetNumber;

        /// <summary>Lesbare Adresse ("45 Third Street"), nur zur Orientierung beim Handbearbeiten.</summary>
        public string label = string.Empty;

        /// <summary><see cref="QuickRidBlacklist.SourceDefault"/> oder <see cref="QuickRidBlacklist.SourceManual"/>.</summary>
        public string source = string.Empty;

        /// <summary>Realzeit des Eintrags im Format ISO 8601 (UTC).</summary>
        public string addedAt = string.Empty;
    }

    [Serializable]
    public sealed class QuickRidBlacklistFile
    {
        public int version = 1;
        public List<QuickRidBlacklistEntry> entries = new List<QuickRidBlacklistEntry>();
    }

    /// <summary>
    /// Liest und schreibt die Sperrliste als JSON unter
    /// <c>&lt;persistentDataPath&gt;/ModData/&lt;ModId&gt;/blacklist.json</c> – dem Ort, an den auch die
    /// Workshop-Mod „Auto Price Maximizer" ihre Einstellungen legt. Spielstandübergreifend und
    /// außerhalb des Mod-Ordners, der bei Workshop-Installationen schreibgeschützt sein kann.
    /// </summary>
    /// <remarks>
    /// <see cref="ModContext.ModId"/> ist zur Laufzeit nicht zwingend der Name aus dem Manifest: im
    /// Test kam dort der vollständige Mod-Pfad an. <c>Path.Combine</c> verwirft dann alle vorherigen
    /// Bestandteile, weil das letzte Stück absolut ist – die Datei landete im Mod-Ordner statt unter
    /// ModData. <see cref="ToFolderName"/> nimmt deshalb nur den letzten Pfadbestandteil.
    /// </remarks>
    public static class QuickRidBlacklistStore
    {
        public const string FileName = "blacklist.json";

        private const string ModDataFolder = "ModData";

        /// <summary>Notnagel, falls die ModId leer oder unbrauchbar ist.</summary>
        private const string FallbackFolderName = "CherryQuickRid";

        /// <summary>
        /// Macht aus der ModId einen einzelnen, gültigen Ordnernamen: bei einem Pfad bleibt der
        /// letzte Bestandteil, ungültige Zeichen werden ersetzt.
        /// </summary>
        public static string ToFolderName(string? modId)
        {
            if (string.IsNullOrEmpty(modId))
                return FallbackFolderName;

            string value = modId!.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            int separator = value.LastIndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            if (separator >= 0)
                value = value.Substring(separator + 1);

            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                value = value.Replace(invalid[i], '_');

            value = value.Trim();
            return value.Length == 0 ? FallbackFolderName : value;
        }

        public static string GetDirectory(string? modId)
        {
            return Path.Combine(Application.persistentDataPath, ModDataFolder, ToFolderName(modId));
        }

        public static string GetPath(string? modId)
        {
            return Path.Combine(GetDirectory(modId), FileName);
        }

        /// <summary>Absoluter, aufgelöster Pfad – das, was im Dateisystem wirklich angefasst wird.</summary>
        public static string GetFullPath(string? modId)
        {
            try
            {
                return Path.GetFullPath(GetPath(modId));
            }
            catch (Exception)
            {
                return GetPath(modId);
            }
        }

        /// <summary>
        /// Einmalige Zeile fürs Log, damit der Speicherort im Fehlerfall nachvollziehbar ist.
        /// Diagnose: der Pfad interessiert nur beim Nachstellen eines Fehlers.
        /// </summary>
        public static void LogLocation(ModContext context, IModLogger? logger)
        {
            QuickRidLog.Dev(logger,
                $"QuickRid: Sperrliste liegt unter {GetFullPath(context.ModId)} " +
                $"(persistentDataPath: {Application.persistentDataPath}, " +
                $"ModId: \"{context.ModId}\" -> Ordner \"{ToFolderName(context.ModId)}\", " +
                $"ModRootPath: {context.ModRootPath}).");
        }

        /// <summary>
        /// Liest die Datei. Rückgabe true nur, wenn sie existiert und lesbar war. Eine vorhandene,
        /// aber kaputte Datei setzt <paramref name="fileExists"/> und liefert false – der Aufrufer
        /// darf sie dann nicht blind überschreiben.
        /// </summary>
        public static bool TryLoad(string? modId, IModLogger? logger, out QuickRidBlacklistFile file, out bool fileExists)
        {
            file = new QuickRidBlacklistFile();

            string path = GetFullPath(modId);

            try
            {
                fileExists = File.Exists(path);
            }
            catch (Exception exception)
            {
                fileExists = false;
                logger?.Warn($"QuickRid: Sperrliste {path} nicht prüfbar – {exception.Message}");
                return false;
            }

            if (!fileExists)
                return false;

            try
            {
                string json = File.ReadAllText(path);

                if (!QuickRidJson.TryRead(json, out QuickRidBlacklistFile parsed))
                {
                    logger?.Warn($"QuickRid: Sperrliste {path} ist kein gültiges JSON. " +
                        "Die Datei bleibt unverändert, bis sie repariert oder gelöscht wird.");
                    return false;
                }

                file = parsed;
                return true;
            }
            catch (Exception exception)
            {
                logger?.Warn($"QuickRid: Sperrliste {path} nicht lesbar – {exception.Message}. Datei bleibt unverändert.");
                return false;
            }
        }

        /// <summary>Schreibt die Datei und prüft danach, dass sie wirklich auf der Platte steht.</summary>
        public static bool Save(string? modId, QuickRidBlacklistFile file, IModLogger? logger)
        {
            string directory = GetDirectory(modId);
            string path = GetFullPath(modId);

            file.entries ??= new List<QuickRidBlacklistEntry>();

            try
            {
                Directory.CreateDirectory(directory);

                string json = QuickRidJson.Write(file);
                File.WriteAllText(path, json);

                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    logger?.Warn($"QuickRid: Sperrliste {path} nach dem Schreiben nicht auffindbar.");
                    return false;
                }

                QuickRidLog.Dev(logger, $"QuickRid: Sperrliste gespeichert – {file.entries.Count} Einträge, " +
                    $"{info.Length} Bytes, {path}");
                return true;
            }
            catch (Exception exception)
            {
                logger?.Warn($"QuickRid: Sperrliste {path} konnte nicht geschrieben werden – " +
                    $"{exception.GetType().Name}: {exception.Message}");
                return false;
            }
        }
    }
}
