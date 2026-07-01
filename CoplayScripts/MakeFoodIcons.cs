using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor helper that draws three simple, clearly-distinct food icons (ice cream,
/// cake slice, chocolate bar) as 256x256 transparent PNGs into Assets/Resources/Food/
/// and imports them as Sprites. Placeholders so the food shop works end-to-end; swap
/// the PNGs later for nicer art. UnityEngine + UnityEditor only; returns a summary.
/// </summary>
public static class MakeFoodIcons
{
    const int N = 256;
    const string Dir = "Assets/Resources/Food";

    public static string Execute()
    {
        Directory.CreateDirectory(Dir);
        WriteIceCream();
        WriteCake();
        WriteChocolate();
        AssetDatabase.Refresh();
        Import("ice_cream"); Import("cake"); Import("chocolate");
        return "wrote ice_cream.png, cake.png, chocolate.png to " + Dir;
    }

    // ---- icons ------------------------------------------------------------------

    static void WriteIceCream()
    {
        var t = Blank();
        // Cone (apex down): tan triangle.
        FillTri(t, 128, 40, 88, 150, 168, 150, new Color(0.85f, 0.68f, 0.42f));
        // Scoop: vanilla circle.
        FillCircle(t, 128, 168, 58, new Color(1f, 0.95f, 0.80f));
        // Cherry on top.
        FillCircle(t, 128, 220, 16, new Color(0.90f, 0.12f, 0.16f));
        Save(t, "ice_cream");
    }

    static void WriteCake()
    {
        var t = Blank();
        // Slice pointing up: sponge triangle.
        FillTri(t, 128, 200, 66, 60, 190, 60, new Color(0.98f, 0.85f, 0.58f));
        // Cream layer band across the middle.
        FillRect(t, 78, 108, 178, 128, new Color(1f, 0.98f, 0.92f));
        // Strawberry near the tip.
        FillCircle(t, 128, 178, 15, new Color(0.90f, 0.14f, 0.18f));
        Save(t, "cake");
    }

    static void WriteChocolate()
    {
        var t = Blank();
        var brown = new Color(0.36f, 0.20f, 0.09f);
        var dark = new Color(0.22f, 0.12f, 0.05f);
        FillRect(t, 64, 60, 192, 196, brown);
        // Segment grid: two vertical + two horizontal grooves.
        FillRect(t, 106, 60, 112, 196, dark);
        FillRect(t, 144, 60, 150, 196, dark);
        FillRect(t, 64, 105, 192, 111, dark);
        FillRect(t, 64, 145, 192, 151, dark);
        // Foil corner hint (top-left) in silver.
        FillTri(t, 64, 196, 64, 150, 110, 196, new Color(0.82f, 0.82f, 0.86f));
        Save(t, "chocolate");
    }

    // ---- drawing primitives -----------------------------------------------------

    static Texture2D Blank()
    {
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var clear = new Color(0, 0, 0, 0);
        var px = new Color[N * N];
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        t.SetPixels(px);
        return t;
    }

    static void Set(Texture2D t, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= N || y >= N) return;
        t.SetPixel(x, y, c);
    }

    static void FillRect(Texture2D t, int x0, int y0, int x1, int y1, Color c)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Set(t, x, y, c);
    }

    static void FillCircle(Texture2D t, int cx, int cy, int r, Color c)
    {
        int r2 = r * r;
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r2) Set(t, x, y, c);
    }

    static void FillTri(Texture2D t, int ax, int ay, int bx, int by, int cx, int cy, Color col)
    {
        int minX = Mathf.Min(ax, Mathf.Min(bx, cx));
        int maxX = Mathf.Max(ax, Mathf.Max(bx, cx));
        int minY = Mathf.Min(ay, Mathf.Min(by, cy));
        int maxY = Mathf.Max(ay, Mathf.Max(by, cy));
        float d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
        if (Mathf.Abs(d) < 0.0001f) return;
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float wa = ((by - cy) * (x - cx) + (cx - bx) * (y - cy)) / d;
                float wb = ((cy - ay) * (x - cx) + (ax - cx) * (y - cy)) / d;
                float wc = 1f - wa - wb;
                if (wa >= 0f && wb >= 0f && wc >= 0f) Set(t, x, y, col);
            }
    }

    static void Save(Texture2D t, string name)
    {
        t.Apply();
        File.WriteAllBytes(Dir + "/" + name + ".png", t.EncodeToPNG());
        Object.DestroyImmediate(t);
    }

    static void Import(string name)
    {
        string path = Dir + "/" + name + ".png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
    }
}
