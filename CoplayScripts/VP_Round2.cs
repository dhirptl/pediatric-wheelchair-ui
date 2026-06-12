using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual polish round 2 (after first capture review):
///  - Stronger floor checker so the pattern reads on dark theme floors.
///  - Slightly lift the near-black floor base colors so shading/pattern show.
///  - TopBar: muddy olive -> deep navy (matches the rounded panels, yellow text pops).
///  - BotIdleBob on the hover-bot visual rig.
/// </summary>
public class VP_Round2
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        // --- 1. Regenerate checker with more contrast ---
        string texPath = "Assets/Textures/FloorChecker.png";
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool a = ((x / 128) + (y / 128)) % 2 == 0;
                float v = a ? 1f : 0.68f;
                int ex = Mathf.Min(x % 128, 127 - (x % 128));
                int ey = Mathf.Min(y % 128, 127 - (y % 128));
                float edge = Mathf.Clamp01(Mathf.Min(ex, ey) / 5f);
                v = Mathf.Lerp(0.84f, v, edge);
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        }
        tex.Apply();
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        sb.Append("checker v2; ");

        // --- 2. Lift floor base colors out of the murk ---
        Lift("Assets/Materials/Theme_DeepNavy_Floor.mat", new Color(0.16f, 0.19f, 0.38f), sb);
        Lift("Assets/Materials/Theme_MidnightPurple_Floor.mat", new Color(0.24f, 0.16f, 0.4f), sb);
        Lift("Assets/Materials/HighContrast_Floor.mat", new Color(0.13f, 0.13f, 0.15f), sb);

        // --- 3. TopBar -> deep navy ---
        var topBar = GameObject.Find("GameHUDCanvas/TopBar");
        if (topBar != null)
        {
            var img = topBar.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0.07f, 0.1f, 0.2f, 0.92f);
                EditorUtility.SetDirty(topBar);
                sb.Append("topbar navy; ");
            }
        }
        else sb.Append("TOPBAR NOT FOUND; ");

        // --- 4. Hover bob on the bot rig ---
        var avatar = GameObject.Find("Wheelchair_Avatar");
        var bot = avatar != null ? avatar.transform.Find("BotVisual") : null;
        if (bot != null)
        {
            if (bot.GetComponent<BotIdleBob>() == null) bot.gameObject.AddComponent<BotIdleBob>();
            EditorUtility.SetDirty(bot.gameObject);
            sb.Append("bob ok");
        }
        else sb.Append("BOT NOT FOUND");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    private static void Lift(string path, Color c, StringBuilder sb)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null || !m.HasProperty("_BaseColor")) { sb.Append(Path.GetFileName(path)).Append(" skip; "); return; }
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        sb.Append(Path.GetFileName(path)).Append(" lifted; ");
    }
}
