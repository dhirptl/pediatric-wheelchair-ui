using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-shot wiring: drops the WheelchairModel prefab under Wheelchair_Avatar as a
/// visual-only child, hides the placeholder capsule renderer, and attaches the wheel
/// animator. Navigation (NavMeshAgent + capsule collider) is left untouched.
/// </summary>
public static class WheelchairSceneWire
{
    public static string Wire()
    {
        var avatar = GameObject.Find("Wheelchair_Avatar");
        if (avatar == null) return "ERROR: Wheelchair_Avatar not found in active scene.";

        // Hide the placeholder capsule (keep collider + NavMeshAgent).
        var capsuleRenderer = avatar.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null) capsuleRenderer.enabled = false;

        // Remove any prior model instance so this is idempotent.
        var existing = avatar.transform.Find("WheelchairModel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PreFabs/WheelchairModel.prefab");
        if (prefab == null) return "ERROR: WheelchairModel.prefab not found. Run the importer first.";

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(avatar.transform, false); // keep prefab's local pos/rot

        // Attach + wire the wheel animator.
        var anim = instance.GetComponent<WheelchairWheelAnimator>();
        if (anim == null) anim = instance.AddComponent<WheelchairWheelAnimator>();

        Transform body = instance.transform.Find("body_frame");
        anim.drivenWheels = new[] { body.Find("L_driven"), body.Find("R_driven") };
        anim.casters = new[]
        {
            body.Find("L_front"), body.Find("R_front"),
            body.Find("L_back"), body.Find("R_back")
        };

        EditorUtility.SetDirty(anim);
        EditorUtility.SetDirty(avatar);
        EditorSceneManager.MarkSceneDirty(avatar.scene);
        bool saved = EditorSceneManager.SaveScene(avatar.scene);

        return $"Wired model under Wheelchair_Avatar (capsule hidden, animator attached). Scene saved: {saved}";
    }
}
