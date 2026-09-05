#if BA_GAME_DLLS_IMPORTED
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menü: Big Ambitions → Cherry Retail → …
///  1. Modell-Skalierung setzen   (Scale Factor aller FBX in Models/)
///  2. Prefabs aus Models erzeugen (ItemController + itemName)
///  3. Kenney-Material zuweisen   (ein HDRP/Lit-Material mit colormap.png für alle Prefabs)
/// </summary>
public static class CherryRetailPrefabGenerator
{
    private const string ModelsFolder  = "Assets/Mods/CherryRetailPack/Models";
    private const string PrefabsFolder = "Assets/Mods/CherryRetailPack/Prefabs";
    private const string ColormapPath  = ModelsFolder + "/colormap.png";
    private const string MaterialPath  = ModelsFolder + "/M_Kenney.mat";
    private const string KeyPrefix     = "cherryretail:itemname_";

    /// <summary>Scale Factor für alle Modelle, die nicht in <see cref="ProductScale"/> stehen. Vanilla-Croissant ≈ 0,15 m.</summary>
    private const float DefaultScaleFactor = 0.3f;

    private const string OnlySubfolder = "";

    /// <summary>
    /// Scale Factor pro Produkt. Key = FBX-Dateiname ohne Endung, klein geschrieben (= Produkt-Key).
    /// Nicht gelistete Modelle bekommen <see cref="DefaultScaleFactor"/>.
    /// </summary>
    private static readonly Dictionary<string, float> ProductScale = new()
    {
        // --- Bäckerei (Pier 1) — bread, breadroll, pancake, sandwich = Default 0,3
        { "cake",    0.25f }, // etwas kleiner
        { "cookies", 0.33f }, // minimal größer
        { "bagel",   0.33f }, // minimal größer

        // --- Tierfutter (Pier 4) — noch nicht im Spiel geprüft
        // { "dogfood",        0.30f },
        // { "dogfoodpremium", 0.30f },
        // { "catfood",        0.30f },
        // { "catfoodpremium", 0.30f },
        // { "pettreats",      0.30f },
        // { "catlitter",      0.30f },
        // { "birdseed",       0.30f },
        // { "pettoy",         0.30f },
        // { "pettoypremium",  0.30f },
        // { "leashcollar",    0.30f },

        // --- Apotheke (Pier 2) — noch nicht im Spiel geprüft
        // { "coldmedicine", 0.30f },
        // { "painkiller",   0.30f },
        // { "vitamins",     0.30f },
        // { "firstaidkit",  0.30f },
        // { "thermometer",  0.30f },
        // { "sunscreen",    0.30f },
    };

    /// <summary>Skalierung für GLB-Modelle (bereits in Metern), 1 = unverändert.</summary>
    /// <summary>GLB-Modelle: (Skalierung, Drehung um Y in Grad). Fehlt ein Eintrag: 1, 0.</summary>
    private static readonly Dictionary<string, (float scale, float rotY)> GlbTransform = new()
    {
        { "bread",    (1.0f, 90f) },
        { "baguette", (0.7f, 90f) },
        { "cake",     (0.8f, 0f) },
    };

    private const string TemplatePath = "Assets/Mods/Example-BusinessType/Models/Gray.mat";

    // ------------------------------------------------------------ 1. Skalierung
    [MenuItem("Big Ambitions/Cherry Retail/1 - Modell-Skalierung setzen")]
    public static void SetModelScale()
    {
        var count = 0;
        var skipped = 0;
        foreach (var path in ModelPaths())
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                continue;

            var key = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            var scale = ProductScale.TryGetValue(key, out var custom) ? custom : DefaultScaleFactor;

            if (importer.useFileScale && Mathf.Approximately(importer.globalScale, scale))
            {
                skipped++;
                continue; // schon korrekt – kein unnötiger Reimport
            }

            importer.useFileScale = true;
            importer.globalScale = scale;
            importer.SaveAndReimport();
            Debug.Log($"[CherryRetail] {key}: Scale Factor {scale}");
            count++;
        }
        Debug.Log($"[CherryRetail] Skalierung: {count} Modelle gesetzt, {skipped} unverändert.");
    }

    // ------------------------------------------------------------ 2. Prefabs
    [MenuItem("Big Ambitions/Cherry Retail/2 - Prefabs aus Models erzeugen")]
    public static void GeneratePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(PrefabsFolder)!.Replace('\\', '/'), Path.GetFileName(PrefabsFolder));

        var created = 0;
        foreach (var modelPath in ModelPaths())
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                continue;

            var key = Path.GetFileNameWithoutExtension(modelPath).ToLowerInvariant();
            var relativeDir = Path.GetDirectoryName(modelPath)!.Replace('\\', '/').Substring(ModelsFolder.Length).TrimStart('/');
            var targetFolder = string.IsNullOrEmpty(relativeDir) ? PrefabsFolder : $"{PrefabsFolder}/{relativeDir}";
            if (!AssetDatabase.IsValidFolder(targetFolder))
                AssetDatabase.CreateFolder(PrefabsFolder, relativeDir);
            var prefabPath = $"{targetFolder}/{key}.prefab";

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = key;

            if (modelPath.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase))
            {
                var t = GlbTransform.TryGetValue(key, out var custom) ? custom : (1f, 0f);
                instance.transform.localScale = Vector3.one * t.Item1;
                instance.transform.rotation = Quaternion.Euler(0f, t.Item2, 0f);
            }

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask = 257;

            var controller = instance.GetComponent<ItemController>() ?? instance.AddComponent<ItemController>();
            controller.itemName = KeyPrefix + key;

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CherryRetail] {created} Prefabs erzeugt/aktualisiert.");
    }

    // ------------------------------------------------------------ 3. Material
    [MenuItem("Big Ambitions/Cherry Retail/3 - Kenney-Material zuweisen")]
    public static void AssignMaterial()
    {
        var colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(ColormapPath);
        if (colormap == null)
        {
            Debug.LogError($"[CherryRetail] colormap.png nicht gefunden: {ColormapPath}");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            AssetDatabase.CopyAsset(TemplatePath, MaterialPath);
            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }
        material.SetTexture("_BaseColorMap", colormap);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(material);

        var renderersPatched = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            if (source != null && AssetDatabase.GetAssetPath(source).EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase))
                continue;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                renderersPatched++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CherryRetail] Material {MaterialPath} auf {renderersPatched} Renderer gesetzt.");
    }

    // ------------------------------------------------------------ Helfer
    private static string[] ModelPaths() =>
        AssetDatabase.FindAssets("", new[] { ModelsFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)
                    || p.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrEmpty(OnlySubfolder)
                    || p.StartsWith($"{ModelsFolder}/{OnlySubfolder}/"))
            .ToArray();
}
#endif