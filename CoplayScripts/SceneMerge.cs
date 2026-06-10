using UnityEditor;
using UnityEngine;
using System.Text;

public class SceneMerge
{
    public static string Execute()
    {
        var log = new StringBuilder();

        // 1. Delete the stale Assets/Scenes copies (folder + contents).
        if (AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            bool deleted = AssetDatabase.DeleteAsset("Assets/Scenes");
            log.AppendLine("Deleted stale Assets/Scenes: " + deleted);
        }
        else
        {
            log.AppendLine("Assets/Scenes not found (already gone).");
        }

        // 2. Move the working folder into the canonical path. Meta files move with
        //    the assets, so GUIDs (and the open scene's references) are preserved.
        string err = AssetDatabase.MoveAsset("Assets/Scenes 1", "Assets/Scenes");
        if (!string.IsNullOrEmpty(err))
        {
            log.AppendLine("MOVE FAILED: " + err);
            return log.ToString();
        }
        log.AppendLine("Moved 'Assets/Scenes 1' -> 'Assets/Scenes'.");

        // 3. Rewrite build settings: MainMenu first (entry scene), then MapScene.
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/MapScene.unity", true),
        };
        log.AppendLine("Build settings rewritten: MainMenu, MapScene.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        foreach (var s in EditorBuildSettings.scenes)
            log.AppendLine("Build scene: " + s.path + " enabled=" + s.enabled + " guid=" + s.guid);

        return log.ToString();
    }
}
