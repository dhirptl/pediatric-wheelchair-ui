using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Rounds every solid UI Image in the active scene (no TMP usage).</summary>
public class RestyleImages
{
    const string SpritePath = "Assets/UI/RoundedRect.png";

    public static string Execute()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null) return "FAIL: rounded sprite missing at " + SpritePath;

        var scene = EditorSceneManager.GetActiveScene();
        int imgCount = 0;
        foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (img == null || !img.gameObject.scene.IsValid()) continue;
            if (img.sprite != null && img.sprite != sprite) continue;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            EditorUtility.SetDirty(img);
            imgCount++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return "Rounded images=" + imgCount + " saved=" + saved;
    }
}
