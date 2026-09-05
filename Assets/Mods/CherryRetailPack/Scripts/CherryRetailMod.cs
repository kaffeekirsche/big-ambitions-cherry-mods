#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BAModAPI;
using BAModAPI.Services;
using BigAmbitions.Items;
using Buildings;
using CherryRetail;
using Helpers;

[assembly: RegisterModClass(typeof(CherryRetailMod))]
[assembly: RegisterModClass(typeof(CherryRetailCityMod))]

/// <summary>
/// Läuft einmal beim Spielstart: lädt das AssetBundle und registriert
/// alle Items und BusinessTypes aus ShopRegistry.
/// </summary>
[ModEntryOnInitializationLoad]
public class CherryRetailMod : IModBigAmbitions
{
    public const string ModId = "CherryRetailPack";
    public const string BundleKey = "AssetBundles/cherryretail.unity3d";

    public const string VanillaCheapGift       = "ba:itemname_cheapgift";
    public const string VanillaExpensiveGift   = "ba:itemname_expensivegift";
    public const string VanillaExpensiveFlower = "ba:itemname_expensiveflower";
    public const string VanillaRoundedShelf    = "ba:itemname_roundedshelf";

    public string[] RelativeAssetBundlePaths => new[] { BundleKey };

    private readonly List<BusinessType> registeredBusinessTypes = new();
    private readonly List<Item> registeredItems = new();

    public Task OnLoadAsync(ModContext context)
    {
        var bundle = AssetService.GetBundle(context.ModId, BundleKey);

        FixShaders(context, bundle);

        foreach (var shop in ShopRegistry.Shops)
        {
            foreach (var product in shop.Products)
            {
                var item = bundle.LoadAsset<Item>(product.ItemAssetPath);
                //item.customColorChannels = (CustomColorChannel)0;
                //item.customizationColors = Array.Empty<UnityEngine.Color>();
                if (item == null)
                {
                    context.Logger.Warn($"[{shop.Name}] Item-Asset nicht gefunden: {product.ItemAssetPath}");
                    continue;
                }
                if (item.itemName != product.ItemName)
                {    
                    context.Logger.Warn($"[{shop.Name}] itemName im Asset ({item.itemName}) weicht von Definition ({product.ItemName}) ab.");
                    continue;
                }
                ItemsGetter.RegisterModItem(item);
                registeredItems.Add(item);
            }

            var businessType = bundle.LoadAsset<BusinessType>(shop.BusinessTypeAssetPath);
            if (businessType == null)
            {
                context.Logger.Warn($"[{shop.Name}] BusinessType-Asset nicht gefunden: {shop.BusinessTypeAssetPath}");
                continue;
            }

            ModdingAPI.RegisterModBusinessType(businessType);
            registeredBusinessTypes.Add(businessType);
            context.Logger.Info($"[{shop.Name}] registriert mit {shop.Products.Length} Produkten.");
        }

        return Task.CompletedTask;
    }

    public Task OnUnloadAsync()
    {
        foreach (var businessType in registeredBusinessTypes)
            ModdingAPI.UnregisterModBusinessType(businessType);
        registeredBusinessTypes.Clear();

        foreach (var item in registeredItems)
            ItemsGetter.UnregisterModItem(item.itemName);
        registeredItems.Clear();

        return Task.CompletedTask;
    }

    private static void FixShaders(ModContext context, UnityEngine.AssetBundle bundle)
    {
        var fixedCount = 0;
        foreach (var shop in ShopRegistry.Shops)
        foreach (var product in shop.Products)
        {
            var key = product.ItemName.Substring(product.ItemName.LastIndexOf('_') + 1);
            var prefab = bundle.LoadAsset<UnityEngine.GameObject>($"Assets/Mods/CherryRetailPack/Prefabs/{key}.prefab");
            if (prefab == null)
                continue;

            foreach (var renderer in prefab.GetComponentsInChildren<UnityEngine.Renderer>(true))
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;
                var runtimeShader = UnityEngine.Shader.Find(mat.shader.name);
                if (runtimeShader != null && runtimeShader != mat.shader)
                {
                    mat.shader = runtimeShader;
                    fixedCount++;
                }
            }
        }
        context.Logger.Info($"[Shader] {fixedCount} Materialien auf Spiel-Shader umgestellt.");
    }
}

/// <summary>
/// Läuft bei jedem Laden der Stadt: macht die Produkte in Vanilla-Regalen
/// anzeigbar und beim Importer am Pier bestellbar.
/// </summary>
[ModEntryOnCityLoad]
public class CherryRetailCityMod : IModBigAmbitions
{
    private const bool DumpVanillaItems = false;

    public string[] RelativeAssetBundlePaths => Array.Empty<string>();

    private readonly Dictionary<Item, string[]> patchedShelves = new();
    private readonly List<string> registeredShowcaseItems = new();
    private ImportExportSettings? importSettings;

    private readonly Dictionary<ImportExportSettings, List<string>> importerAssignments = new();

    private static IEnumerable<ProductDefinition> AllProducts =>
        ShopRegistry.Shops.SelectMany(shop => shop.Products);

