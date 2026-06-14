using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Finishes Phase 4 in the scene: creates the coin/path/theme materials and the
/// Coin prefab, wires ScoreManager + ThemeManager + CoinSpawner + PathVisualizer,
/// builds the Theme Shop panel, and activates the pause overlay's THEME SHOP button.
/// Idempotent: safe to re-run.
/// </summary>
public class FinishPhase4
{
    static readonly Color Yellow = new Color(1f, 1f, 0f, 1f);
    static readonly Color Black = new Color(0f, 0f, 0f, 1f);

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
        var log = new System.Text.StringBuilder();

        // ---------- 1. Materials -------------------------------------------------
        Material coinMat = LitMat("Assets/Materials/Coin_Mat.mat",
            new Color(1f, 0.84f, 0.1f), 0.6f, emission: new Color(1f, 0.84f, 0.1f) * 0.5f, metallic: 0.7f);
        Material starMat = ParticleMat("Assets/Materials/CoinStar_Mat.mat", new Color(1f, 0.95f, 0.5f, 1f));
        Material pathMat = UnlitMat("Assets/Materials/NeonPath_Mat.mat", new Color(0f, 1f, 1f, 1f));

        Material purpleFloor = LitMat("Assets/Materials/Theme_MidnightPurple_Floor.mat",
            new Color(0.10f, 0.0f, 0.20f), 0.1f);
        Material greenWall = LitMat("Assets/Materials/Theme_NeonGreen_Wall.mat",
            new Color(0.22f, 1f, 0.08f), 0.2f, emission: new Color(0.22f, 1f, 0.08f) * 0.35f);
        Material navyFloor = LitMat("Assets/Materials/Theme_DeepNavy_Floor.mat",
            new Color(0.0f, 0.04f, 0.10f), 0.1f);
        Material pinkWall = LitMat("Assets/Materials/Theme_HotPink_Wall.mat",
            new Color(1f, 0.17f, 0.84f), 0.2f, emission: new Color(1f, 0.17f, 0.84f) * 0.35f);

        Material defaultFloor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HighContrast_Floor.mat");
        Material defaultWall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HighContrast_Wall.mat");
        log.Append("materials ok; ");

        // ---------- 2. Coin prefab -----------------------------------------------
        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PreFabs/Coin.prefab");
        if (coinPrefab == null)
        {
            var root = new GameObject("Coin");
            root.AddComponent<Coin>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "CoinBody";
            Object.DestroyImmediate(body.GetComponent<Collider>()); // pickup is distance-based, never physics
            body.transform.SetParent(root.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.45f, 0.05f, 0.45f);
            body.GetComponent<MeshRenderer>().sharedMaterial = coinMat;

            var star = new GameObject("StarPop");
            star.transform.SetParent(root.transform, false);
            var ps = star.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = 0.5f;
            main.startSpeed = 2.5f;
            main.startSize = 0.18f;
            main.startColor = new Color(1f, 0.95f, 0.4f);
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            star.GetComponent<ParticleSystemRenderer>().sharedMaterial = starMat;

            coinPrefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/PreFabs/Coin.prefab");
            Object.DestroyImmediate(root);
            log.Append("coin prefab created; ");
        }

        // ---------- 3. Scene wiring ----------------------------------------------
        var canvas = GameObject.Find("GameHUDCanvas");
        var managers = GameObject.Find("Managers");
        var avatar = GameObject.Find("Wheelchair_Avatar");
        if (canvas == null || managers == null || avatar == null)
            return "ABORT: missing GameHUDCanvas/Managers/Wheelchair_Avatar";

        var score = managers.GetComponent<ScoreManager>();
        if (score == null) score = managers.AddComponent<ScoreManager>();
        var pointsT = canvas.transform.Find("TopBar/Txt_Points");
        score.pointsLabel = pointsT != null ? pointsT.GetComponent<TextMeshProUGUI>() : null;

        var themeMgr = managers.GetComponent<ThemeManager>();
        if (themeMgr == null) themeMgr = managers.AddComponent<ThemeManager>();
        themeMgr.themes = new[]
        {
            new ThemeDefinition { themeName = "CLASSIC YELLOW", cost = 0, floorMaterial = defaultFloor, wallMaterial = defaultWall },
            new ThemeDefinition { themeName = "NEON GREEN", cost = 10, floorMaterial = purpleFloor, wallMaterial = greenWall },
            new ThemeDefinition { themeName = "CYBER PINK", cost = 25, floorMaterial = navyFloor, wallMaterial = pinkWall },
        };

        var coinSystem = GameObject.Find("CoinSystem");
        if (coinSystem == null) coinSystem = new GameObject("CoinSystem");
        var spawner = coinSystem.GetComponent<CoinSpawner>();
        if (spawner == null) spawner = coinSystem.AddComponent<CoinSpawner>();
        spawner.coinPrefab = coinPrefab;

