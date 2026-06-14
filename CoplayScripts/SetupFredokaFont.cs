using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: imports Assets/Fonts/Fredoka.ttf, generates a dynamic TMP SDF font
/// asset (Assets/Fonts/Fredoka SDF.asset) with LiberationSans SDF as a fallback
/// (so the ▲ ▶ ◀ ● ■ control glyphs keep rendering), and makes it the global TMP
/// default so every label in the game switches to the bubbly font.
/// </summary>
public class SetupFredokaFont
{
    const string TtfPath      = "Assets/Fonts/Fredoka.ttf";
    const string SdfPath      = "Assets/Fonts/Fredoka SDF.asset";
    const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string TmpSettings  = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    public static string Execute()
    {
        AssetDatabase.ImportAsset(TtfPath, ImportAssetOptions.ForceUpdate);
        var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (font == null) return "FAIL: could not load Font at " + TtfPath;

        // Reuse an existing asset if present, else create a fresh dynamic SDF asset.
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(font);     // dynamic, 1024 atlas, on-demand glyphs
            if (fontAsset == null) return "FAIL: CreateFontAsset returned null";
            fontAsset.name = "Fredoka SDF";

            AssetDatabase.CreateAsset(fontAsset, SdfPath);

            // Persist the generated atlas texture + material as sub-assets.
            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    fontAsset.atlasTextures[i].name = "Fredoka Atlas " + i;
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[i], fontAsset);
                }
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "Fredoka SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
        }

        // Fallback chain -> LiberationSans for any glyph Fredoka lacks (arrows, etc.).
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
        if (fallback != null)
        {
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        }

        EditorUtility.SetDirty(fontAsset);

        // Make it the project-wide default.
        bool defaultSet = false;
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettings);
        if (settings != null)
        {
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop != null)
            {
                prop.objectReferenceValue = fontAsset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                defaultSet = true;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return "Fredoka SDF ready at " + SdfPath +
               " | fallback=" + (fallback != null) +
               " | tmpDefaultSet=" + defaultSet;
    }
}
