using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Makes the top tabs the single, scannable menu and retires the pause modal:
///   - assigns ModeTabBar.shopTab + tabScan.options (the 5 tab buttons)
///   - repoints Btn_SmartMenu onClick -> ModeTabBar.Focus, relabels it "MENU"
///   - adds a "MENU" button to DestinationPanel (Magic) -> ModeTabBar.Focus, and
///     appends it to that panel's switch-scan ring
///   - deletes the now-unreferenced PauseOverlayPanel
/// Idempotent. Run via Coplay execute_script (editor only); Play mode must be off.
/// </summary>
public static class FixMenuConsistency
{
    static readonly Color Yellow = new Color(1f, 1f, 0f, 1f);
    static readonly Color Black = new Color(0f, 0f, 0f, 1f);
    static Sprite _rounded;
    static TMP_FontAsset _fredoka;

    public static string Execute()
    {
        _rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/RoundedRect.png");
        _fredoka = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka SDF.asset");

        var canvas = GameObject.Find("GameHUDCanvas");
        if (canvas == null) return "ERROR: GameHUDCanvas not found";
        var managers = GameObject.Find("Managers");
        if (managers == null) return "ERROR: Managers not found";
        var tabBar = managers.GetComponent<ModeTabBar>();
        if (tabBar == null) return "ERROR: ModeTabBar not found";

        Button Tab(string n)
        {
            var t = canvas.transform.Find("TopBar/" + n);
            return t != null ? t.GetComponent<Button>() : null;
        }
        var magic = Tab("Btn_TabMagic");
        var explorer = Tab("Btn_TabExplorer");
        var guide = Tab("Btn_TabGuide");
        var map = Tab("Btn_TabMap");
        var shop = Tab("Btn_TabShop");

        // --- ModeTabBar: shop ref + scan ring --------------------------------
        tabBar.shopTab = shop;
        tabBar.tabScan.options = new[] { magic, explorer, guide, map, shop };
        tabBar.tabScan.gridCols = 0;
        EditorUtility.SetDirty(tabBar);

        // --- Repoint the Smart Guide menu button -----------------------------
        string smartInfo = "no-smartmenu";
        var smT = canvas.transform.Find("SmartGuidePanel/SmartGuideBox/Btn_SmartMenu");
        if (smT != null)
        {
            var sm = smT.GetComponent<Button>();
            while (sm.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(sm.onClick, 0);
            UnityEventTools.AddPersistentListener(sm.onClick, tabBar.Focus);
            SetLabel(smT, "MENU");
            smartInfo = "repointed";
        }

        // --- Add a MENU entry to the Magic Travel panel ----------------------
        string destInfo = "no-destpanel";
        var destT = canvas.transform.Find("DestinationPanel");
        if (destT != null)
        {
            var existing = destT.Find("Btn_DestMenu");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var menuBtn = NewButton("Btn_DestMenu", destT, "MENU", new Vector2(330f, 120f), 40f);
            menuBtn.transform.SetAsLastSibling();
            UnityEventTools.AddPersistentListener(menuBtn.onClick, tabBar.Focus);

            // Rebuild the panel's switch-scan ring: drop stale null entries (a
            // pre-existing dead first slot made scanning start on nothing), keep the
            // real buttons, and append MENU so a switch user can leave Magic.
            var psc = destT.GetComponent<PanelScanController>();
            if (psc != null && psc.group != null)
            {
                var opts = psc.group.options;
                var kept = new System.Collections.Generic.List<Button>();
                if (opts != null)
                    foreach (var b in opts)
                        if (b != null && b != menuBtn) kept.Add(b);
                kept.Add(menuBtn);
                psc.group.options = kept.ToArray();
                EditorUtility.SetDirty(psc);
                destInfo = "menu-added ring=" + kept.Count;
            }
            else destInfo = "menu-added (no PanelScanController)";
        }

        // --- Delete the retired pause modal ----------------------------------
        string pauseInfo = "absent";
        var pause = canvas.transform.Find("PauseOverlayPanel");
        if (pause != null) { Object.DestroyImmediate(pause.gameObject); pauseInfo = "deleted"; }

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        return "OK tabs[" + (magic && explorer && guide && map && shop) +
               "] smartMenu=" + smartInfo + " dest=" + destInfo + " pause=" + pauseInfo;
    }

    // --- helpers (match the existing tab/room button styling) ----------------

    static void SetLabel(Transform btn, string text)
    {
        var lbl = btn.Find("Label");
        if (lbl == null) return;
        var tmp = lbl.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
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
        tmp.fontSizeMin = 20f;
        tmp.fontSizeMax = fontSize;
        tmp.margin = new Vector4(12f, 6f, 12f, 6f);

        return btn;
    }
}
