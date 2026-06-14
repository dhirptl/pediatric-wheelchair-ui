using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Phase 2 HUD in MapScene: GameHUDCanvas (TopBar + pause overlay +
/// destination panel + mini-map) and the MiniMapCamera/RenderTexture pipeline.
/// Idempotent: deletes and rebuilds GameHUDCanvas/MiniMapCamera if they exist.
/// </summary>
public class BuildGameHUD
{
    static readonly Color Yellow = new Color(1f, 1f, 0f, 1f);
    static readonly Color Black = new Color(0f, 0f, 0f, 1f);
    static readonly Color PanelBlack = new Color(0f, 0f, 0f, 0.85f);

    const string RoundedSpritePath = "Assets/UI/RoundedRect.png";
    static Sprite _rounded;
    static Sprite Rounded => _rounded != null ? _rounded : (_rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath));

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
        // --- Clean rebuild ---------------------------------------------------
        foreach (var name in new[] { "GameHUDCanvas", "MiniMapCamera" })
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        // --- Render texture ---------------------------------------------------
        const string rtPath = "Assets/Textures/MiniMap_Texture.renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if (rt == null)
        {
            rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
            AssetDatabase.CreateAsset(rt, rtPath);
        }

        // --- Mini-map camera ---------------------------------------------------
        var camGO = new GameObject("MiniMapCamera");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 160f; // reframed at runtime by MiniMapController
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Black;
        cam.targetTexture = rt;
        camGO.transform.position = new Vector3(0f, 60f, 0f);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camGO.AddComponent<MiniMapController>();

        // --- Canvas ------------------------------------------------------------
        var canvasGO = new GameObject("GameHUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // --- Top bar -----------------------------------------------------------
        var topBar = NewPanel("TopBar", canvasGO.transform, PanelBlack);
        SetAnchors(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        topBar.rectTransform.sizeDelta = new Vector2(0f, 110f);
        topBar.rectTransform.anchoredPosition = Vector2.zero;
        ApplyRounded(topBar);
        AddOutline(topBar.gameObject, 2f);

        var backBtn = NewButton("Btn_BackToMenu", topBar.transform, "MENU", new Vector2(300f, 90f), 44f);
        SetAnchors((Image)backBtn.targetGraphic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        backBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, 0f);

        var prompt = NewText("Txt_CalibrationPrompt", topBar.transform, "", 34f, Yellow, TextAlignmentOptions.Center);
        SetAnchors(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        prompt.rectTransform.sizeDelta = new Vector2(1100f, 90f);
        prompt.fontStyle = FontStyles.Bold;

        var points = NewText("Txt_Points", topBar.transform, "PTS: 0", 48f, Yellow, TextAlignmentOptions.Right);
        SetAnchors(points.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        points.rectTransform.anchoredPosition = new Vector2(-24f, 0f);
        points.rectTransform.sizeDelta = new Vector2(320f, 90f);
        points.fontStyle = FontStyles.Bold;

        // --- Mini-map panel (yellow frame + clickable RawImage) -----------------
        var frame = NewPanel("MiniMapPanel", canvasGO.transform, Yellow);
        SetAnchors(frame, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        frame.rectTransform.anchoredPosition = new Vector2(-16f, -126f);
        frame.rectTransform.sizeDelta = new Vector2(420f, 420f);
        ApplyRounded(frame);

        var rawGO = new GameObject("MiniMapImage", typeof(RectTransform), typeof(RawImage), typeof(MiniMapClickHandler));
        rawGO.layer = LayerMask.NameToLayer("UI");
        var rawRT = rawGO.GetComponent<RectTransform>();
        rawRT.SetParent(frame.transform, false);
        rawRT.anchorMin = Vector2.zero;
        rawRT.anchorMax = Vector2.one;
        rawRT.offsetMin = new Vector2(6f, 6f);
        rawRT.offsetMax = new Vector2(-6f, -6f);
        rawGO.GetComponent<RawImage>().texture = rt;

        // --- Destination panel (Magic Travel) -----------------------------------
        var destPanel = NewPanel("DestinationPanel", canvasGO.transform, new Color(0f, 0f, 0f, 0.7f));
        SetAnchors(destPanel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        destPanel.rectTransform.anchoredPosition = new Vector2(16f, 0f);
        destPanel.rectTransform.sizeDelta = new Vector2(370f, 700f);
        ApplyRounded(destPanel);
        AddOutline(destPanel.gameObject, 2f);
        var destLayout = destPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        destLayout.spacing = 18f;
        destLayout.padding = new RectOffset(20, 20, 20, 20);
        destLayout.childAlignment = TextAnchor.UpperCenter;
        destLayout.childControlWidth = false;
        destLayout.childControlHeight = false;
        destLayout.childForceExpandWidth = false;
        destLayout.childForceExpandHeight = false;

        var destTitle = NewText("Title", destPanel.transform, "MAGIC TRAVEL", 40f, Yellow, TextAlignmentOptions.Center);
        destTitle.rectTransform.sizeDelta = new Vector2(330f, 60f);
        destTitle.fontStyle = FontStyles.Bold;

        var roomButtons = new (string label, string target)[]
        {
            ("KITCHEN", "Target_Kitchen"),
            ("BATHROOM", "Target_Bathroom"),
            ("LIVING ROOM", "Target_LivingRoom"),
            ("BEDROOM", "Target_Bedroom"),
        };

        // --- Pause / settings overlay -------------------------------------------
        var overlay = NewPanel("PauseOverlayPanel", canvasGO.transform, new Color(0f, 0f, 0f, 0.92f));
        overlay.rectTransform.anchorMin = Vector2.zero;
        overlay.rectTransform.anchorMax = Vector2.one;
        overlay.rectTransform.offsetMin = Vector2.zero;
        overlay.rectTransform.offsetMax = Vector2.zero;

        var box = new GameObject("SettingsBox", typeof(RectTransform), typeof(VerticalLayoutGroup));
        box.layer = LayerMask.NameToLayer("UI");
        var boxRT = box.GetComponent<RectTransform>();
        boxRT.SetParent(overlay.transform, false);
        boxRT.sizeDelta = new Vector2(720f, 920f);
        var boxLayout = box.GetComponent<VerticalLayoutGroup>();
        boxLayout.spacing = 26f;
        boxLayout.childAlignment = TextAnchor.MiddleCenter;
        boxLayout.childControlWidth = false;
        boxLayout.childControlHeight = false;
        boxLayout.childForceExpandWidth = false;
        boxLayout.childForceExpandHeight = false;

        var title = NewText("Title", box.transform, "SETTINGS", 64f, Yellow, TextAlignmentOptions.Center);
        title.rectTransform.sizeDelta = new Vector2(640f, 100f);
        title.fontStyle = FontStyles.Bold;

        var magicBtn = NewButton("Btn_ModeMagicTravel", box.transform, "MODE: MAGIC TRAVEL", new Vector2(640f, 120f), 42f);
        var explorerBtn = NewButton("Btn_ModeExplorer", box.transform, "MODE: EXPLORER", new Vector2(640f, 120f), 42f);
        var shopBtn = NewButton("Btn_ThemeShop", box.transform, "THEME SHOP", new Vector2(640f, 120f), 42f);
        var resumeBtn = NewButton("Btn_Resume", box.transform, "RESUME", new Vector2(640f, 120f), 42f);
        var quitBtn = NewButton("Btn_QuitToMenu", box.transform, "QUIT TO MENU", new Vector2(640f, 120f), 42f);

        // --- Migrate ControlDashboard from the old ExplorerCanvas ----------------
        var oldCanvas = GameObject.Find("ExplorerCanvas");
        GameObject dashboard = null;
        ExplorerUIController newCtrl = null;
        if (oldCanvas != null)
        {
            var oldCtrl = oldCanvas.GetComponent<ExplorerUIController>();
            var dashT = oldCanvas.transform.Find("ControlDashboard");
            if (dashT != null)
            {
                dashboard = dashT.gameObject;
                dashT.SetParent(canvasGO.transform, false);
                var dashRT = (RectTransform)dashT;
                dashRT.anchorMin = new Vector2(0.5f, 0f);
                dashRT.anchorMax = new Vector2(0.5f, 0f);
                dashRT.pivot = new Vector2(0.5f, 0f);
                dashRT.anchoredPosition = new Vector2(0f, 24f);

                newCtrl = dashboard.AddComponent<ExplorerUIController>();
                if (oldCtrl != null)
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(oldCtrl), newCtrl);
                newCtrl.forwardButton = dashT.Find("Btn_MoveForward") != null ? dashT.Find("Btn_MoveForward").GetComponent<Button>() : null;
                newCtrl.turnRightButton = dashT.Find("Btn_TurnRight") != null ? dashT.Find("Btn_TurnRight").GetComponent<Button>() : null;
                newCtrl.bridge = null; // auto-found by avatar name at Start
            }
            Object.DestroyImmediate(oldCanvas);
        }

        // --- Managers wiring ------------------------------------------------------
        var managers = GameObject.Find("Managers");
        if (managers == null) managers = new GameObject("Managers");

        var gmm = managers.GetComponent<GameModeManager>();
        if (gmm == null) gmm = managers.AddComponent<GameModeManager>();
        gmm.explorerDashboard = dashboard;
        gmm.destinationPanel = destPanel.gameObject;
        gmm.pauseOverlay = overlay.gameObject;

        var mtc = managers.GetComponent<MagicTravelController>();
        if (mtc == null) mtc = managers.AddComponent<MagicTravelController>();

        var cal = managers.GetComponent<RoomCalibrationManager>();
        if (cal != null) cal.promptText = prompt;

        // --- Room buttons (created after mtc exists so listeners can bind) -------
        foreach (var (label, target) in roomButtons)
        {
            var b = NewButton("Btn_" + target, destPanel.transform, label, new Vector2(330f, 120f), 40f);
            UnityEventTools.AddStringPersistentListener(b.onClick, mtc.OnRoomButton, target);
        }

        // --- Persistent listeners --------------------------------------------------
        UnityEventTools.AddPersistentListener(backBtn.onClick, gmm.OpenPause);
        UnityEventTools.AddPersistentListener(resumeBtn.onClick, gmm.ClosePause);
        UnityEventTools.AddPersistentListener(magicBtn.onClick, gmm.SetModeMagicTravel);
        UnityEventTools.AddPersistentListener(explorerBtn.onClick, gmm.SetModeExplorer);
        UnityEventTools.AddPersistentListener(quitBtn.onClick, gmm.QuitToMenu);
        // shopBtn gets its listener in Phase 4 with the Theme Shop panel.
        shopBtn.interactable = false;

        // Default editor visibility: overlay hidden; runtime toggles the rest.
        overlay.gameObject.SetActive(false);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return "HUD built. dashboard=" + (dashboard != null) + " ctrl=" + (newCtrl != null) + " saved=" + saved;
    }

    // --- helpers -----------------------------------------------------------------

    static Image NewPanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = LayerMask.NameToLayer("UI");
        go.GetComponent<RectTransform>().SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void SetAnchors(Graphic g, Vector2 min, Vector2 max, Vector2 pivot)
        => SetAnchors(g.rectTransform, min, max, pivot);

    static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
    }

    static void AddOutline(GameObject go, float thickness)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Yellow;
        outline.effectDistance = new Vector2(thickness, -thickness);
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.GetComponent<RectTransform>().SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        return tmp;
    }

    static Button NewButton(string name, Transform parent, string label, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = Black;
        ApplyRounded(img);
        AddOutline(go, 3f);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.1f, 1f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.1f, 1f);
        btn.colors = colors;

        var txt = NewText("Label", go.transform, label, fontSize, Yellow, TextAlignmentOptions.Center);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = Vector2.zero;
        txt.rectTransform.offsetMax = Vector2.zero;
        txt.fontStyle = FontStyles.Bold;
        // Auto-size so longer labels stay inside the button.
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 20f;
        txt.fontSizeMax = fontSize;
        txt.margin = new Vector4(12f, 6f, 12f, 6f);

        return btn;
    }
}
