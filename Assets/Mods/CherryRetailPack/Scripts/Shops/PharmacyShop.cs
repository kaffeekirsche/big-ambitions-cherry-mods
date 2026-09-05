#nullable enable
namespace CherryRetail
{
    public static class PharmacyShop
    {
        public const string BusinessTypeName = "cherryretail:businesstype_pharmacy";
        private const string Dir = "Assets/Mods/CherryRetailPack/Pharmacy/";

        // Friseur-Regal (Haarpflegeprodukte) als Apothekenregal
        private static readonly ShelfTarget Shelf =
            new("ba:itemname_hairdressershelf", "ba:itemname_haircareproduct");

        public static readonly ShopDefinition Definition = new(
            "Pharmacy",
            Dir + "PharmacyShop.asset", 2,
            new[]
            {
                P("coldmedicine", "ColdMedicine", CherryRetailMod.VanillaCheapGift,     Shelf),
                P("painkiller",   "Painkiller",   CherryRetailMod.VanillaCheapGift,     Shelf),
                P("vitamins",     "Vitamins",     CherryRetailMod.VanillaCheapGift,     Shelf),
                P("firstaidkit",  "FirstAidKit",  CherryRetailMod.VanillaExpensiveGift, Shelf),
                P("thermometer",  "Thermometer",  CherryRetailMod.VanillaCheapGift,     Shelf),
                P("sunscreen",    "Sunscreen",    CherryRetailMod.VanillaCheapGift,     Shelf),
            });

        private static ProductDefinition P(string key, string file, string giftTemplate, params ShelfTarget[] extra) =>
            new($"cherryretail:itemname_{key}", Dir + file + ".asset", giftTemplate, extra);
    }
}
