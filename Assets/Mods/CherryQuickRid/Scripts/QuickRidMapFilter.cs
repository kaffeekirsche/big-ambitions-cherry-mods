#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using City.CityMap;
using Helpers;
using Localizor.LanguageChangeEvent;
using Streets;
using UI;
using UnityEngine;

namespace CherryQuickRid
{
    /// <summary>
    /// Filter „QuickRid" in der Kategorie Jobs der Stadtkarte und der Kartenpunkt der laufenden
    /// Fahrt (Abholung, nach dem Einsteigen das Ziel).
    /// </summary>
    /// <remarks>
    /// <c>CityMapFilters.CreateFilter</c> und die Sammlungen <c>_filterEntries</c> und
    /// <c>CityMapFilterCategory._filters</c> sind privat; das Spiel bietet keinen Weg, einen
    /// eigenen Filter einzuhängen. Der Zugriff läuft deshalb über Reflection. Schlägt er fehl,
    /// bleibt es bei einer einzigen Warnung und die Mod arbeitet ohne Filter weiter.
    /// <para>
    /// Der Filtername ist zugleich der Locale-Key seiner Beschriftung
    /// (<c>CityMapFilter.SetUp</c> setzt <c>label.Key = filterName</c>), deshalb
    /// <c>quickrid_map_filter</c> und kein technischer Name. An- und Abwahl speichert das Spiel
    /// selbst in <c>SaveGameManager.Current.SelectedCitymapFilters</c> und stellt sie beim Öffnen
    /// der Karte wieder her.
    /// </para>
    /// <para>
    /// Der Aufbau läuft erst, wenn <c>CityMapFilters.Start</c> durch ist – vorher ist die Sammlung
    /// leer, weil <c>InitializeFilters</c> sie zuerst leert. Bis der Lieferfahrer-Filter dasteht,
    /// kehrt <see cref="Tick"/> deshalb still zurück; das ist der Normalfall in den ersten Frames
    /// einer Stadt und darf nicht protokolliert werden.
    /// </para>
    /// </remarks>
    internal sealed class QuickRidMapFilter
    {
        /// <summary>Name des Filters und Locale-Key seiner Beschriftung in einem.</summary>
        private const string FilterKey = "quickrid_map_filter";

        /// <summary>Der Vanilla-Lieferjob; unser Filter kommt direkt darunter.</summary>
        private const string DeliveryFilterKey = "ba:skill_deliverydriver";

        private const string JobsCategoryKey = "citymap_category_jobs";

        private readonly IModLogger? _logger;
        private readonly Sprite? _icon;

        private CityMapFilter? _filter;

        private PointOfInterest? _poi;
        private GameObject? _poiAnchor;

        /// <summary>Adresse des aktiven Kartenpunkts, oder null wenn gerade keine Fahrt läuft.</summary>
        private Address? _activeAddress;

        /// <summary>
        /// Steht der Kartenpunkt schon an seiner Weltposition? Beim Einsteigen ist das Zielgebäude
        /// nicht immer sofort auflösbar, dann zieht <see cref="Tick"/> es nach.
        /// </summary>
        private bool _positionResolved;

        /// <summary>Einmal warnen, nicht in jedem Frame – <see cref="Tick"/> läuft dauernd.</summary>
        private bool _reflectionFailed;

        public QuickRidMapFilter(IModLogger? logger, Sprite? icon)
        {
            _logger = logger;
            _icon = icon;
        }

        /// <summary>
        /// Legt den Filter an, sobald die Karte bereit ist, und zieht eine noch offene Zielposition
        /// nach. Läuft in jedem Frame – hier darf nichts protokolliert werden.
        /// </summary>
        public void Tick()
        {
            if (_reflectionFailed)
                return;

            if (SaveGameManager.Current == null)
                return;
            if (!InstanceBehavior<UIs>.IsInitialized || !InstanceBehavior<CityManager>.IsInitialized)
                return;
            if (InstanceBehavior<CityManager>.Instance.cityMap == null)
                return;

            if (_filter == null)
                TryCreateFilter();

            // Die Zieladresse steht manchmal erst ein paar Frames nach dem Einsteigen bereit.
            if (!_positionResolved && _activeAddress != null)
                TryPlaceAtAddress(_activeAddress);
        }

