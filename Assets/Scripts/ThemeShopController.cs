using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The unlockable Theme Shop screen (opened from the pause overlay). One card
/// per ThemeDefinition, cloned from an inactive template button: BUY deducts
/// points, EQUIP re-skins the live map instantly. Labels refresh whenever the
/// player's points change.
/// </summary>
public class ThemeShopController : MonoBehaviour
{
    public GameObject panel;
    public Button templateButton;
    public Transform listParent;

    private readonly List<Button> cards = new List<Button>();
    private readonly List<TextMeshProUGUI> cardLabels = new List<TextMeshProUGUI>();

    void Start()
    {
        BuildCards();
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnPointsChanged += _ => RefreshLabels();
        if (panel != null) panel.SetActive(false);
    }

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
        RefreshLabels();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
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