    public Task OnLoadAsync(ModContext context)
    {
        DumpItems(context);
        PatchShowcaseShelves(context);
        AddToImporter(context);
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync()
    {
        RestoreShowcaseShelves();
        RemoveFromImporter();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------- Regale

    private void PatchShowcaseShelves(ModContext context)
    {
        if (ItemsGetter.AllItems == null)
            return;

        var products = AllProducts.ToArray();
        var shelvesByName = ItemsGetter.AllItems
            .Where(i => i != null && !string.IsNullOrEmpty(i.itemName))
            .GroupBy(i => i.itemName)
            .ToDictionary(g => g.Key, g => g.First());

        // 1) Geschenk-Regale: alle Produkte mit ihrer Gift-Vorlage
        foreach (var shelf in shelvesByName.Values)
        {
            if (!IsGiftShowcaseShelf(shelf))
                continue;

            foreach (var product in products)
            {
                var template = shelf.itemName == CherryRetailMod.VanillaRoundedShelf
                    ? CherryRetailMod.VanillaExpensiveFlower
                    : product.GiftShelfTemplate;
                AddToShelf(shelf, product.ItemName, template);
            }
        }

        // 2) Explizite Ziel-Regale pro Produkt (Bäckereivitrine, Apothekenregal, ...)
        foreach (var product in products)
        {
            foreach (var target in product.ExtraShelves)
            {
                if (!shelvesByName.TryGetValue(target.ShelfItemName, out var shelf))
                {
                    context.Logger.Warn($"Regal nicht gefunden: {target.ShelfItemName} (für {product.ItemName})");
                    continue;
                }
                AddToShelf(shelf, product.ItemName, target.TemplateItemName);
            }
        }

        registeredShowcaseItems.AddRange(products.Select(p => p.ItemName));
    }

    private void AddToShelf(Item shelf, string itemName, string templateItemName)
    {
        var current = shelf.itemsThatCanShowcase ?? Array.Empty<string>();
        if (current.Contains(itemName))
            return;

        ShelfController.RegisterItemToShow(itemName, shelf.itemName, templateItemName);

        if (!patchedShelves.ContainsKey(shelf))
            patchedShelves[shelf] = current.ToArray();

        shelf.itemsThatCanShowcase = current.Append(itemName).ToArray();
    }

    private static bool IsGiftShowcaseShelf(Item item)
    {
        if (item == null || item.itemsThatCanShowcase == null)
            return false;

        if (item.itemName == CherryRetailMod.VanillaRoundedShelf)
            return true;

        return (item.type & ItemType.ShowcaseShelf) != 0
            && (item.itemsThatCanShowcase.Contains(CherryRetailMod.VanillaCheapGift)
                || item.itemsThatCanShowcase.Contains(CherryRetailMod.VanillaExpensiveGift));
    }

    private void RestoreShowcaseShelves()
    {
        foreach (var entry in patchedShelves)
            entry.Key.itemsThatCanShowcase = entry.Value;
        patchedShelves.Clear();

        foreach (var itemName in registeredShowcaseItems)
            ShelfController.UnregisterItemToShow(itemName);
        registeredShowcaseItems.Clear();
    }

    // -------------------------------------------------------------- Importer

    private void AddToImporter(ModContext context)
    {
        foreach (var shop in ShopRegistry.Shops)
        {
            ImportExportSettings? settings = null;
            try
            {
                var building = BuildingHelper.GetBuilding(new Address("ba:street_pier", shop.ImporterPierNumber));
                settings = building?.SpecialService?.settings as ImportExportSettings;
            }
            catch (Exception) { }

            if (settings == null)
            {
                context.Logger.Warn($"[{shop.Name}] Importeur Pier {shop.ImporterPierNumber} nicht gefunden");
                continue;
            }

            if (!importerAssignments.TryGetValue(settings, out var added))
                importerAssignments[settings] = added = new List<string>();

            foreach (var product in shop.Products)
                if (!settings.itemsAvailable.Contains(product.ItemName))
                {
                    settings.itemsAvailable.Add(product.ItemName);
                    added.Add(product.ItemName);
                }
        }
    }

    private void RemoveFromImporter()
    {
        foreach (var entry in importerAssignments)
            foreach (var itemName in entry.Value)
                entry.Key.itemsAvailable.Remove(itemName);
        importerAssignments.Clear();
    }

    // ----------------------------------------------------------------- Debug

    private static void DumpItems(ModContext context)
    {
        if (!DumpVanillaItems || ItemsGetter.AllItems == null)
            return;

        foreach (var item in ItemsGetter.AllItems.Where(i => i.isADemandedProduct).OrderBy(i => i.itemName))
            context.Logger.Info($"[Dump] {item.itemName} | box {item.boxSize} | wholesale {item.wholesalePrice} | market {item.DefaultMarketPrice}");

        foreach (var item in ItemsGetter.AllItems
                     .Where(i => i.itemsThatCanShowcase != null && i.itemsThatCanShowcase.Length > 0)
                     .OrderBy(i => i.itemName))
            context.Logger.Info($"[Shelf] {item.itemName} -> {string.Join(", ", item.itemsThatCanShowcase)}");

        for (var number = 1; number <= 20; number++)
        {
            ImportExportSettings? settings = null;
            try
            {
                var building = BuildingHelper.GetBuilding(new Address("ba:street_pier", number));
                settings = building?.SpecialService?.settings as ImportExportSettings;
            }
            catch (Exception) { }

            if (settings == null)
                continue;

            context.Logger.Info($"[Importer] Pier {number} -> {string.Join(", ", settings.itemsAvailable)}");
        }
    }
}
