#nullable enable
using System;
using System.Collections.Generic;
using BAModAPI;
using Streets;

namespace CherryQuickRid
{
    /// <summary>
    /// Sperrliste für Adressen, die weder Abhol- noch Zieladresse sein dürfen: feste Standardliste
    /// im Code plus manuelle Einträge aus dem Spiel ("Adresse ausschließen"), gespeichert über
    /// <see cref="QuickRidBlacklistStore"/>.
    /// </summary>
    /// <remarks>
    /// Geprüft wird zur Auswahlzeit in <c>QuickRidController.TryCreateRequest</c>, nicht beim
    /// Aufbau des Adress-Caches – so wirken Melden und Zurücksetzen sofort, ohne Neuaufbau.
    /// <para>
    /// Schlüssel ist "streetName|streetNumber" als String: <c>Address</c> liegt in einem externen
    /// Assembly, und ob neben <c>==</c> auch <c>Equals(object)</c> sauber ist, lässt sich nicht
    /// nachlesen. Ein String-Schlüssel ist davon unabhängig.
    /// </para>
    /// <para>
    /// Standardeinträge gelten immer: fehlen sie in der Datei, werden sie beim Laden ergänzt.
    /// "Zurücksetzen" schreibt eine Datei, die nur die Standardeinträge enthält.
    /// </para>
    /// </remarks>
    public static class QuickRidBlacklist
    {
        public const string SourceDefault = "default";
        public const string SourceManual = "manual";

        /// <summary>
        /// Feste Sperren, Format <c>("ba:street_thirdstreet", 45)</c>. Vorerst leer – Adressen, die
        /// im Spiel auffallen, kommen hier hinein. Den Straßen-Key nennt die JSON-Datei zu jedem
        /// gemeldeten Eintrag.
        /// </summary>
        private static readonly (string streetName, int streetNumber)[] Defaults =
        {
            // ("ba:street_thirdstreet", 45),
        };

        private static string? _modId;
        private static IModLogger? _logger;

        private static readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);
        private static QuickRidBlacklistFile _file = new QuickRidBlacklistFile();

        /// <summary>
        /// Datei vorhanden, aber nicht lesbar. Dann schreibt <see cref="Load"/> nichts zurück, damit
        /// ein Tippfehler beim Handbearbeiten nicht alle Einträge kostet. Eine bewusste Aktion des
        /// Spielers (Melden, Zurücksetzen) hebt die Sperre auf.
        /// </summary>
        private static bool _fileUnreadable;

        public static int Count => _keys.Count;

        public static void Initialize(ModContext context)
        {
            _modId = context.ModId;
            _logger = context.Logger;

            QuickRidBlacklistStore.LogLocation(context, _logger);
            Load();
        }

        /// <summary>Datei lesen (oder leer beginnen), Standardeinträge ergänzen, Schlüssel aufbauen.</summary>
        public static void Load()
        {
            _keys.Clear();
            _file = new QuickRidBlacklistFile();
            _fileUnreadable = false;

            bool loaded = QuickRidBlacklistStore.TryLoad(_modId, _logger, out QuickRidBlacklistFile file, out bool exists);

            if (loaded)
                _file = file;
            else if (exists)
                _fileUnreadable = true; // nur bei kaputtem JSON, nicht bei einer gültigen leeren Datei

            bool changed = MergeDefaults();
            RebuildKeys();

            if (_fileUnreadable)
            {
                _logger?.Warn($"QuickRid: Sperrliste wird nicht überschrieben, solange die Datei unlesbar ist " +
                    $"({_keys.Count} Adressen nur im Speicher).");
                return;
            }

            // Eine fehlende Datei wird angelegt, damit der Spieler sie zum Bearbeiten vorfindet.
            if (changed || !exists)
                Save();

            _logger?.Info($"QuickRid: Sperrliste geladen – {_keys.Count} Adressen.");
        }

        public static bool Contains(Address? address)
        {
            return address != null && _keys.Contains(KeyOf(address.streetName, address.streetNumber));
        }

        /// <summary>Trägt eine Adresse ein und speichert. False, wenn sie schon gesperrt war.</summary>
        public static bool Add(Address address, string source)
        {
            string key = KeyOf(address.streetName, address.streetNumber);
            if (!_keys.Add(key))
            {
                _logger?.Info($"QuickRid: {AddressHelper.ToFormattedString(address)} stand bereits auf der Sperrliste.");
                return false;
            }

            _file.entries.Add(new QuickRidBlacklistEntry
            {
                streetName = address.streetName,
                streetNumber = address.streetNumber,
                label = AddressHelper.ToFormattedString(address),
                source = source,
                addedAt = DateTime.UtcNow.ToString("o")
            });

            // Ein gemeldeter Ausschluss ist eine bewusste Entscheidung und darf eine kaputte Datei ersetzen.
            _fileUnreadable = false;
            Save();
            return true;
        }

        /// <summary>Verwirft alle manuellen Einträge; die Datei enthält danach nur die Standardliste.</summary>
        public static void ResetToDefaults()
        {
            _keys.Clear();
            _file = new QuickRidBlacklistFile();
            _fileUnreadable = false;

            MergeDefaults();
            RebuildKeys();
            Save();

            _logger?.Info($"QuickRid: Sperrliste zurückgesetzt – {_keys.Count} Standardadressen.");
        }

        /// <summary>Ergänzt fehlende Standardeinträge. True, wenn etwas dazukam.</summary>
        private static bool MergeDefaults()
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _file.entries.Count; i++)
                present.Add(KeyOf(_file.entries[i].streetName, _file.entries[i].streetNumber));

            bool changed = false;
            for (int i = 0; i < Defaults.Length; i++)
            {
                (string streetName, int streetNumber) = Defaults[i];
                if (!present.Add(KeyOf(streetName, streetNumber)))
                    continue;

                _file.entries.Add(new QuickRidBlacklistEntry
                {
                    streetName = streetName,
                    streetNumber = streetNumber,
                    label = SafeLabel(streetName, streetNumber),
                    source = SourceDefault,
                    addedAt = DateTime.UtcNow.ToString("o")
                });
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Lesbare Adresse für Standardeinträge. Beim Laden zum Spielstart ist die Lokalisierung
        /// eventuell noch nicht bereit, deshalb mit Rückfall auf den rohen Key.
        /// </summary>
        private static string SafeLabel(string streetName, int streetNumber)
        {
            try
            {
                return AddressHelper.ToFormattedString(new Address(streetName, streetNumber));
            }
            catch (Exception)
            {
                return streetNumber + " " + streetName;
            }
        }

        private static void RebuildKeys()
        {
            _keys.Clear();
            for (int i = 0; i < _file.entries.Count; i++)
                _keys.Add(KeyOf(_file.entries[i].streetName, _file.entries[i].streetNumber));
        }

        private static void Save()
        {
            if (_modId == null)
            {
                _logger?.Warn("QuickRid: Sperrliste nicht gespeichert – die Mod wurde nie initialisiert.");
                return;
            }

            QuickRidBlacklistStore.Save(_modId, _file, _logger);
        }

        private static string KeyOf(string streetName, int streetNumber)
        {
            return streetName + "|" + streetNumber;
        }
    }
}
