using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual polish pass 4: UI. Generates a 9-slice rounded-rectangle sprite and
/// applies it to every HUD panel and button (replacing the boxy default
/// UISprite), then adds soft drop shadows. Colors, outlines, ButtonHighlighter
/// pulses and ButtonJuice flashes are untouched - this only softens the shapes.
/// Skips Filled images (the wind-up bar) and RawImages (minimap feed).
/// </summary>
public class VP_UI
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        // --- 1. Rounded-rect sprite (white, 9-sliced) ---
        string path = "Assets/Textures/RoundedRect.png";
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
        {
            const int s = 64;
            const float r = 20f;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(r - x, x - (s - 1 - r)));
                    float dy = Mathf.Max(0, Mathf.Max(r - y, y - (s - 1 - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);          // 1px AA edge
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = new Vector4(24, 24, 24, 24);
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        var rounded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        sb.Append("sprite=").Append(rounded != null).Append("; ");
        if (rounded == null) return sb.ToString();

        // --- 2. Panels: rounded + drop shadow ---
        string[] panels =
        {
            "GameHUDCanvas/ExplorerControls/ActiveCommandPanel",
            "GameHUDCanvas/MiniMapPanel",
            "GameHUDCanvas/DestinationPanel",
        };
        int panelCount = 0;
        foreach (string p in panels)
            panelCount += Style(FindAnywhere(p), rounded, new Vector2(0f, -6f), 0.5f) ? 1 : 0;

        // Boxes inside (possibly inactive) overlays - find by name anywhere.
        foreach (string name in new[] { "SettingsBox", "ShopBox" })
            panelCount += Style(FindAnywhere(name), rounded, new Vector2(0f, -6f), 0.5f) ? 1 : 0;
        sb.Append("panels=").Append(panelCount).Append("; ");

        // --- 3. Buttons: rounded + subtle shadow ---
        int buttonCount = 0;
        foreach (var btn in Object.FindObjectsOfType<Button>(true))
        {
            var img = btn.GetComponent<Image>();
            if (img == null || img.type == Image.Type.Filled) continue;
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            AddShadow(btn.gameObject, new Vector2(0f, -3f), 0.4f);
            EditorUtility.SetDirty(btn.gameObject);
            buttonCount++;
        }
        sb.Append("buttons=").Append(buttonCount);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    private static GameObject FindAnywhere(string pathOrName)
    {
        var direct = GameObject.Find(pathOrName);
        if (direct != null) return direct;
        // GameObject.Find misses inactive objects; walk all transforms.
        string leaf = pathOrName.Contains("/")
            ? pathOrName.Substring(pathOrName.LastIndexOf('/') + 1) : pathOrName;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name != leaf || !t.gameObject.scene.IsValid()) continue;
            return t.gameObject;
        }
        return null;
    }

    private static bool Style(GameObject go, Sprite sprite, Vector2 shadowDist, float shadowAlpha)
    {
        if (go == null) return false;
        var img = go.GetComponent<Image>();
        if (img == null || img.type == Image.Type.Filled) return false;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        AddShadow(go, shadowDist, shadowAlpha);
        EditorUtility.SetDirty(go);
        return true;
    }

    private static void AddShadow(GameObject go, Vector2 dist, float alpha)
    {
        var shadow = go.GetComponent<Shadow>();
        if (shadow == null) shadow = go.AddComponent<Shadow>();
        shadow.effectDistance = dist;
        shadow.effectColor = new Color(0f, 0f, 0f, alpha);
    }
}
