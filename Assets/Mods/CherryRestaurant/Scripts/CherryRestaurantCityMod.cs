#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Items;
using Helpers;

/// <summary>
/// Läuft bei jedem Laden der Stadt: schaltet das Testitem auf dem Vanilla-Pizzaofen frei und
/// prüft, ob der Geschäftstyp noch registriert ist.
/// </summary>
/// <remarks>
/// BusinessTypeHelper verwirft einen Mod-Typ beim erneuten Laden der BusinessTypes kommentarlos,
/// wenn inzwischen ein Vanilla-Typ denselben Namen belegt. Ohne die Logzeile wäre das im Spiel
/// nur daran zu merken, dass der Typ in BizMan fehlt.
/// </remarks>
[ModEntryOnCityLoad]
public class CherryRestaurantCityMod : IModBigAmbitions
{
    /// <summary>
    /// Das einzige Vanilla-Regal, das ba:itemname_pizza führt (ShowcaseShelf, bisher genau ein
    /// Produkt). Sein Prefab enthält damit einen Child-Transform namens "pizza" – genau den
    /// findet ShelfController.SetItemToShow auch für unser Item, weil beide dieselbe ID haben.
    /// </summary>
    private const string PizzaOvenItemName = "ba:itemname_pizzaoven";

    public string[] RelativeAssetBundlePaths => Array.Empty<string>();

    /// <summary>Originalzustand von itemsThatCanShowcase, für den Rollback.</summary>
    private Item? patchedShelf;
    private string[]? originalShowcaseItems;

    public Task OnLoadAsync(ModContext context)
    {
        var isRegistered = BusinessTypeHelper.BusinessTypeNames
            .Contains(CherryRestaurantMod.BusinessTypeName, StringComparer.Ordinal);

        if (!isRegistered)
        {
            context.Logger.Warn(
                $"CherryRestaurant: '{CherryRestaurantMod.BusinessTypeName}' ist beim Stadtladen " +
                "nicht registriert – der Geschäftstyp taucht in BizMan nicht auf.");
        }

        PatchPizzaOven(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Trägt das Testitem in das Sortiment des Pizzaofens ein.
    /// </summary>
    /// <remarks>
    /// Bewusst OHNE ShelfController.RegisterItemToShow: dessen Mod-Pfad SpawnModShowcaseItems
    /// verwirft die Template-Modelle per ClearChildren() und lädt stattdessen "pizza.prefab" aus
    /// den Mod-Bundles. Das liegt hier nicht vor – die Folge wäre ein LogError und ein break,
    /// das die Verarbeitung aller Mod-Items auf diesem Regal abbricht. Der Vanilla-Pfad
    /// SetItemToShow findet den vorhandenen Child-Transform "pizza" dagegen von allein.
    /// </remarks>
    private void PatchPizzaOven(ModContext context)
    {
        var shelf = ItemsGetter.GetByName(PizzaOvenItemName, true);
        if (shelf == null)
        {
            context.Logger.Warn($"CherryRestaurant: Regal '{PizzaOvenItemName}' nicht gefunden.");
            return;
        }

        var current = shelf.itemsThatCanShowcase ?? Array.Empty<string>();
        if (current.Contains(CherryRestaurantMod.ItemName, StringComparer.Ordinal))
            return;

        patchedShelf = shelf;
        originalShowcaseItems = current.ToArray();
        shelf.itemsThatCanShowcase = current.Append(CherryRestaurantMod.ItemName).ToArray();

        context.Logger.Info(
            $"CherryRestaurant: '{CherryRestaurantMod.ItemName}' auf '{PizzaOvenItemName}' " +
            $"freigeschaltet ({shelf.itemsThatCanShowcase.Length} Produkte).");
    }

    public Task OnUnloadAsync()
    {
        if (patchedShelf != null && originalShowcaseItems != null)
        {
            patchedShelf.itemsThatCanShowcase = originalShowcaseItems;
            patchedShelf = null;
            originalShowcaseItems = null;
        }

        return Task.CompletedTask;
    }
}
