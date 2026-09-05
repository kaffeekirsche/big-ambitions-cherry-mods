#nullable enable
using System;

namespace CherryRetail
{
    /// <summary>
    /// Beschreibt ein Geschäft: BusinessType-Asset und zugehörige Produkte.
    /// Neues Geschäft = neue Klasse in Scripts/Shops + Eintrag in ShopRegistry.
    /// </summary>
    public sealed class ShopDefinition
    {
        public string Name { get; }
        public string BusinessTypeAssetPath { get; }
        public int ImporterPierNumber { get; }
        public ProductDefinition[] Products { get; }

        public ShopDefinition(string name, string businessTypeAssetPath, int importerPierNumber, ProductDefinition[] products)
        {
            Name = name;
            BusinessTypeAssetPath = businessTypeAssetPath;
            ImporterPierNumber = importerPierNumber;
            Products = products;
        }
    }

    /// <summary>Ein Vanilla-Regal, auf dem ein Produkt zusätzlich angezeigt werden soll.</summary>
    public sealed class ShelfTarget
    {
        public string ShelfItemName { get; }
        /// <summary>Vanilla-Produkt, dessen Darstellung auf diesem Regal übernommen wird.</summary>
        public string TemplateItemName { get; }

        public ShelfTarget(string shelfItemName, string templateItemName)
        {
            ShelfItemName = shelfItemName;
            TemplateItemName = templateItemName;
        }
    }

    public sealed class ProductDefinition
    {
        /// <summary>Muss exakt dem itemName im Item-Asset entsprechen (cherryretail:itemname_xxx).</summary>
        public string ItemName { get; }
        public string ItemAssetPath { get; }

        /// <summary>Vorlage für die Geschenk-Regale (Fallback für alle Produkte).</summary>
        public string GiftShelfTemplate { get; }

        /// <summary>Zusätzliche Vanilla-Regale mit passender Vorlage (z. B. Bäckerei-Vitrine).</summary>
        public ShelfTarget[] ExtraShelves { get; }

        public ProductDefinition(string itemName, string itemAssetPath, string giftShelfTemplate,
            params ShelfTarget[] extraShelves)
        {
            ItemName = itemName;
            ItemAssetPath = itemAssetPath;
            GiftShelfTemplate = giftShelfTemplate;
            ExtraShelves = extraShelves ?? Array.Empty<ShelfTarget>();
        }
    }
}
