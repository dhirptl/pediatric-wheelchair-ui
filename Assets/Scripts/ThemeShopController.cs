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

    [Header("Locked-card feedback")]
    [Tooltip("Gentle buzz when selecting a card that can't be used yet.")]
    public AudioClip lockedClip;
    [Range(0f, 1f)] public float lockedVolume = 0.5f;
    [Tooltip("Horizontal shake distance in pixels for the locked wiggle.")]
    public float shakePixels = 6f;

    private readonly List<Button> cards = new List<Button>();
    private readonly List<TextMeshProUGUI> cardLabels = new List<TextMeshProUGUI>();
    private Button[] scanRing;          // cards + closeButton
    private int scannerLast = -1;
    private Coroutine shakeRoutine;
    private AudioSource sfx;

    void Start()
    {
        sfx = GetComponent<AudioSource>();
        if (sfx == null) sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;          // 2D UI sound

        BuildCards();
        BuildScanRing();
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnPointsChanged += _ => RefreshLabels();
        if (panel != null) panel.SetActive(false);
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
            if (btn == null) return;
            if (btn.interactable) btn.onClick.Invoke();
            else PlayLockedFeedback(btn);   // never let a press do silently nothing
        }
    }

    /// <summary>
    /// Gentle "not yet" wiggle + buzz when the child selects a locked or already
    /// equipped card. Positional motion only (no luminance flash).
    /// </summary>
    private void PlayLockedFeedback(Button btn)
    {
        if (lockedClip != null) sfx.PlayOneShot(lockedClip, lockedVolume);
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(btn.transform as RectTransform));
    }

    private System.Collections.IEnumerator ShakeRoutine(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector2 basePos = rt.anchoredPosition;
        const float duration = 0.25f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            // Two soft oscillations that decay to rest.
            float wave = Mathf.Sin(t / duration * 2f * 2f * Mathf.PI) * (1f - t / duration);
            rt.anchoredPosition = basePos + new Vector2(shakePixels * wave, 0f);
            yield return null;
        }
        rt.anchoredPosition = basePos;
        shakeRoutine = null;
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
                int have = sm != null ? sm.CurrentPoints : 0;
                bool affordable = have >= def.cost;
                // Tell the child how close they are instead of a dead BUY button.
                // (PTS not the star glyph - LiberationSans SDF lacks ★.)
                status = affordable ? "BUY: " + def.cost + " PTS"
                                    : "NEED " + (def.cost - have) + " MORE PTS";
                interactable = affordable;
            }

            if (cardLabels[i] != null) cardLabels[i].text = def.themeName + "\n<size=70%>" + status + "</size>";
            cards[i].interactable = interactable;
        }
    }
}
