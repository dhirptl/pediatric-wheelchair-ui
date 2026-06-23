using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Procedurally generates a Google-Maps-style navigation marker (blue disc +
/// white heading arrow) as Assets/UI_Sprites/WheelchairIcon.png and imports it
/// as a Sprite. Run once via Coplay execute_script; safe to delete afterward.
/// </summary>
public static class GenWheelchairIcon
{
    public static string Execute()
    {
        const int S = 256;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var clear = new Color(0, 0, 0, 0);
        var blue = new Color(0.13f, 0.45f, 0.95f, 1f);
        var white = new Color(1f, 1f, 1f, 1f);

        Vector2 c = new Vector2(128, 128);
        float rFill = 96f, rOuter = 112f;

        // Navigation arrow (paper-plane) pointing up (+y).
        Vector2 apex = new Vector2(128, 216);
        Vector2 bl = new Vector2(62, 64);
        Vector2 notch = new Vector2(128, 112);
        Vector2 br = new Vector2(194, 64);

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Vector2.Distance(p, c);
            Color col = clear;
            if (d <= rOuter) col = white;      // white rim
            if (d <= rFill) col = blue;         // blue disc
            if (d <= rFill &&
                (InTri(p, apex, bl, notch) || InTri(p, apex, notch, br)))
                col = white;                    // heading arrow
            tex.SetPixel(x, y, col);
        }
        tex.Apply();

        const string path = "Assets/UI_Sprites/WheelchairIcon.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();

        return "WheelchairIcon.png written + imported as Sprite at " + path;
    }

    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 cc)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, cc), d3 = Sign(p, cc, a);
        bool neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(neg && pos);
    }
}
