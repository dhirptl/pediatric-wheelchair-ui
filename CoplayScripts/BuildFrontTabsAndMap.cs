using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Surfaces the mode switcher as a persistent top tab bar, adds a full-screen
/// 2D-map view + wheelchair icon, and wires the new TwoDMapView / ModeTabBar
/// components. Idempotent: removes the objects it creates before rebuilding.
/// Run via Coplay execute_script (editor only); delete when done.
/// </summary>
public static class BuildFrontTabsAndMap
{
    static readonly Color Yellow = new Color(1f, 1f, 0f, 1f);
    static readonly Color Black = new Color(0f, 0f, 0f, 1f);

    static Sprite _rounded;
    static TMP_FontAsset _fredoka;

    public static string Execute()
    {
        _rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/RoundedRect.png");
        _fredoka = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka SDF.asset");
        var wheelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI_Sprites/WheelchairIcon.png");
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Textures/MiniMap_Texture.renderTexture");

        var canvas = GameObject.Find("GameHUDCanvas");
        if (canvas == null) return "ERROR: GameHUDCanvas not found";
        var topBar = canvas.transform.Find("TopBar");
        if (topBar == null) return "ERROR: TopBar not found";
        var miniMapPanel = canvas.transform.Find("MiniMapPanel");
        var managers = GameObject.Find("Managers");
        if (managers == null) return "ERROR: Managers not found";

        var gmm = managers.GetComponent<GameModeManager>();
        var shop = managers.GetComponent<ThemeShopController>();
        var miniMap = Object.FindObjectOfType<MiniMapController>();

        // --- idempotent cleanup ---------------------------------------------
        foreach (var n in new[] { "Btn_BackToMenu", "Btn_TabMagic", "Btn_TabExplorer",
                                  "Btn_TabGuide", "Btn_TabMap", "Btn_TabShop" })
        {
            var old = topBar.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var oldMap = canvas.transform.Find("TwoDMapFull");
        if (oldMap != null) Object.DestroyImmediate(oldMap.gameObject);

        // --- move the calibration prompt below the bar so tabs have room -----
        var promptT = topBar.Find("Txt_CalibrationPrompt");
        if (promptT != null)
        {
            var prt = (RectTransform)promptT;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -118f);
            prt.sizeDelta = new Vector2(1100f, 80f);
        }

        // --- top tab bar -----------------------------------------------------
        var magic = NewTab("Btn_TabMagic", topBar, "MAGIC", 16f, 210f);
        var explorer = NewTab("Btn_TabExplorer", topBar, "EXPLORER", 234f, 230f);
        var guide = NewTab("Btn_TabGuide", topBar, "GUIDE", 472f, 190f);
        var map = NewTab("Btn_TabMap", topBar, "2D MAP", 670f, 210f);
        var shopTab = NewTab("Btn_TabShop", topBar, "SHOP", 888f, 150f);

        // --- full-screen 2D map + wheelchair icon ----------------------------
        var mapGO = new GameObject("TwoDMapFull", typeof(RectTransform), typeof(RawImage));
        mapGO.layer = LayerMask.NameToLayer("UI");
        var mapRT = mapGO.GetComponent<RectTransform>();
        mapRT.SetParent(canvas.transform, false);
        mapRT.anchorMin = Vector2.zero;
        mapRT.anchorMax = Vector2.one;
        mapRT.offsetMin = Vector2.zero;
        mapRT.offsetMax = Vector2.zero;
        mapRT.SetSiblingIndex(0); // behind all HUD panels
        var rawImg = mapGO.GetComponent<RawImage>();
        rawImg.texture = rt;
        rawImg.raycastTarget = false; // view only, no tap-to-drive

        var iconGO = new GameObject("WheelchairIcon", typeof(RectTransform), typeof(Image));
        iconGO.layer = LayerMask.NameToLayer("UI");
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.SetParent(mapRT, false);
        iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.sizeDelta = new Vector2(76f, 76f);
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.sprite = wheelSprite;
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;

        mapGO.SetActive(false); // TwoDMapView toggles at runtime

        // --- bump the mini-map render texture to 1024 for full-screen --------
        if (rt != null && rt.width < 1024)
        {
            // Detach from the camera first so resizing doesn't log
            // "Releasing render texture that is set as Camera.targetTexture".
            Camera mmCam = miniMap != null ? miniMap.GetComponent<Camera>() : null;
            if (mmCam != null) mmCam.targetTexture = null;
            rt.Release();
            rt.width = 1024;
            rt.height = 1024;
            if (mmCam != null) mmCam.targetTexture = rt;
            EditorUtility.SetDirty(rt);
        }

        // --- components + refs -----------------------------------------------
        var tdv = managers.GetComponent<TwoDMapView>();
        if (tdv == null) tdv = managers.AddComponent<TwoDMapView>();
        tdv.twoDMap = rawImg;
        tdv.miniMapPanel = miniMapPanel != null ? miniMapPanel.gameObject : null;
        tdv.wheelchairIcon = iconRT;
        tdv.miniMap = miniMap;

        var tabBar = managers.GetComponent<ModeTabBar>();
        if (tabBar == null) tabBar = managers.AddComponent<ModeTabBar>();
        tabBar.magicTab = magic;
        tabBar.explorerTab = explorer;
        tabBar.guideTab = guide;
        tabBar.mapTab = map;

        // --- listeners --------------------------------------------------------
        if (gmm != null)
        {
            UnityEventTools.AddPersistentListener(magic.onClick, gmm.SetModeMagicTravel);
            UnityEventTools.AddPersistentListener(explorer.onClick, gmm.SetModeExplorer);
            UnityEventTools.AddPersistentListener(guide.onClick, gmm.SetModeSmartGuide);
        }
        UnityEventTools.AddPersistentListener(map.onClick, tdv.ToggleView);
        if (shop != null)
            UnityEventTools.AddPersistentListener(shopTab.onClick, shop.Open);

        // --- retire the modal pause overlay (tabs replace it) ----------------
        var pause = canvas.transform.Find("PauseOverlayPanel");
        if (pause != null) pause.gameObject.SetActive(false);

        // --- keep the tab bar always on top -----------------------------------
        // Smart Guide (and any future overlay) draws a full-screen scrim; render
        // the persistent tabs above everything so they stay visible + clickable.
        topBar.SetAsLastSibling();

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        return "OK tabs[magic,explorer,guide,map,shop] tdv=" + (tdv != null) +
               " tabBar=" + (tabBar != null) + " gmm=" + (gmm != null) +
               " shop=" + (shop != null) + " miniMap=" + (miniMap != null) +
               " icon=" + (wheelSprite != null) + " rt=" + (rt != null ? rt.width : -1);
    }

    // --- helpers -------------------------------------------------------------

    static Button NewTab(string name, Transform parent, string label, float x, float w)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(w, 86f);
        rt.anchoredPosition = new Vector2(x, 0f);

        var img = go.GetComponent<Image>();
        img.color = Black;
        if (_rounded != null) { img.sprite = _rounded; img.type = Image.Type.Sliced; }

        var outline = go.AddComponent<Outline>();
        outline.effectColor = Yellow;
        outline.effectDistance = new Vector2(3f, -3f);

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
        if (_fredoka != null) tmp.font = _fredoka;
        tmp.text = label;
        tmp.color = Yellow;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 40f;
        tmp.margin = new Vector4(8f, 4f, 8f, 4f);

        return btn;
    }
}
