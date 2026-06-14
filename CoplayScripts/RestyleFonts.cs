using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Switches every TMP label in the active scene to Fredoka SDF, cleans stray
/// marker glyphs, and auto-sizes button labels (detected by the "Label" name the
/// builders give them). No UnityEngine.UI references.
/// </summary>
public class RestyleFonts
{
    const string FontPath = "Assets/Fonts/Fredoka SDF.asset";

    public static string Execute()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) return "FAIL: Fredoka SDF missing at " + FontPath;

        var scene = EditorSceneManager.GetActiveScene();
        int fontCount = 0, labelFix = 0;

        foreach (var t in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            t.font = font;

            string txt = t.text;
            if (!string.IsNullOrEmpty(txt) &&
                (txt.Contains("^") || txt.Contains("<") || txt.Contains(">") || txt.Contains("☰") || txt.Contains("★")))
            {
                txt = txt.Replace("☰", "").Replace("★", "PTS:").Replace("^", "").Replace("<", "").Replace(">", "");
                while (txt.Contains("  ")) txt = txt.Replace("  ", " ");
                t.text = txt.Trim();
                labelFix++;
            }

            if (t.gameObject.name == "Label")
            {
                float max = t.fontSize > 0f ? t.fontSize : 48f;
                t.enableAutoSizing = true;
                t.fontSizeMin = 20f;
                t.fontSizeMax = max;
                t.margin = new Vector4(12f, 6f, 12f, 6f);
            }

            EditorUtility.SetDirty(t);
            fontCount++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return "Fonts set=" + fontCount + " labelFixes=" + labelFix + " saved=" + saved;
    }
}
