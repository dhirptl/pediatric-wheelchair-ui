using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

public class FixHudLabels
{
    public static string Execute()
    {
        // LiberationSans SDF lacks the ☰ and ★ glyphs (rendered as boxes).
        var menuLabel = GameObject.Find("GameHUDCanvas/TopBar/Btn_BackToMenu/Label");
        if (menuLabel != null) menuLabel.GetComponent<TextMeshProUGUI>().text = "MENU";

        var points = GameObject.Find("GameHUDCanvas/TopBar/Txt_Points");
        if (points != null) points.GetComponent<TextMeshProUGUI>().text = "PTS: 0";

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        return "labels fixed, saved=" + EditorSceneManager.SaveScene(scene);
    }
}
