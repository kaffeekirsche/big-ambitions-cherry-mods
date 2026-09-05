#nullable enable
namespace CherryRetail
{
    public static class BakeryShop
    {
        public const string BusinessTypeName = "cherryretail:businesstype_bakery";
        private const string Dir = "Assets/Mods/CherryRetailPack/Bakery/";

        // Vanilla-Bäckereivitrine (zeigt Croissant, Cupcake, Donut)
        private static readonly ShelfTarget Showcase =
            new("ba:itemname_bakeryshowcase", "ba:itemname_croissant");
        private static readonly ShelfTarget ShowcaseLarge =
            new("ba:itemname_bakeryshowcase", "ba:itemname_cupcake");

        public static readonly ShopDefinition Definition = new(
            "Bakery",
            Dir + "BakeryShop.asset", 1,
            new[]
            {
                P("bread",     "Bread",     CherryRetailMod.VanillaExpensiveGift, Showcase),
                P("breadroll", "BreadRoll", CherryRetailMod.VanillaCheapGift,     Showcase),
                P("bagel",     "Bagel",     CherryRetailMod.VanillaCheapGift,     Showcase),
                P("pancake",   "Pancake",   CherryRetailMod.VanillaCheapGift,     Showcase),
                P("cake",      "Cake",      CherryRetailMod.VanillaExpensiveGift, Showcase),
                P("cookies",   "Cookies",   CherryRetailMod.VanillaCheapGift,     Showcase),
                P("baguette",  "Baguette",  CherryRetailMod.VanillaCheapGift,     Showcase),
            });

        private static ProductDefinition P(string key, string file, string giftTemplate, params ShelfTarget[] extra) =>
            new($"cherryretail:itemname_{key}", Dir + file + ".asset", giftTemplate, extra);
    }
}
