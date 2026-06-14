using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: bakes a white, anti-aliased rounded-rectangle PNG at
/// Assets/UI/RoundedRect.png and imports it as a 9-sliced Sprite (uniform border)
/// so any UI Image can use it as a rounded background, tinted via Image.color.
/// </summary>
public class MakeRoundedSprite
{
    const string Path = "Assets/UI/RoundedRect.png";
    const int Size = 64;
    const int Radius = 18;

    public static string Execute()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var px = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // Nearest point on the inner rect inset by Radius; distance to it
                // gives a rounded-rectangle signed distance field for clean corners.
                float cx = Mathf.Clamp(x, Radius, Size - 1 - Radius);
                float cy = Mathf.Clamp(y, Radius, Size - 1 - Radius);
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(Radius + 0.5f - dist);   // 1 inside, AA at the edge
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(Path) as TextureImporter;
        if (importer == null) return "FAIL: no TextureImporter at " + Path;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(Radius, Radius, Radius, Radius); // L,B,R,T -> 9-slice
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path);
        return "RoundedRect ready at " + Path + " | spriteLoaded=" + (sprite != null) +
               " | border=" + importer.spriteBorder;
    }
}
