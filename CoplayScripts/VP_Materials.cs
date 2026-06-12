using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Visual polish pass 2: materials.
///  - Soft checker texture on every theme floor material (near-white pattern so
///    each theme's BaseColor tint still defines the look; the pattern also gives
///    the child clear motion feedback while driving).
///  - Emissive coins + star particles so bloom makes pickups glow.
///  - HDR neon path color for a true glow line.
///  - Avatar material warm accent.
/// </summary>
public class VP_Materials
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        // --- 1. Procedural soft checker texture ---
        string texPath = "Assets/Textures/FloorChecker.png";
        if (!AssetDatabase.IsValidFolder("Assets/Textures"))
            AssetDatabase.CreateFolder("Assets", "Textures");
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool a = ((x / 128) + (y / 128)) % 2 == 0;
                    float v = a ? 1f : 0.86f;
                    // Soft 6px blend at tile borders so the checker never shimmers.
                    int ex = Mathf.Min(x % 128, 127 - (x % 128));
                    int ey = Mathf.Min(y % 128, 127 - (y % 128));
                    float edge = Mathf.Clamp01(Mathf.Min(ex, ey) / 6f);
                    v = Mathf.Lerp(0.93f, v, edge);
                    tex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        var checker = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        sb.Append("checker=").Append(checker != null).Append("; ");

        // --- 2. Apply to all floor materials (one checker cell ~= 4 world units) ---
        string[] floorMats =
        {
            "Assets/Materials/HighContrast_Floor.mat",
            "Assets/Materials/Theme_MidnightPurple_Floor.mat",
            "Assets/Materials/Theme_DeepNavy_Floor.mat",
        };
        foreach (string path in floorMats)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { sb.Append(Path.GetFileName(path)).Append(" MISSING; "); continue; }
            m.SetTexture("_BaseMap", checker);
            // The generated floor plane maps UV 0..1 across the whole map; the map
            // is ~16 cells of 2px... actual world size set at runtime, so pick a
            // tiling that reads as ~4m squares on a 100m map and tune visually.
            m.SetTextureScale("_BaseMap", new Vector2(24f, 24f));
            m.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(m);
        }
        sb.Append("floors ok; ");

        // --- 3. Coin + star glow ---
        var coinMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Coin_Mat.mat");
        if (coinMat != null)
        {
            coinMat.EnableKeyword("_EMISSION");
            coinMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            coinMat.SetColor("_EmissionColor", new Color(1.2f, 0.9f, 0.15f) * 1.4f);
            EditorUtility.SetDirty(coinMat);
        }
        var starMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CoinStar_Mat.mat");
        if (starMat != null && starMat.HasProperty("_BaseColor"))
        {
            Color c = starMat.GetColor("_BaseColor");
            starMat.SetColor("_BaseColor", c * 1.8f);
            EditorUtility.SetDirty(starMat);
        }
        sb.Append("coin glow=").Append(coinMat != null).Append("; ");

        // --- 4. Neon path: HDR cyan so bloom lights it up ---
        var pathMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/NeonPath_Mat.mat");
        if (pathMat != null)
        {
            string prop = pathMat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            pathMat.SetColor(prop, new Color(0.15f, 2.2f, 2.4f, 1f));
            EditorUtility.SetDirty(pathMat);
        }
        sb.Append("neon=").Append(pathMat != null).Append("; ");

        // --- 5. Avatar accent (warm coral, slightly glossy) ---
        var avatarMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Avatar_Mat.mat");
        if (avatarMat != null && avatarMat.HasProperty("_BaseColor"))
        {
            avatarMat.SetColor("_BaseColor", new Color(1f, 0.45f, 0.35f, 1f));
            avatarMat.SetFloat("_Smoothness", 0.55f);
            EditorUtility.SetDirty(avatarMat);
        }
        sb.Append("avatar mat ok");

        AssetDatabase.SaveAssets();
        return sb.ToString();
    }
}
