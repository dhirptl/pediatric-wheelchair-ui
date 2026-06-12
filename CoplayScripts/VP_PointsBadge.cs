using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives the top-right points counter a black rounded badge like the Settings
/// button: a sliced RoundedRect Image placed behind Txt_Points.
/// </summary>
public class VP_PointsBadge
{
    public static string Execute()
    {
        var points = GameObject.Find("GameHUDCanvas/TopBar/Txt_Points");
        if (points == null) return "Txt_Points not found";
        var topBar = points.transform.parent;
        var pointsRt = (RectTransform)points.transform;

        Transform existing = topBar.Find("PointsBadge");
        GameObject badgeGo = existing != null ? existing.gameObject
            : new GameObject("PointsBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeGo.transform.SetParent(topBar, false);
        badgeGo.layer = points.layer;

        var rt = (RectTransform)badgeGo.transform;
        rt.anchorMin = pointsRt.anchorMin;
        rt.anchorMax = pointsRt.anchorMax;
        rt.pivot = pointsRt.pivot;
        rt.anchoredPosition = pointsRt.anchoredPosition;
        rt.sizeDelta = pointsRt.sizeDelta;

        var img = badgeGo.GetComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/RoundedRect.png");
        img.type = Image.Type.Sliced;
        img.color = Color.black;
        img.raycastTarget = false;

        var shadow = badgeGo.GetComponent<Shadow>();
        if (shadow == null) shadow = badgeGo.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);

        // Badge sits behind the label.
        badgeGo.transform.SetSiblingIndex(points.transform.GetSiblingIndex());

        EditorUtility.SetDirty(badgeGo);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        return "badge " + (existing != null ? "updated" : "created");
    }
}
