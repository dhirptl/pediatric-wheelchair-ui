using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The unlockable Theme Shop screen (opened from the pause overlay). One card
/// per ThemeDefinition, cloned from an inactive template button: BUY deducts
/// points, EQUIP re-skins the live map instantly. Labels refresh whenever the
/// player's points change.
///
/// The cards are built at runtime, so this screen owns its own switch-access
/// scanning (Space cycles cards + Close, Enter selects) rather than a generic
/// PanelScanController. It grabs scan focus while open so the two keys drive the
/// shop instead of the Explorer command panel.
/// </summary>
public class ThemeShopController : MonoBehaviour
{
    public GameObject panel;
    public Button templateButton;
    public Transform listParent;

    [Header("Switch-access scanning")]
    [Tooltip("Closes the shop; appended to the end of the scan ring.")]
    public Button closeButton;
    public SwitchScanner scanner = new SwitchScanner();
    [Tooltip("Highlight scale for cards without a ButtonHighlighter.")]
    public float fallbackScale = 1.08f;

    private readonly List<Button> cards = new List<Button>();
    private readonly List<TextMeshProUGUI> cardLabels = new List<TextMeshProUGUI>();
    private Button[] scanRing;          // cards + closeButton
    private int scannerLast = -1;

    void Start()
    {
        BuildCards();
        BuildScanRing();
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnPointsChanged += _ => RefreshLabels();
        if (panel != null) panel.SetActive(false);

        // The top tab bar is always on top of (and clickable over) the shop's
        // full-screen scrim, so picking any other tab must dismiss the shop -
        // otherwise the modal lingers over a mode/view the child just switched to.
        // Both touch and switch-scan paths funnel through these two events.
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.OnModeChanged += OnNavigatedAway;
        if (TwoDMapView.Instance != null)
            TwoDMapView.Instance.OnViewChanged += OnNavigatedAway;
    }

    void OnDestroy()
    {
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.OnModeChanged -= OnNavigatedAway;
        if (TwoDMapView.Instance != null)
            TwoDMapView.Instance.OnViewChanged -= OnNavigatedAway;
    }

    private void OnNavigatedAway(GameModeManager.Mode _) => CloseIfOpen();
    private void OnNavigatedAway(TwoDMapView.ViewMode _) => CloseIfOpen();

    private void CloseIfOpen()
    {
        if (panel != null && panel.activeSelf) Close();
    }

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
        RefreshLabels();
        ScanFocus.Push(this);
        scanner.Reset();
        scannerLast = scanner.CurrentIndex;
        RefreshHighlights();
    }

    public void Close()
    {
        ClearHighlights();
        ScanFocus.Pop(this);
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (scanRing == null || scanRing.Length == 0) return;
        if (!ScanFocus.IsTop(this)) return;

        int selected = scanner.Tick(scanRing.Length);
        if (scanner.CurrentIndex != scannerLast)
        {
            scannerLast = scanner.CurrentIndex;
            RefreshHighlights();
        }
        if (selected >= 0)
        {
            Button btn = scanRing[selected];
            if (btn != null && btn.interactable) btn.onClick.Invoke();
        }
    }

    private void BuildScanRing()
    {
        var ring = new List<Button>(cards);
        if (closeButton != null) ring.Add(closeButton);
        scanRing = ring.ToArray();
    }

    private void RefreshHighlights()
    {
        for (int i = 0; i < scanRing.Length; i++) SetHighlight(i, i == scanner.CurrentIndex);
    }

    private void ClearHighlights()
    {
        if (scanRing == null) return;
        for (int i = 0; i < scanRing.Length; i++) SetHighlight(i, false);
    }

    private void SetHighlight(int i, bool on)
    {
        Button btn = scanRing[i];
        if (btn == null) return;
        var hl = btn.GetComponent<ButtonHighlighter>();
        if (hl != null) { hl.SetHighlighted(on); return; }
        float s = on ? fallbackScale : 1f;
        btn.transform.localScale = new Vector3(s, s, 1f);
    }

    private void BuildCards()
    {
        var tm = ThemeManager.Instance;
        if (tm == null || tm.themes == null || templateButton == null) return;

        for (int i = 0; i < tm.themes.Length; i++)
        {
            int index = i; // capture for the closure
            Button card = Instantiate(templateButton, listParent);
            card.name = "Card_" + tm.themes[i].themeName;
            card.gameObject.SetActive(true);
            card.onClick.AddListener(() => OnCardClicked(index));
            cards.Add(card);
            cardLabels.Add(card.GetComponentInChildren<TextMeshProUGUI>());
        }
        RefreshLabels();
    }

    private void OnCardClicked(int index)
    {
        var tm = ThemeManager.Instance;
        ThemeDefinition def = tm.themes[index];

        if (tm.IsOwned(def.themeName)) tm.Equip(def.themeName);
        else if (tm.TryPurchase(def.themeName)) tm.Equip(def.themeName); // buy & wear it home
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        var tm = ThemeManager.Instance;
        var sm = ScoreManager.Instance;
        if (tm == null) return;

        for (int i = 0; i < cards.Count; i++)
        {
            ThemeDefinition def = tm.themes[i];
            string status;
            bool interactable = true;

            if (tm.IsEquipped(def.themeName))
            {
                status = "EQUIPPED";
                interactable = false;
            }
            else if (tm.IsOwned(def.themeName))
            {
                status = "TAP TO EQUIP";
            }
            else
            {
                status = "BUY: " + def.cost + " PTS";
                interactable = sm != null && sm.CurrentPoints >= def.cost;
            }

            if (cardLabels[i] != null) cardLabels[i].text = def.themeName + "\n<size=70%>" + status + "</size>";
            cards[i].interactable = interactable;
        }
    }
}
