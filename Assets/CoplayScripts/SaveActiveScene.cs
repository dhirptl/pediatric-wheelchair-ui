using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Saves the currently active scene in place (same path), without going through
// the save_scene MCP tool, which has a Save-As gotcha in this project.
public static class SaveActiveScene
{
    public static string SaveActive()
    {
        var scene = EditorSceneManager.GetActiveScene();
        bool ok = EditorSceneManager.SaveScene(scene);
        return ok ? ("Saved scene in place: " + scene.path) : ("Save failed for: " + scene.path);
    }
}
