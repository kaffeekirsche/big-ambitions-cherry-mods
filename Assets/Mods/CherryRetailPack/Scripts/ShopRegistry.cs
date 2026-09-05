#nullable enable
namespace CherryRetail
{
    /// <summary>Zentrale Liste aller Geschäfte. Zum Deaktivieren eines Shops einfach auskommentieren.</summary>
    public static class ShopRegistry
    {
        public static readonly ShopDefinition[] Shops =
        {
            BakeryShop.Definition,
            PetFoodShop.Definition,
            PharmacyShop.Definition,
        };
    }
}
