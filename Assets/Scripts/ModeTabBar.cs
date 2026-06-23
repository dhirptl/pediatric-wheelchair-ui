using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The persistent top tab bar IS the menu - the single source of truth for mode
/// and view selection. It does two jobs:
///
///   1. Visual feedback - highlights the active control mode (Magic / Explorer /
///      Smart Guide) and reflects the 2D-map view toggle's on/off state.
///   2. Switch-access - the tabs double as a scannable menu. The bar does NOT grab
///      the two keys passively (so Explorer driving keeps them); instead a mode's
///      "MENU" affordance calls Focus(), which hands the scanner to the tab row.
///      Selecting a tab fires the same onClick a touch would, then releases focus
///      so the chosen mode's own scanning / driving resumes.
///
/// There is no separate pause/settings modal, so nothing can fall out of sync with
/// the active mode.
/// </summary>
public class ModeTabBar : MonoBehaviour
{
    public static ModeTabBar Instance { get; private set; }

    [Header("Control-mode tabs")]
    public Button magicTab;
    public Button explorerTab;
    public Button guideTab;

    [Header("View toggle tab")]
    public Button mapTab;

    [Header("Shop tab")]
    public Button shopTab;

    [Header("Switch-access scanning (options assigned by the scene builder)")]
    public ScanGroup tabScan = new ScanGroup();

    static readonly Color Active = new Color(0.55f, 0.5f, 0.05f, 1f); // lit yellow-ish
    static readonly Color Idle = new Color(0f, 0f, 0f, 1f);           // black

    private bool wired;
    private bool scanActive;

    void Awake()
    {
        Instance = this;
        if (!wired)
        {
            tabScan.OnOptionSelected += OnTabSelected;
            wired = true;
        }
    }

    void OnDestroy()
    {
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.OnModeChanged -= OnModeChanged;
        if (TwoDMapView.Instance != null)
            TwoDMapView.Instance.OnViewChanged -= OnViewChanged;
        if (Instance == this) Instance = null;
    }

    // Subscribe in Start (not OnEnable): every singleton Awake has run by now, so
    // GameModeManager.Instance / TwoDMapView.Instance are guaranteed non-null and
    // the highlight stays in lock-step with the mode/view from here on.
    void Start()
    {
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.OnModeChanged += OnModeChanged;
        if (TwoDMapView.Instance != null)
            TwoDMapView.Instance.OnViewChanged += OnViewChanged;
        Refresh();
    }

    void Update()
    {
        if (ScanFocus.IsTop(this)) tabScan.Tick();
        else if (scanActive) { tabScan.Deactivate(); scanActive = false; } // another panel took over
    }

    // --- Switch-access entry point -------------------------------------------

    /// <summary>Hand the two-switch scanner to the tab row (called from each mode's
    /// MENU affordance). Tabs aren't a passive focus grabber, so driving keeps the
    /// keys until the child explicitly opens the menu.</summary>
    public void Focus()
    {
        ScanFocus.Push(this);
        tabScan.Activate();
        scanActive = true;
    }

    private void OnTabSelected(int index)
    {
        if (tabScan.options == null || index < 0 || index >= tabScan.options.Length) return;
        var btn = tabScan.options[index];
        // Selecting a tab fires the same onClick a touch tap would (SetMode* /
        // ToggleView / Open shop), then we release the tab-scan focus so the chosen
        // mode's own scanner (or Explorer driving) takes the keys back.
        ScanFocus.Pop(this);
        tabScan.Deactivate();
        scanActive = false;
        if (btn != null && btn.interactable) btn.onClick.Invoke();
    }

    // --- Highlight feedback ---------------------------------------------------

    void OnModeChanged(GameModeManager.Mode _) => Refresh();
    void OnViewChanged(TwoDMapView.ViewMode _) => Refresh();

    void Refresh()
    {
        var gmm = GameModeManager.Instance;
        if (gmm != null)
        {
            Tint(magicTab, gmm.CurrentMode == GameModeManager.Mode.MagicTravel);
            Tint(explorerTab, gmm.CurrentMode == GameModeManager.Mode.Explorer);
            Tint(guideTab, gmm.CurrentMode == GameModeManager.Mode.SmartGuide);
        }

        var view = TwoDMapView.Instance;
        Tint(mapTab, view != null && view.CurrentView == TwoDMapView.ViewMode.TwoD);
    }

    static void Tint(Button b, bool active)
    {
        if (b == null) return;
        var img = b.targetGraphic as Image;
        if (img != null) img.color = active ? Active : Idle;
    }
}
