using UnityEditor;
using UnityEditor.SceneManagement;

public class FixScenePath
{
    public static string Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        string before = scene.path;

        // Save the live scene content over the canonical asset. The destination's
        // .meta survives, so the build-settings GUID stays valid.
        bool saved = EditorSceneManager.SaveScene(scene, "Assets/Scenes/MapScene.unity");

        bool deletedStray = false;
        if (before == "Assets/MapScene.unity")
            deletedStray = AssetDatabase.DeleteAsset("Assets/MapScene.unity");

        AssetDatabase.Refresh();
        return "was=" + before + " saved=" + saved
             + " now=" + EditorSceneManager.GetActiveScene().path
             + " deletedStray=" + deletedStray;
    }

    /// <summary>Save the active scene in place (no Save-As). Use this from now on.</summary>
    public static string SaveActive()
    {
        var scene = EditorSceneManager.GetActiveScene();
        bool saved = EditorSceneManager.SaveScene(scene);
        return "saved=" + saved + " path=" + scene.path + " dirty=" + scene.isDirty;
    }
}
