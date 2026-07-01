using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FoodDefinition
{
    public string foodName;
    public int cost;
    public Sprite icon;
}

/// <summary>
/// Unlockable collectible-food catalog - the sibling of <see cref="ThemeManager"/>,
/// but for the thing you drive into instead of the map skin. Equipping swaps which
/// food image every live collectible shows (via <see cref="OnFoodChanged"/>);
/// ownership and the equipped food persist in PlayerPrefs; foods[0] is the free
/// default. Icons are loaded from Resources so no scene/prefab wiring is needed.
/// </summary>
public class FoodManager : MonoBehaviour
{
    public static FoodManager Instance { get; private set; }

    /// <summary>Fired whenever the equipped food changes, so live coins restyle.</summary>
    public event Action OnFoodChanged;

    public FoodDefinition[] foods;

    [Serializable]
    private class OwnedData
    {
        public List<string> names = new List<string>();
    }

    private OwnedData owned;
    private string equipped;

    void Awake()
    {
        Instance = this;

        // The three starter foods. Icons live in Assets/Resources/Food/ so they load
        // at runtime without any serialized references (keeps the shop panel builder,
        // which can't be re-run, out of the loop).
        foods = new[]
        {
            new FoodDefinition { foodName = "ICE CREAM", cost = 0,  icon = LoadIcon("ice_cream") },
            new FoodDefinition { foodName = "CAKE",      cost = 10, icon = LoadIcon("cake") },
            new FoodDefinition { foodName = "CHOCOLATE", cost = 25, icon = LoadIcon("chocolate") },
        };

        owned = GamePrefs.GetJson<OwnedData>(GamePrefs.OwnedFoods) ?? new OwnedData();
        string defaultName = foods.Length > 0 ? foods[0].foodName : "";
        if (!owned.names.Contains(defaultName)) owned.names.Add(defaultName);
        equipped = GamePrefs.GetString(GamePrefs.EquippedFood, defaultName);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static Sprite LoadIcon(string fileName)
    {
        Sprite s = Resources.Load<Sprite>("Food/" + fileName);
        if (s == null) Debug.LogWarning("[FoodManager] Missing sprite Resources/Food/" + fileName);
        return s;
    }

    /// <summary>Icon of the currently equipped food (what live collectibles show).</summary>
    public Sprite EquippedIcon
    {
        get { FoodDefinition d = Find(equipped); return d != null ? d.icon : null; }
    }

    public bool IsOwned(string foodName) => owned.names.Contains(foodName);
    public bool IsEquipped(string foodName) => equipped == foodName;

    public bool TryPurchase(string foodName)
    {
        FoodDefinition def = Find(foodName);
        if (def == null) return false;
        if (IsOwned(foodName)) return true;
        if (ScoreManager.Instance == null || !ScoreManager.Instance.TrySpend(def.cost)) return false;

        owned.names.Add(foodName);
        GamePrefs.SetJson(GamePrefs.OwnedFoods, owned);
        return true;
    }

    public void Equip(string foodName)
    {
        if (!IsOwned(foodName) || Find(foodName) == null) return;
        equipped = foodName;
        GamePrefs.SetString(GamePrefs.EquippedFood, foodName);
        OnFoodChanged?.Invoke();
    }

    private FoodDefinition Find(string foodName)
    {
        if (foods == null) return null;
        foreach (FoodDefinition f in foods)
            if (f.foodName == foodName) return f;
        return null;
    }
}
