#if BA_GAME_DLLS_IMPORTED
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Big Ambitions → Cherry Retail → 4 - glTF-Materialien nach HDRP/Lit konvertieren
/// Für jedes Prefab unter Prefabs/: Materialien mit glTF-Shader werden durch eine
/// Kopie von Gray.mat (validiertes HDRP/Lit) ersetzt, Textur und Farbe übernommen.
/// Ergebnis liegt in Models/Materials/<prefab>_<n>.mat. Läuft beliebig oft.
/// </summary>
public static class CherryRetailGltfMaterialConverter
{
    private const string PrefabsFolder   = "Assets/Mods/CherryRetailPack/Prefabs";
    private const string MaterialsFolder = "Assets/Mods/CherryRetailPack/Models/Materials";
    private const string TemplatePath    = "Assets/Mods/Example-BusinessType/Models/Gray.mat";

    private static readonly string[] TextureProps = { "baseColorTexture", "_BaseColorMap", "_MainTex", "_BaseMap" };
    private static readonly string[] ColorProps   = { "baseColorFactor", "_BaseColor", "_Color" };

    [MenuItem("Big Ambitions/Cherry Retail/4 - glTF-Materialien nach HDRP-Lit konvertieren")]
    public static void Convert()
    {
        var template = AssetDatabase.LoadAssetAtPath<Material>(TemplatePath);
        if (template == null) { Debug.LogError($"[CherryRetail] Vorlage fehlt: {TemplatePath}"); return; }
        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(MaterialsFolder)!.Replace('\\', '/'), Path.GetFileName(MaterialsFolder));

        var converted = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var changed = false;
            var index = 0;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null || src.shader == null || !src.shader.name.Contains("glTF"))
                        continue;

                    var texture = TextureProps.Select(p => src.HasProperty(p) ? src.GetTexture(p) : null).FirstOrDefault(t => t != null)
                                  ?? src.mainTexture;
                    var color = ColorProps.Where(src.HasProperty).Select(src.GetColor).DefaultIfEmpty(Color.white).First();

                    var matPath = $"{MaterialsFolder}/{prefabName}_{index++}.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null)
                    {
                        AssetDatabase.CopyAsset(TemplatePath, matPath);
                        mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    }
                    mat.SetTexture("_BaseColorMap", texture);
                    mat.SetColor("_BaseColor", color);
                    mat.SetFloat("_Smoothness", 0.3f);
                    EditorUtility.SetDirty(mat);

                    mats[i] = mat;
                    changed = true;
                    converted++;
                }
                renderer.sharedMaterials = mats;
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CherryRetail] {converted} glTF-Materialien nach HDRP/Lit konvertiert.");
    }
}
#endif
