using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SetFloorMat
{
    public static string Execute()
    {
        var go = GameObject.Find("MapBuilder");
        if (go == null) return "MapBuilder not found";
        var gen = go.GetComponent<MapGenerator>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HighContrast_Floor.mat");
        if (gen == null || mat == null) return "gen=" + (gen != null) + " mat=" + (mat != null);

        // SerializedObject records the change as a prefab-instance override, so it
        // survives the play-mode serialization round-trip (plain reflection does not).
        var so = new SerializedObject(gen);
        so.FindProperty("floorMaterial").objectReferenceValue = mat;
        so.ApplyModifiedProperties();

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return "floorMaterial=" + gen.floorMaterial.name + " saved=" + saved;
    }
}
