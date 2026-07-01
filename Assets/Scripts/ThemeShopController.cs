using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The unlockable shop screen (opened from the pause overlay). It now holds two
/// sections built from the same inactive template button:
///   - THEMES : one card per ThemeDefinition (re-skins the live map).
///   - FOODS  : one card per FoodDefinition (changes the collectible you drive into).
/// BUY deducts points, EQUIP applies instantly. Because the panel is built once by
/// FinishPhase4 (which can't be re-run), the food cards - and a scroll view so both
/// sections fit - are all assembled here at runtime.
///
/// The cards are built at runtime, so this screen owns its own switch-access
/// scanning (Space cycles cards + Close, Enter selects) rather than a generic
/// PanelScanController. It grabs scan focus while open so the two keys drive the
/// shop instead of the Explorer command panel, and auto-scrolls to the highlighted
/// card so switch users can reach every option.
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

    static readonly Color Yellow = new Color(1f, 1f, 0f, 1f);

    private readonly List<Button> cards = new List<Button>();
    private readonly List<TextMeshProUGUI> cardLabels = new List<TextMeshProUGUI>();
    private readonly List<Button> foodCards = new List<Button>();
    private readonly List<TextMeshProUGUI> foodLabels = new List<TextMeshProUGUI>();
    private Button[] scanRing;          // theme cards + food cards + closeButton
    private int scannerLast = -1;

    private ScrollRect scroll;
    private RectTransform viewportRT;
    private RectTransform contentRT;

    void Start()
    {
        BuildCards();
        BuildFoodSection();
        WrapInScrollView();
        BuildScanRing();
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnPointsChanged += _ => RefreshLabels();
        if (FoodManager.Instance != null)
            FoodManager.Instance.OnFoodChanged += RefreshLabels;
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
        if (FoodManager.Instance != null)
            FoodManager.Instance.OnFoodChanged -= RefreshLabels;
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
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
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
        ring.AddRange(foodCards);
        if (closeButton != null) ring.Add(closeButton);
        scanRing = ring.ToArray();
    }

    private void RefreshHighlights()
    {
        for (int i = 0; i < scanRing.Length; i++) SetHighlight(i, i == scanner.CurrentIndex);
        ScrollTo(scanner.CurrentIndex);
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

    // ---- Theme cards (unchanged behaviour) --------------------------------------

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
    }

    private void OnCardClicked(int index)
    {
        var tm = ThemeManager.Instance;
        ThemeDefinition def = tm.themes[index];

        if (tm.IsOwned(def.themeName)) tm.Equip(def.themeName);
        else if (tm.TryPurchase(def.themeName)) tm.Equip(def.themeName); // buy & wear it home
        RefreshLabels();
    }

    // ---- Food cards -------------------------------------------------------------

    private void BuildFoodSection()
    {
        var fm = FoodManager.Instance;
        if (fm == null || fm.foods == null || templateButton == null) return;

        NewSectionHeader("FoodsHeader", "FOODS");

        for (int i = 0; i < fm.foods.Length; i++)
        {
            int index = i; // capture for the closure
            FoodDefinition def = fm.foods[i];

            Button card = Instantiate(templateButton, listParent);
            card.name = "Food_" + def.foodName;
            card.gameObject.SetActive(true);
            card.onClick.AddListener(() => OnFoodCardClicked(index));

            // Food picture on the left; nudge the label right so it clears the icon.
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.layer = card.gameObject.layer;
            var iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.SetParent(card.transform, false);
            iconRT.anchorMin = new Vector2(0f, 0.5f);
            iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(18f, 0f);
            iconRT.sizeDelta = new Vector2(96f, 96f);
            var img = iconGO.GetComponent<Image>();
            img.sprite = def.icon;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var lbl = card.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.margin = new Vector4(130f, 6f, 12f, 6f);

            foodCards.Add(card);
            foodLabels.Add(lbl);
        }
    }

    private void OnFoodCardClicked(int index)
    {
        var fm = FoodManager.Instance;
        if (fm == null || fm.foods == null) return;
        FoodDefinition def = fm.foods[index];

        if (fm.IsOwned(def.foodName)) fm.Equip(def.foodName);
        else if (fm.TryPurchase(def.foodName)) fm.Equip(def.foodName); // buy & wear it home
        RefreshLabels();
    }

    private void NewSectionHeader(string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = listParent.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(listParent, false);
        rt.sizeDelta = new Vector2(640f, 56f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 40f;
        tmp.color = Yellow;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    // ---- Labels -----------------------------------------------------------------

    private void RefreshLabels()
    {
        RefreshThemeLabels();
        RefreshFoodLabels();
    }

    private void RefreshThemeLabels()
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

    private void RefreshFoodLabels()
    {
        var fm = FoodManager.Instance;
        var sm = ScoreManager.Instance;
        if (fm == null) return;

        for (int i = 0; i < foodCards.Count; i++)
        {
            FoodDefinition def = fm.foods[i];
            string status;
            bool interactable = true;

            if (fm.IsEquipped(def.foodName))
            {
                status = "EQUIPPED";
                interactable = false;
            }
            else if (fm.IsOwned(def.foodName))
            {
                status = "TAP TO EQUIP";
            }
            else
            {
                status = "BUY: " + def.cost + " PTS";
                interactable = sm != null && sm.CurrentPoints >= def.cost;
            }

            if (foodLabels[i] != null) foodLabels[i].text = def.foodName + "\n<size=70%>" + status + "</size>";
            foodCards[i].interactable = interactable;
        }
    }

    // ---- Scroll view ------------------------------------------------------------

    /// <summary>
    /// Wrap the (already populated) card list in a clamped, vertical ScrollRect so
    /// the extra FOODS section fits inside the fixed shop box without overflowing
    /// onto the close button. Keeps the same on-screen footprint the list had.
    /// </summary>
    private void WrapInScrollView()
    {
        contentRT = listParent as RectTransform;
        if (contentRT == null) return;
        var box = contentRT.parent as RectTransform; // ShopBox
        if (box == null) return;
        int sib = contentRT.GetSiblingIndex();
        int uiLayer = contentRT.gameObject.layer;

        var scrollGO = new GameObject("CardScroll", typeof(RectTransform));
        scrollGO.layer = uiLayer;
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.SetParent(box, false);
        scrollRT.SetSiblingIndex(sib);
        scrollRT.sizeDelta = new Vector2(660f, 470f);   // the visible window

        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGO.layer = uiLayer;
        viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.SetParent(scrollRT, false);
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;

        contentRT.SetParent(viewportRT, false);
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;

        scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewportRT;
        scroll.content = contentRT;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        ResizeContentToFit();
    }

    private void ResizeContentToFit()
    {
        if (contentRT == null) return;
        var vlg = contentRT.GetComponent<VerticalLayoutGroup>();
        float spacing = vlg != null ? vlg.spacing : 0f;
        float total = vlg != null ? vlg.padding.top + vlg.padding.bottom : 0f;

        int count = 0;
        foreach (RectTransform child in contentRT)
        {
            if (!child.gameObject.activeSelf) continue;
            total += child.rect.height;
            count++;
        }
        if (count > 1) total += spacing * (count - 1);
        contentRT.sizeDelta = new Vector2(contentRT.sizeDelta.x, total);
    }

    /// <summary>Scroll so the card at the given scan-ring index is visible.</summary>
    private void ScrollTo(int index)
    {
        if (scroll == null || contentRT == null || viewportRT == null) return;
        if (index < 0 || index >= scanRing.Length) return;
        Button btn = scanRing[index];
        if (btn == null) return;
        var target = btn.transform as RectTransform;
        if (target == null || !target.IsChildOf(contentRT)) return; // e.g. Close button

        float contentH = contentRT.rect.height;
        float viewH = viewportRT.rect.height;
        float range = contentH - viewH;
        if (range <= 0f) return; // everything already fits

        // Walk children in layout order to find the target's centre distance from top.
        var vlg = contentRT.GetComponent<VerticalLayoutGroup>();
        float y = vlg != null ? vlg.padding.top : 0f;
        float spacing = vlg != null ? vlg.spacing : 0f;
        float targetCenter = -1f;
        foreach (RectTransform child in contentRT)
        {
            if (!child.gameObject.activeSelf) continue;
            float h = child.rect.height;
            if (child == target) { targetCenter = y + h * 0.5f; break; }
            y += h + spacing;
        }
        if (targetCenter < 0f) return;

        float topOffset = Mathf.Clamp(targetCenter - viewH * 0.5f, 0f, range);
        scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - topOffset / range);
    }
}
