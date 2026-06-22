using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

// One-shot upgrade of the existing SmartGuidePanel to the redesigned two-phase HUD:
//  - relabels/reorders the target buttons to GROWN-UP / WALL / EXIT-DOORWAY
//  - sets the prompt to "What do you want to follow?"
//  - adds Txt_FollowName + Btn_StopFollow and wires the controller's new fields.
// Avoids `using TMPro` (the Coplay executor can't compile TMPro + UnityEngine.UI in
// one file) so TMP text is set via reflection. Idempotent - safe to re-run.
public static class UpgradeSmartGuideHUD
{
    static readonly System.Type TMP = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");

    static void SetTextSelf(GameObject go, string text)
    {
        var c = go.GetComponent(TMP);
        if (c != null) TMP.GetProperty("text").SetValue(c, text);
    }
    static void SetTextChild(GameObject go, string text)
    {
        var c = go.GetComponentInChildren(TMP, true);
        if (c != null) TMP.GetProperty("text").SetValue(c, text);
    }
    static Component GetTmp(GameObject go) => go.GetComponent(TMP);

    static void ClearClicks(Button b)
    {
        for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(b.onClick, i);
    }

    public static string Execute()
    {
        if (TMP == null) return "ERROR: could not resolve TMPro.TextMeshProUGUI type.";

        var canvas = GameObject.Find("GameHUDCanvas");
        if (canvas == null) return "ERROR: GameHUDCanvas not found.";

        var panelT = canvas.transform.Find("SmartGuidePanel");
        if (panelT == null) return "ERROR: SmartGuidePanel not found.";
        var panel = panelT.gameObject;
        var box = panel.transform.Find("SmartGuideBox");
        if (box == null) return "ERROR: SmartGuideBox not found.";

        var ctrl = panel.GetComponent<SmartGuideController>();
        if (ctrl == null) return "ERROR: SmartGuideController missing.";

        var title    = box.Find("Txt_SmartTitle")?.gameObject;
        var dist     = box.Find("Txt_Distance")?.gameObject;
        var btnCare  = box.Find("Btn_Caregiver")?.gameObject;
        var btnWall  = box.Find("Btn_Wall")?.gameObject;
        var btnDoor  = box.Find("Btn_Door")?.gameObject;
        var btnMenu  = box.Find("Btn_SmartMenu")?.gameObject;
        if (title == null || dist == null || btnCare == null || btnWall == null || btnDoor == null || btnMenu == null)
            return "ERROR: expected SmartGuideBox children missing.";

        // ---- Prompt + button labels (TDD wording / order) ----
        SetTextSelf(title, "What do you want to follow?");
        SetTextChild(btnCare, "GROWN-UP");
        SetTextChild(btnWall, "WALL");
        SetTextChild(btnDoor, "EXIT / DOORWAY");

        // ---- Txt_FollowName (clone of the title) ----
        var followNameT = box.Find("Txt_FollowName");
        GameObject followName;
        if (followNameT == null)
        {
            followName = Object.Instantiate(title, box);
            followName.name = "Txt_FollowName";
            SetTextSelf(followName, "Following:");
        }
        else followName = followNameT.gameObject;

        // ---- Btn_StopFollow (clone of the menu button) ----
        var stopT = box.Find("Btn_StopFollow");
        GameObject stop;
        if (stopT == null)
        {
            stop = Object.Instantiate(btnMenu, box);
            stop.name = "Btn_StopFollow";
            SetTextChild(stop, "STOP FOLLOWING");
            var sb = stop.GetComponent<Button>();
            ClearClicks(sb);
            sb.interactable = true;
            UnityEventTools.AddPersistentListener(sb.onClick, new UnityAction(ctrl.StopFollowing));
        }
        else stop = stopT.gameObject;

        // ---- Sibling order: title, followName, distance, care, wall, door, stop, menu ----
        title.transform.SetSiblingIndex(0);
        followName.transform.SetSiblingIndex(1);
        dist.transform.SetSiblingIndex(2);
        btnCare.transform.SetSiblingIndex(3);
        btnWall.transform.SetSiblingIndex(4);
        btnDoor.transform.SetSiblingIndex(5);
        stop.transform.SetSiblingIndex(6);
        btnMenu.transform.SetSiblingIndex(7);

        // ---- Wire the controller's new fields ----
        var so = new SerializedObject(ctrl);
        so.FindProperty("followNameLabel").objectReferenceValue = GetTmp(followName);
        so.FindProperty("stopButton").objectReferenceValue = stop.GetComponent<Button>();
        so.FindProperty("promptText").stringValue = "What do you want to follow?";
        so.FindProperty("logInterval").floatValue = 1.0f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Sensible editor defaults; the controller manages these live in OnEnable.
        followName.SetActive(false);
        stop.SetActive(false);

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        return "OK: SmartGuide HUD upgraded (followName + stop wired).";
    }
}