        var viz = avatar.GetComponent<PathVisualizer>();
        if (viz == null) viz = avatar.AddComponent<PathVisualizer>(); // RequireComponent adds the LineRenderer
        viz.lineMaterial = pathMat;
        var line = avatar.GetComponent<LineRenderer>();
        line.sharedMaterial = pathMat;
        line.positionCount = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        log.Append("managers/avatar wired; ");

        // ---------- 4. Theme Shop panel -------------------------------------------
        var old = canvas.transform.Find("ThemeShopPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var panel = NewPanel("ThemeShopPanel", canvas.transform, new Color(0f, 0f, 0f, 0.92f));
        SetStretch(panel.rectTransform);

        var box = NewPanel("ShopBox", panel.transform, new Color(0f, 0f, 0f, 0.85f));
        box.rectTransform.sizeDelta = new Vector2(720f, 860f);
        ApplyRounded(box);
        AddOutline(box.gameObject, 3f);
        var boxLayout = box.gameObject.AddComponent<VerticalLayoutGroup>();
        boxLayout.childAlignment = TextAnchor.UpperCenter;
        boxLayout.spacing = 24f;
        boxLayout.padding = new RectOffset(40, 40, 30, 30);
        boxLayout.childControlWidth = false;
        boxLayout.childControlHeight = false;
        boxLayout.childForceExpandWidth = false;
        boxLayout.childForceExpandHeight = false;

        var title = NewText("Title", box.transform, "THEME SHOP", 64f, Yellow);
        title.rectTransform.sizeDelta = new Vector2(640f, 100f);
        title.fontStyle = FontStyles.Bold;

        var list = new GameObject("CardList", typeof(RectTransform));
        list.layer = LayerMask.NameToLayer("UI");
        var listRT = list.GetComponent<RectTransform>();
        listRT.SetParent(box.transform, false);
        listRT.sizeDelta = new Vector2(640f, 440f);
        var listLayout = list.AddComponent<VerticalLayoutGroup>();
        listLayout.childAlignment = TextAnchor.UpperCenter;
        listLayout.spacing = 20f;
        listLayout.childControlWidth = false;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = false;
        listLayout.childForceExpandHeight = false;

        var template = NewButton("Card_Template", list.transform, "THEME", new Vector2(640f, 130f), 40f);
        template.gameObject.SetActive(false);

        var closeBtn = NewButton("Btn_CloseShop", box.transform, "BACK TO SETTINGS", new Vector2(640f, 120f), 42f);

        var shopCtrl = managers.GetComponent<ThemeShopController>();
        if (shopCtrl == null) shopCtrl = managers.AddComponent<ThemeShopController>();
        shopCtrl.panel = panel.gameObject;
        shopCtrl.templateButton = template;
        shopCtrl.listParent = list.transform;

        // ---------- 5. Button listeners -------------------------------------------
        var gmm = managers.GetComponent<GameModeManager>();
        var shopBtnT = canvas.transform.Find("PauseOverlayPanel/SettingsBox/Btn_ThemeShop");
        if (shopBtnT == null || gmm == null) return "ABORT: Btn_ThemeShop or GameModeManager missing";
        var shopBtn = shopBtnT.GetComponent<Button>();
        shopBtn.interactable = true;
        while (shopBtn.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(shopBtn.onClick, 0);
        UnityEventTools.AddPersistentListener(shopBtn.onClick, gmm.ClosePause);
        UnityEventTools.AddPersistentListener(shopBtn.onClick, shopCtrl.Open);
        UnityEventTools.AddPersistentListener(closeBtn.onClick, shopCtrl.Close);
        UnityEventTools.AddPersistentListener(closeBtn.onClick, gmm.OpenPause);

        // Hidden in the editor; ThemeShopController.Open shows it at runtime.
        panel.gameObject.SetActive(false);
        log.Append("shop panel built; ");

        // ---------- 6. Save in place ----------------------------------------------
        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        return log + "saved=" + saved + " path=" + scene.path;
    }

    // --- material helpers --------------------------------------------------------

    static Material LitMat(string path, Color baseColor, float smoothness, Color? emission = null, float metallic = 0f)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material UnlitMat(string path, Color baseColor)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", baseColor);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material ParticleMat(string path, Color baseColor)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetColor("_BaseColor", baseColor);
        // Additive transparency so the star pop glows over the black floor.
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // --- UI helpers (same conventions as BuildGameHUD) ----------------------------

    static Image NewPanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = LayerMask.NameToLayer("UI");
        go.GetComponent<RectTransform>().SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void SetStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AddOutline(GameObject go, float thickness)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Yellow;
        outline.effectDistance = new Vector2(thickness, -thickness);
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.GetComponent<RectTransform>().SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
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

        var txt = NewText("Label", go.transform, label, fontSize, Yellow);
        SetStretch(txt.rectTransform);
        txt.fontStyle = FontStyles.Bold;
        // Auto-size so longer labels stay inside the button.
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 20f;
        txt.fontSizeMax = fontSize;
        txt.margin = new Vector4(12f, 6f, 12f, 6f);

        return btn;
    }
}
