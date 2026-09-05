#nullable enable
namespace CherryRetail
{
    public static class PetFoodShop
    {
        public const string BusinessTypeName = "cherryretail:businesstype_petfood";
        private const string Dir = "Assets/Mods/CherryRetailPack/PetFood/";

        // Holzkiste (Obst) für Schüttgut wie Vogelfutter / Leckerlis
        private static readonly ShelfTarget Crate =
            new("ba:itemname_woodenproductcrate", "ba:itemname_apple");

        public static readonly ShopDefinition Definition = new(
            "PetFood",
            Dir + "PetFoodShop.asset", 4,
            new[]
            {
                P("dogfood",        "DogFood",        CherryRetailMod.VanillaExpensiveGift),
                P("dogfoodpremium", "DogFoodPremium", CherryRetailMod.VanillaExpensiveGift),
                P("catfood",        "CatFood",        CherryRetailMod.VanillaExpensiveGift),
                P("catfoodpremium", "CatFoodPremium", CherryRetailMod.VanillaExpensiveGift),
                P("pettreats",      "PetTreats",      CherryRetailMod.VanillaCheapGift, Crate),
                P("catlitter",      "CatLitter",      CherryRetailMod.VanillaExpensiveGift),
                P("birdseed",       "BirdSeed",       CherryRetailMod.VanillaCheapGift, Crate),
                P("pettoy",         "PetToy",         CherryRetailMod.VanillaCheapGift),
                P("pettoypremium",  "PetToyPremium",  CherryRetailMod.VanillaExpensiveGift),
                P("leashcollar",    "LeashCollar",    CherryRetailMod.VanillaCheapGift),
            });

        private static ProductDefinition P(string key, string file, string giftTemplate, params ShelfTarget[] extra) =>
            new($"cherryretail:itemname_{key}", Dir + file + ".asset", giftTemplate, extra);
    }
}
