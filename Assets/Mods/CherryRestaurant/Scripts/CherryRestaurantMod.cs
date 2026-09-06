#nullable enable
using System.Threading.Tasks;
using BAModAPI;
using BAModAPI.Services;
using BigAmbitions.Items;
using UnityEngine;

[assembly: RegisterModClass(typeof(CherryRestaurantMod))]
[assembly: RegisterModClass(typeof(CherryRestaurantCityMod))]

/// <summary>
/// Läuft einmal beim Spielstart: lädt das AssetBundle und registriert den
/// Geschäftstyp "Restaurant" (Full Service, Abendprofil).
/// </summary>
[ModEntryOnInitializationLoad]
public class CherryRestaurantMod : IModBigAmbitions
{
    /// <summary>
    /// Pfad relativ zum Mod-Ordner, ohne Plattform-Segment – Windows oder Mac wählt
    /// die Laufzeit selbst. Der Name hinter dem Schrägstrich muss dem AssetBundleName
    /// im Manifest entsprechen, sonst findet der AssetService die Datei nicht.
    /// </summary>
    public const string BundleKey = "AssetBundles/cherryrestaurant.unity3d";

    /// <summary>Voller Projektpfad – so legt der Mod Builder die Assets im Bundle ab.</summary>
    public const string BusinessTypeAssetPath = "Assets/Mods/CherryRestaurant/CherryRestaurant.asset";

    /// <summary>Muss exakt dem Feld businessTypeName im Asset entsprechen.</summary>
    public const string BusinessTypeName = "cherryrestaurant:businesstype_cherryrestaurant";

    /// <summary>Testitem der Phase 3, siehe Docs~/IDEEN.md.</summary>
    public const string ItemAssetPath = "Assets/Mods/CherryRestaurant/Items/SignaturePizza.asset";

    /// <summary>
    /// Bewusste Suffix-Kollision mit ba:itemname_pizza. ItemsGetter schlüsselt auf den vollen
    /// Namen, beide Items existieren also getrennt nebeneinander – aber jede Darstellung wird
    /// über GetIdWithoutType() aufgelöst, also über "pizza". Dadurch erbt das Item Icon,
    /// Regalmodell und Tellermodell der Vanilla-Pizza, ohne ein eigenes 3D-Asset zu brauchen.
    /// </summary>
    public const string ItemName = "cherryrestaurant:itemname_pizza";

    public string[] RelativeAssetBundlePaths => new[] { BundleKey };

    /// <summary>
    /// Nur gesetzt, wenn die Registrierung tatsächlich angenommen wurde. Ohne diese
    /// Unterscheidung würde OnUnloadAsync einen Geschäftstyp abmelden, den die Mod nie
    /// registriert hat: RegisterModBusinessType lehnt eine Namenskollision ab und meldet
    /// das ausschließlich über den Rückgabewert.
    /// </summary>
    private BusinessType? registeredBusinessType;

    /// <summary>
    /// Nur gesetzt, wenn das Asset geladen wurde. RegisterModItem ist void und verwirft eine
    /// Doppelregistrierung lediglich mit einer Warnung, deshalb reicht hier das geladene Asset
    /// als Bedingung fürs spätere Abmelden.
    /// </summary>
    private Item? registeredItem;

    public Task OnLoadAsync(ModContext context)
    {
        AssetBundle bundle = AssetService.GetBundle(context.ModId, BundleKey);
        if (bundle == null)
        {
            context.Logger.Warn(
                $"CherryRestaurant: AssetBundle '{BundleKey}' nicht geladen – " +
                "der Geschäftstyp steht im Spiel nicht zur Verfügung.");
            return Task.CompletedTask;
        }

        RegisterItem(context, bundle);

        var businessType = bundle.LoadAsset<BusinessType>(BusinessTypeAssetPath);
        if (businessType == null)
        {
            context.Logger.Warn(
                $"CherryRestaurant: BusinessType-Asset '{BusinessTypeAssetPath}' nicht im Bundle gefunden.");
            return Task.CompletedTask;
        }

        if (businessType.businessTypeName != BusinessTypeName)
        {
            // Aus businessTypeName leitet das Spiel auch den Hilfeseiten-Slug ab.
            context.Logger.Warn(
                $"CherryRestaurant: businessTypeName im Asset ist '{businessType.businessTypeName}', " +
                $"erwartet wurde '{BusinessTypeName}'. Locale-Keys und Hilfeseite greifen dann nicht.");
        }

        if (!ModdingAPI.RegisterModBusinessType(businessType))
        {
            context.Logger.Warn(
                $"CherryRestaurant: '{businessType.businessTypeName}' wurde nicht registriert – " +
                "vermutlich Namenskollision mit einem anderen Geschäftstyp.");
            return Task.CompletedTask;
        }

        registeredBusinessType = businessType;
        context.Logger.Info($"CherryRestaurant: Geschäftstyp '{businessType.businessTypeName}' registriert.");
        return Task.CompletedTask;
    }

    private void RegisterItem(ModContext context, AssetBundle bundle)
    {
        var item = bundle.LoadAsset<Item>(ItemAssetPath);
        if (item == null)
        {
            context.Logger.Warn(
                $"CherryRestaurant: Item-Asset '{ItemAssetPath}' nicht im Bundle gefunden.");
            return;
        }

        if (item.itemName != ItemName)
        {
            // Aus itemName leitet das Spiel Icon-, Prefab- und Regalschlüssel ab.
            context.Logger.Warn(
                $"CherryRestaurant: itemName im Asset ist '{item.itemName}', erwartet wurde " +
                $"'{ItemName}'. Locale-Key und Vanilla-Optik greifen dann nicht.");
            return;
        }

        ItemsGetter.RegisterModItem(item);
        registeredItem = item;
        context.Logger.Info($"CherryRestaurant: Item '{item.itemName}' registriert.");
    }

    public Task OnUnloadAsync()
    {
        if (registeredBusinessType != null)
        {
            ModdingAPI.UnregisterModBusinessType(registeredBusinessType);
            registeredBusinessType = null;
        }

        if (registeredItem != null)
        {
            ItemsGetter.UnregisterModItem(registeredItem.itemName);
            registeredItem = null;
        }

        return Task.CompletedTask;
    }
}
