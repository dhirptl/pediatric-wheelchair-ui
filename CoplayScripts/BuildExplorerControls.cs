using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 3: rebuilds the explorer control UI under GameHUDCanvas/ExplorerControls:
/// a restyled high-contrast dashboard (TURN LEFT / FORWARD / TURN RIGHT) and a
/// 2x2 grid menu for two-stage scanning, both driven by ExplorerUIController.
/// Idempotent: deletes and rebuilds ExplorerControls if it already exists.
/// </summary>
public class BuildExplorerControls
{
    static readonly Color Yellow = new Color(1f, 1f, 0f, 1f);
    static readonly Color Black = new Color(0f, 0f, 0f, 1f);

    const string RoundedSpritePath = "Assets/UI/RoundedRect.png";
    const string BubblyFontPath = "Assets/Fonts/Fredoka SDF.asset";
    static Sprite _rounded;
    static TMP_FontAsset _bubbly;
    static Sprite Rounded => _rounded != null ? _rounded : (_rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath));
    static TMP_FontAsset Bubbly => _bubbly != null ? _bubbly : (_bubbly = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BubblyFontPath));

    /// <summary>Give a UI Image rounded corners via the shared 9-sliced sprite.</summary>
    static void ApplyRounded(Image img)
    {
        var s = Rounded;
        if (s == null || img == null) return;
        img.sprite = s;
        img.type = Image.Type.Sliced;
    }

    public static string Execute()
    {
        var canvas = GameObject.Find("GameHUDCanvas");
        if (canvas == null) return "GameHUDCanvas not found";

        // --- container ----------------------------------------------------------
        var oldControls = canvas.transform.Find("ExplorerControls");
        if (oldControls != null) Object.DestroyImmediate(oldControls.gameObject);

        var controlsGO = new GameObject("ExplorerControls", typeof(RectTransform));
        controlsGO.layer = LayerMask.NameToLayer("UI");
        var controlsRT = controlsGO.GetComponent<RectTransform>();
        controlsRT.SetParent(canvas.transform, false);
        controlsRT.anchorMin = Vector2.zero;
        controlsRT.anchorMax = Vector2.one;
        controlsRT.offsetMin = Vector2.zero;
        controlsRT.offsetMax = Vector2.zero;
        // Keep the pause overlay rendering above the controls.
        var overlay = canvas.transform.Find("PauseOverlayPanel");
        if (overlay != null) controlsRT.SetSiblingIndex(overlay.GetSiblingIndex());

        // --- dashboard (rebuild fresh; old buttons were legacy-styled) -----------
        var oldDash = canvas.transform.Find("ControlDashboard");
        if (oldDash != null) Object.DestroyImmediate(oldDash.gameObject);

        var dash = new GameObject("ControlDashboard", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        dash.layer = LayerMask.NameToLayer("UI");
        var dashRT = dash.GetComponent<RectTransform>();
        dashRT.SetParent(controlsRT, false);
        dashRT.anchorMin = new Vector2(0f, 0f);
        dashRT.anchorMax = new Vector2(1f, 0f);
        dashRT.pivot = new Vector2(0.5f, 0f);
        dashRT.sizeDelta = new Vector2(0f, 290f);
        dashRT.anchoredPosition = Vector2.zero;
        dash.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        ApplyRounded(dash.GetComponent<Image>());
        AddOutline(dash, 2f);
        var hl = dash.GetComponent<HorizontalLayoutGroup>();
        hl.spacing = 48f;
        hl.padding = new RectOffset(40, 40, 30, 30);
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = false;
        hl.childControlHeight = false;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;

        var leftBtn = NewBigButton("Btn_TurnLeft", dash.transform, "TURN LEFT", new Vector2(430f, 220f), 52f);
        var fwdBtn = NewBigButton("Btn_MoveForward", dash.transform, "FORWARD", new Vector2(430f, 220f), 52f);
        var rightBtn = NewBigButton("Btn_TurnRight", dash.transform, "TURN RIGHT", new Vector2(430f, 220f), 52f);

        // --- grid menu (strategy 2) ----------------------------------------------
        var gridGO = new GameObject("GridMenu", typeof(RectTransform), typeof(Image), typeof(GridLayoutGroup));
        gridGO.layer = LayerMask.NameToLayer("UI");
        var gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.SetParent(controlsRT, false);
        gridRT.anchorMin = new Vector2(0.5f, 0f);
        gridRT.anchorMax = new Vector2(0.5f, 0f);
        gridRT.pivot = new Vector2(0.5f, 0f);
        gridRT.anchoredPosition = new Vector2(0f, 40f);
        gridRT.sizeDelta = new Vector2(860f, 440f);
        gridGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        ApplyRounded(gridGO.GetComponent<Image>());
        AddOutline(gridGO, 2f);
        var grid = gridGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(400f, 190f);
        grid.spacing = new Vector2(20f, 20f);
        grid.padding = new RectOffset(20, 20, 20, 20);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.MiddleCenter;

        // Row-major order must match ExplorerUIController.gridActions.
        var gFwd = NewBigButton("Btn_GridForward", gridGO.transform, "FORWARD", Vector2.zero, 44f);
        var gLeft = NewBigButton("Btn_GridTurnLeft", gridGO.transform, "TURN LEFT", Vector2.zero, 44f);
        var gRight = NewBigButton("Btn_GridTurnRight", gridGO.transform, "TURN RIGHT", Vector2.zero, 44f);
        var gStop = NewBigButton("Btn_GridStop", gridGO.transform, "STOP", Vector2.zero, 44f);
        gridGO.SetActive(false);

        // --- controller ---------------------------------------------------------
        var ctrl = controlsGO.AddComponent<ExplorerUIController>();
        ctrl.dashboardPanel = dash;
        ctrl.gridMenuPanel = gridGO;
        ctrl.forwardButton = fwdBtn;
        ctrl.turnRightButton = rightBtn;
        ctrl.turnLeftButton = leftBtn;
        ctrl.gridButtons = new[] { gFwd, gLeft, gRight, gStop };
        ctrl.gridColumns = 2;

        UnityEventTools.AddPersistentListener(leftBtn.onClick, ctrl.OnTurnLeftClicked);
        UnityEventTools.AddPersistentListener(fwdBtn.onClick, ctrl.OnMoveForwardClicked);
        UnityEventTools.AddPersistentListener(rightBtn.onClick, ctrl.OnTurnRightClicked);
        UnityEventTools.AddPersistentListener(gFwd.onClick, ctrl.OnMoveForwardClicked);
        UnityEventTools.AddPersistentListener(gLeft.onClick, ctrl.OnTurnLeftClicked);
        UnityEventTools.AddPersistentListener(gRight.onClick, ctrl.OnTurnRightClicked);
        UnityEventTools.AddPersistentListener(gStop.onClick, ctrl.OnStopClicked);

        // --- mode manager rewire --------------------------------------------------
        var managers = GameObject.Find("Managers");
        var gmm = managers != null ? managers.GetComponent<GameModeManager>() : null;
        if (gmm != null) gmm.explorerDashboard = controlsGO;

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return "ExplorerControls built, gmm wired=" + (gmm != null) + " saved=" + saved;
    }

    static void AddOutline(GameObject go, float thickness)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Yellow;
        outline.effectDistance = new Vector2(thickness, -thickness);
    }

    static Button NewBigButton(string name, Transform parent, string label, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonHighlighter));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        if (size != Vector2.zero) rt.sizeDelta = size; // grid cells are sized by the layout

        var img = go.GetComponent<Image>();
        img.color = Black;
        ApplyRounded(img);
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Yellow;
        outline.effectDistance = new Vector2(4f, -4f);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.1f, 1f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.1f, 1f);
        btn.colors = colors;

        var txtGO = new GameObject("Label", typeof(RectTransform));
        txtGO.layer = LayerMask.NameToLayer("UI");
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.SetParent(go.transform, false);
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        if (Bubbly != null) tmp.font = Bubbly;
        tmp.text = label;
        tmp.color = Yellow;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        // Auto-size so labels always fit the button (with breathing room) instead
        // of overflowing at a fixed point size.
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 24f;
        tmp.fontSizeMax = fontSize;
        tmp.enableWordWrapping = true;
        tmp.margin = new Vector4(14f, 8f, 14f, 8f);

        return btn;
    }
}