        // --- Filter --------------------------------------------------------------

        private void TryCreateFilter()
        {
            CityMapFilters mapFilters = InstanceBehavior<UIs>.Instance.mapFilters;
            if (mapFilters == null)
                return;

            FieldInfo? entriesField = typeof(CityMapFilters)
                .GetField("_filterEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo? createMethod = typeof(CityMapFilters)
                .GetMethod("CreateFilter", BindingFlags.Instance | BindingFlags.NonPublic);

            if (entriesField == null || createMethod == null)
            {
                _reflectionFailed = true;
                _logger?.Warn("QuickRid: CityMapFilters hat sich geändert – kein Kartenfilter.");
                return;
            }

            if (!(entriesField.GetValue(mapFilters) is Dictionary<string, CityMapFilter> entries))
            {
                _reflectionFailed = true;
                _logger?.Warn("QuickRid: Filterliste der Karte nicht lesbar – kein Kartenfilter.");
                return;
            }

            // Solange der Lieferfahrer-Filter fehlt, hat CityMapFilters.Start noch nicht gebaut.
            if (!entries.TryGetValue(DeliveryFilterKey, out CityMapFilter? deliveryFilter) || deliveryFilter == null)
                return;

            if (!entries.TryGetValue(FilterKey, out CityMapFilter? filter) || filter == null)
            {
                CityMapFilterCategory category = mapFilters.GetCategory(JobsCategoryKey);
                if (category == null)
                {
                    _reflectionFailed = true;
                    _logger?.Warn($"QuickRid: Kartenkategorie \"{JobsCategoryKey}\" fehlt – kein Kartenfilter.");
                    return;
                }

                object? created = createMethod.Invoke(mapFilters, new object?[]
                {
                    FilterKey,
                    _icon != null ? _icon : mapFilters.deliveryJobIcon,
                    null,
                    category,
                    default(LanguageChangeEventDataHolder),
                    null,
                    (Func<Vector3?>)ResolveFocusPoint,
                });

                filter = created as CityMapFilter;
                if (filter == null)
                {
                    _reflectionFailed = true;
                    _logger?.Warn("QuickRid: Kartenfilter konnte nicht angelegt werden.");
                    return;
                }

                InsertBelow(category, deliveryFilter, filter);
            }

            _filter = filter;

            bool selected = SaveGameManager.Current.SelectedCitymapFilters.Contains(FilterKey);
            _filter.Toggle.SetIsOnWithoutNotify(selected);

            // Erst abmelden: findet sich ein bereits vorhandener Filter wieder, hängt unser
            // Zuhörer sonst zweimal daran.
            _filter.Toggle.onValueChanged.RemoveListener(OnToggled);
            _filter.Toggle.onValueChanged.AddListener(OnToggled);

            UpdatePoiVisibility();
            _logger?.Info("QuickRid: Kartenfilter unter dem Lieferfahrer eingehängt.");
        }

        /// <summary>
        /// Schiebt den Filter unter den Lieferfahrer – sichtbar in der Anzeige und in der Liste der
        /// Kategorie, die deren Sammelschalter durchläuft.
        /// </summary>
        private static void InsertBelow(CityMapFilterCategory category, CityMapFilter above, CityMapFilter filter)
        {
            filter.transform.SetSiblingIndex(above.transform.GetSiblingIndex() + 1);

            FieldInfo? filtersField = typeof(CityMapFilterCategory)
                .GetField("_filters", BindingFlags.Instance | BindingFlags.NonPublic);

            if (!(filtersField?.GetValue(category) is List<CityMapFilter> filters))
                return;

            filters.Remove(filter);
            int index = filters.IndexOf(above);
            filters.Insert(index >= 0 ? index + 1 : filters.Count, filter);
        }

        private void OnToggled(bool isOn)
        {
            UpdatePoiVisibility();
        }

        private bool IsFilterOn()
        {
            return _filter != null && _filter.Toggle.isOn;
        }

        /// <summary>Ziel des Fokus-Knopfes neben dem Filter: der aktive Kartenpunkt.</summary>
        private Vector3? ResolveFocusPoint()
        {
            if (_poiAnchor == null || _activeAddress == null || !_positionResolved)
                return null;

            return _poiAnchor.transform.position;
        }

        // --- Kartenpunkt ---------------------------------------------------------

        /// <summary>Setzt den Kartenpunkt auf den wartenden Fahrgast.</summary>
        public void ShowPickup(Vector3 position, Address? address)
        {
            if (address == null)
                return;

            _activeAddress = address;
            Place(position, address);
        }

        /// <summary>
        /// Setzt den Kartenpunkt auf das Fahrtziel. Ist die Adresse noch nicht auflösbar, holt
        /// <see cref="Tick"/> das nach.
        /// </summary>
        public void ShowDestination(Address? address)
        {
            if (address == null)
                return;

            _activeAddress = address;
            _positionResolved = false;

            if (!TryPlaceAtAddress(address))
                UpdatePoiVisibility();
        }

        private bool TryPlaceAtAddress(Address address)
        {
            Transform entrance = BuildingHelper.GetAddressEntranceTransform(address);
            if (entrance == null)
                return false;

            Place(entrance.position, address);
            return true;
        }

        private void Place(Vector3 position, Address address)
        {
            if (!EnsurePoi() || _poi == null || _poiAnchor == null)
                return;

            _poiAnchor.transform.position = position;
            _poi.SetText(AddressHelper.ToFormattedString(address));
            _positionResolved = true;

            UpdatePoiVisibility();
        }

        /// <summary>Nimmt den Kartenpunkt von der Karte; der Filter selbst bleibt stehen.</summary>
        public void Clear()
        {
            _activeAddress = null;
            _positionResolved = false;
            UpdatePoiVisibility();
        }

        /// <remarks>
        /// Bewusst ohne <c>Address</c>-Argument an <c>AddPoi</c>: das Spiel leitet daraus sonst
        /// einen Gebäudeeintrag im Spielstand ab. Aus demselben Grund hängt der Kartenpin der
        /// Abholung an einer Position statt an einer Adresse (siehe QuickRidController.PinPickup).
        /// </remarks>
        private bool EnsurePoi()
        {
            if (_poi != null && _poiAnchor != null)
                return true;

            if (!InstanceBehavior<CityManager>.IsInitialized)
                return false;

            CityMap cityMap = InstanceBehavior<CityManager>.Instance.cityMap;
            if (cityMap == null)
                return false;

            Sprite icon = _icon != null
                ? _icon
                : InstanceBehavior<GlobalReferences>.Instance.vehiclePOIIcon;

            if (_poiAnchor == null)
                _poiAnchor = new GameObject("QuickRid - Map POI Anchor");

            _poi = cityMap.AddPoi(_poiAnchor.transform, icon, Colors.Lime, string.Empty, null);
            if (_poi == null)
                return false;

            // Permanent heißt: das Spiel blendet ihn nicht selbst wieder aus – die Sichtbarkeit
            // steuert allein unser Filter.
            _poi.SetPermanent(true);
            _poi.SetHidden(true);
            return true;
        }

        private void UpdatePoiVisibility()
        {
            if (_poi == null)
                return;

            _poi.SetHidden(!(IsFilterOn() && _activeAddress != null && _positionResolved));
        }

        // --- Lebenszyklus --------------------------------------------------------

        /// <summary>
        /// Beim Verlassen der Stadt: Filter und Kartenpunkt sterben mit der Szene, hier werden nur
        /// die Verweise darauf gelöst.
        /// </summary>
        public void ForgetSceneObjects()
        {
            _filter = null;
            _poi = null;
            _poiAnchor = null;
            _activeAddress = null;
            _positionResolved = false;
        }

        public void Destroy()
        {
            if (GameManager.isCitySceneBeingUnloaded)
            {
                ForgetSceneObjects();
                return;
            }

            if (_filter != null)
                UnityEngine.Object.Destroy(_filter.gameObject);
            if (_poi != null)
                UnityEngine.Object.Destroy(_poi.gameObject);
            if (_poiAnchor != null)
                UnityEngine.Object.Destroy(_poiAnchor);

            ForgetSceneObjects();
        }
    }
}
