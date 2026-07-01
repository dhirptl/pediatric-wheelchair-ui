using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// One-shot editor helper: adds the FoodManager component to the scene's "Managers"
/// object so the shop's FOODS section has a catalog to build from. ThemeShopController
/// (already on Managers) and the food sprites (loaded from Resources at runtime) need
/// no further wiring. UnityEngine + UnityEditor only, returns a summary, saves in place.
/// Idempotent.
/// </summary>
public static class AddFoodShop
{
    public static string Execute()
    {
        var managers = GameObject.Find("Managers");
        if (managers == null) return "ABORT: 'Managers' GameObject not found in the open scene";

        var log = new System.Text.StringBuilder();
        var fm = managers.GetComponent<FoodManager>();
        if (fm == null) { managers.AddComponent<FoodManager>(); log.Append("FoodManager added; "); }
        else log.Append("FoodManager already present; ");

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return log + "saved=" + saved + " path=" + scene.path;
    }
}
