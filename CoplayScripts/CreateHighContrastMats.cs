using UnityEditor;
using UnityEngine;

public class CreateHighContrastMats
{
    public static string Execute()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return "URP Lit shader not found";

        // Pitch-black floor, low smoothness so it reads as a flat dark field
        // (specular glare would wash out the contrast for low-vision users).
        var floor = new Material(lit);
        floor.SetColor("_BaseColor", Color.black);
        floor.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(floor, "Assets/Materials/HighContrast_Floor.mat");

        // Vivid neon-yellow walls with mild emission so they stay bright in the
        // dark scene and pop on the mini-map.
        var wall = new Material(lit);
        Color neonYellow = new Color(1f, 1f, 0f, 1f);
        wall.SetColor("_BaseColor", neonYellow);
        wall.SetFloat("_Smoothness", 0.2f);
        wall.EnableKeyword("_EMISSION");
        wall.SetColor("_EmissionColor", neonYellow * 0.35f);
        wall.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(wall, "Assets/Materials/HighContrast_Wall.mat");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Created HighContrast_Floor.mat and HighContrast_Wall.mat";
    }
}
