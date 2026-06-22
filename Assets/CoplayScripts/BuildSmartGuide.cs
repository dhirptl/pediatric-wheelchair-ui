using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

// One-shot scene builder for Smart Guide mode. Avoids `using TMPro` on purpose:
// the Coplay executor can't compile a single file that uses both TMPro and
// UnityEngine.UI, so TMP text is set via reflection instead. Idempotent - safe to
// re-run; it skips anything it already created.
public static class BuildSmartGuide
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
    static void SetFontSize(GameObject go, float size)
    {
        var c = go.GetComponent(TMP);
        if (c != null) TMP.GetProperty("fontSize").SetValue(c, size);
    }
    static Component GetTmp(GameObject go) => go.GetComponent(TMP);

    static void ClearClicks(Button b)
    {
        for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(b.onClick, i);
    }

    static GameObject MakeButton(GameObject tmpl, Transform parent, string name, string label)
    {
        var go = Object.Instantiate(tmpl, parent);
        go.name = name;
        var b = go.GetComponent<Button>();
        ClearClicks(b);
        b.interactable = true;
        SetTextChild(go, label);
        return go;
    }

    static void MakeTarget(string name, SmartGuideTarget.TargetType type, string atRoom)
    {
        if (GameObject.Find(name) != null) return;
        var go = new GameObject(name);
        var t = go.AddComponent<SmartGuideTarget>();
        t.type = type;
        var room = GameObject.Find(atRoom);
        if (room != null) go.transform.position = room.transform.position;
    }

    public static string Execute()
    {
        if (TMP == null) return "ERROR: could not resolve TMPro.TextMeshProUGUI type.";

        var canvas = GameObject.Find("GameHUDCanvas");
        var managers = GameObject.Find("Managers");
        if (canvas == null || managers == null) return "ERROR: GameHUDCanvas or Managers not found.";

        var gmm = managers.GetComponent<GameModeManager>();
        var pausePanel = canvas.transform.Find("PauseOverlayPanel").gameObject;
        var settingsBox = pausePanel.transform.Find("SettingsBox").gameObject;
        var btnExplorer = settingsBox.transform.Find("Btn_ModeExplorer").gameObject;
        var titleTmpl = settingsBox.transform.Find("Title").gameObject;
        var refImg = pausePanel.GetComponent<Image>();

        var avatar = GameObject.Find("Wheelchair_Avatar");
        var bridge = avatar != null ? avatar.GetComponent<WheelchairStateBridge>() : null;

        int uiLayer = LayerMask.NameToLayer("UI");

        // ---------- A. SmartGuidePanel (mirrors PauseOverlayPanel) ----------
        GameObject panel = canvas.transform.Find("SmartGuidePanel")?.gameObject;
        if (panel == null)
        {
            panel = new GameObject("SmartGuidePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.layer = uiLayer;
            var prt = panel.GetComponent<RectTransform>();
            prt.SetParent(canvas.transform, false);
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            var pimg = panel.GetComponent<Image>();
            pimg.sprite = refImg.sprite; pimg.type = refImg.type; pimg.color = refImg.color;

            var box = new GameObject("SmartGuideBox", typeof(RectTransform));
            box.layer = uiLayer;
            var brt = box.GetComponent<RectTransform>();
            brt.SetParent(panel.transform, false);
            brt.anchorMin = new Vector2(0.5f, 0.5f); brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(760f, 920f);
            var vlg = box.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter; vlg.spacing = 26f;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = false; vlg.childControlHeight = false;

            var title = Object.Instantiate(titleTmpl, box.transform);
            title.name = "Txt_SmartTitle";
            SetTextSelf(title, "Where do you want to go?");
            SetFontSize(title, 52f);
            var trt = title.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(720f, 160f);

            var dist = Object.Instantiate(titleTmpl, box.transform);
            dist.name = "Txt_Distance";
            SetTextSelf(dist, "");
            SetFontSize(dist, 44f);
            dist.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, 70f);

            var btnDoor = MakeButton(btnExplorer, box.transform, "Btn_Door", "DOOR");
            var btnCare = MakeButton(btnExplorer, box.transform, "Btn_Caregiver", "GROWN-UP");
            var btnWall = MakeButton(btnExplorer, box.transform, "Btn_Wall", "WALL");
            var btnMenu = MakeButton(btnExplorer, box.transform, "Btn_SmartMenu", "BACK TO MENU");

            title.transform.SetSiblingIndex(0);
            dist.transform.SetSiblingIndex(1);
            btnDoor.transform.SetSiblingIndex(2);
            btnCare.transform.SetSiblingIndex(3);
            btnWall.transform.SetSiblingIndex(4);
            btnMenu.transform.SetSiblingIndex(5);

            var ctrl = panel.AddComponent<SmartGuideController>();

            // onClick wiring (touch path; scan ring invokes the same onClick)
            UnityEventTools.AddPersistentListener(btnDoor.GetComponent<Button>().onClick, new UnityAction(ctrl.FollowDoorway));
            UnityEventTools.AddPersistentListener(btnCare.GetComponent<Button>().onClick, new UnityAction(ctrl.FollowCaregiver));
            UnityEventTools.AddPersistentListener(btnWall.GetComponent<Button>().onClick, new UnityAction(ctrl.FollowWall));
            UnityEventTools.AddPersistentListener(btnMenu.GetComponent<Button>().onClick, new UnityAction(gmm.BackToMenu));

            // field wiring via SerializedObject (keeps this file free of TMPro types)
            var so = new SerializedObject(ctrl);
            so.FindProperty("titleLabel").objectReferenceValue = GetTmp(title);
            so.FindProperty("distanceLabel").objectReferenceValue = GetTmp(dist);
            so.FindProperty("doorButton").objectReferenceValue = btnDoor.GetComponent<Button>();
            so.FindProperty("caregiverButton").objectReferenceValue = btnCare.GetComponent<Button>();
            so.FindProperty("wallButton").objectReferenceValue = btnWall.GetComponent<Button>();
            so.FindProperty("menuButton").objectReferenceValue = btnMenu.GetComponent<Button>();
            if (bridge != null) so.FindProperty("bridge").objectReferenceValue = bridge;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
        }

        // assign GameModeManager.smartGuidePanel
        var gso = new SerializedObject(gmm);
        gso.FindProperty("smartGuidePanel").objectReferenceValue = panel;
        gso.ApplyModifiedPropertiesWithoutUndo();

        // Render the panel below the overlays so the pause/settings menu draws on top
        // of it (otherwise the Smart Guide screen lingers over the menu).
        panel.transform.SetSiblingIndex(pausePanel.transform.GetSiblingIndex());

        // keep the controller's arrival radius above the NavMeshAgent stopping plateau
        var existingCtrl = panel.GetComponent<SmartGuideController>();
        if (existingCtrl != null)
        {
            var cso = new SerializedObject(existingCtrl);
            cso.FindProperty("arrivalRadius").floatValue = 1.25f;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- B. Pause-overlay SMART GUIDE button ----------
        GameObject smartBtn = settingsBox.transform.Find("Btn_ModeSmartGuide")?.gameObject;
        if (smartBtn == null)
        {
            smartBtn = MakeButton(btnExplorer, settingsBox.transform, "Btn_ModeSmartGuide", "SMART GUIDE");
            UnityEventTools.AddPersistentListener(smartBtn.GetComponent<Button>().onClick, new UnityAction(gmm.SetModeSmartGuide));
            smartBtn.transform.SetSiblingIndex(btnExplorer.transform.GetSiblingIndex() + 1);
        }

        // add to the pause panel's scan ring (if not already present)
        var psc = pausePanel.GetComponent<PanelScanController>();
        var pso = new SerializedObject(psc);
        var optsProp = pso.FindProperty("group.options");
        bool present = false;
        for (int i = 0; i < optsProp.arraySize; i++)
            if (optsProp.GetArrayElementAtIndex(i).objectReferenceValue == smartBtn.GetComponent<Button>()) present = true;
        if (!present)
        {
            int idx = optsProp.arraySize;
            optsProp.arraySize = idx + 1;
            optsProp.GetArrayElementAtIndex(idx).objectReferenceValue = smartBtn.GetComponent<Button>();
            pso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- C. Backend host + placeholder target markers ----------
        if (GameObject.Find("FollowAssistBackend") == null)
            new GameObject("FollowAssistBackend").AddComponent<FollowAssistBackend>();

        MakeTarget("SmartTarget_Doorway", SmartGuideTarget.TargetType.Doorway, "Target_Kitchen");
        MakeTarget("SmartTarget_Caregiver", SmartGuideTarget.TargetType.Caregiver, "Target_LivingRoom");
        MakeTarget("SmartTarget_Wall", SmartGuideTarget.TargetType.WallCorridor, "Target_Bedroom");

        EditorUtility.SetDirty(gmm);
        EditorUtility.SetDirty(psc);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return "OK: " + SaveActiveScene.SaveActive();
    }
}
